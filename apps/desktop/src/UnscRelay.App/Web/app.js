"use strict";
const $ = (id) => document.getElementById(id);
const native = !!window.chrome?.webview;
const pending = /* @__PURE__ */ new Map();
let state = null, activeId = null, layout = "tabs", selectedProfile = null;
const panes = /* @__PURE__ */ new Map();
const modal = $("modal");
modal.addEventListener("close", () => {
  const host = $("toasts");
  if (host.parentElement === modal) {
    document.body.append(host);
    raiseToasts();
  }
});
const closedFolders = /* @__PURE__ */ new Set();
const escapeText = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
function raiseToasts() {
  const host = $("toasts");
  if (!host.childElementCount) return;
  const owner = modal.open ? modal : document.body;
  if (host.parentElement !== owner) owner.append(host);
  if (host.showPopover) {
    if (host.matches(":popover-open")) host.hidePopover();
    host.showPopover();
  } else host.dataset.fallback = "true";
}
function toast(text, error = false) {
  const e = document.createElement("div");
  e.className = "toast" + (error ? " error" : "");
  e.textContent = text;
  $("toasts").append(e);
  raiseToasts();
  setTimeout(() => {
    e.remove();
    const host = $("toasts");
    if (!host.childElementCount && host.hidePopover && host.matches(":popover-open")) host.hidePopover();
  }, 6500);
}
function rpc(action, payload = {}) {
  if (!native) return Promise.reject(new Error("Questa anteprima non è collegata al motore Windows."));
  const id = crypto.randomUUID();
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    window.chrome.webview.postMessage({ id, action, payload });
  });
}
function call(action, payload = {}) {
  return rpc(action, payload).catch((e) => {
    toast(e.message, true);
    throw e;
  });
}
function receive(message) {
  const { type, payload: p } = message;
  window.recordingMessage?.(type, p);
  if (window.fileMessage) window.fileMessage(type, p);
  if (type === "reply") {
    const waiter = pending.get(p.id);
    if (!waiter) return;
    pending.delete(p.id);
    p.error ? waiter.reject(Object.assign(new Error(uiText(p.error)), { retryable: p.retryable === true })) : waiter.resolve(p.result);
  }
  if (type === "vaultLocked") window.dispatchEvent(new Event("relay-vault-locked"));
  if (type === "windowsSessionLocked") {
    if (state) {
      state.appLocked = true;
      state.vault.unlocked = false;
      state.vault.keys = [];
      state.vault.passwords = [];
    }
    window.renderPersonalHome?.();
    if (modal.open) modal.close();
    stopAppMusic();
    waitForApplicationLogin().then(() => {
      $("access-gate")?.remove();
      startAppMusic();
      scheduleDesktopLayout();
    });
  }
  if (type === "accountErasureClosed") {
    for (const dialog of document.querySelectorAll("dialog[data-account-erasure]")) if (dialog.dataset.accountErasure === p.token) dialog.close();
  }
  if (type === "plainExportClosed") {
    for (const dialog of document.querySelectorAll("dialog[data-plain-export]")) if (dialog.dataset.plainExport === p.token) dialog.close();
  }
  if (type === "account") {
    if (state) state.account = p;
    window.dispatchEvent(new Event("relay-account"));
  }
  if (type === "data") update(p);
  if (type === "updateAvailable") {
    window.relayAvailableUpdate = p;
    if (state) {
      if (p.required) updatesDialog().catch(() => {
      });
      else toast(uiText("È disponibile ") + p.release.version + ". Apri Aggiornamenti per leggere le novità.");
    }
  }
  if (type === "updateCheckFailed" && p.required) {
    window.relayAvailableUpdate = { required: true };
    if (state) updatesDialog().catch(() => {
    });
  }
  if (type === "updateProgress") renderUpdateProgress(p);
  if (type === "updateSessionsClosed") {
    for (const pane of panes.values()) {
      window.cancelAutoReconnect?.(pane);
      pane.connected = false;
      pane.status.textContent = uiText("Sessione chiusa per aggiornamento");
      pane.reconnect.hidden = false;
    }
  }
  if (type === "notice") toast(p.text);
  if (type === "storageStatus") {
    const pane = panes.get(p.sessionId);
    pane?.storageDisconnected?.(p.text);
    if (pane?.fileWorkspace) {
      pane.setFileConnection(false);
      pane.status.textContent = p.text;
      pane.reconnect.hidden = false;
    }
  }
  if (type === "nativeAuthenticated") {
    const pane = panes.get(p.sessionId);
    if (pane) {
      pane.localStateReceived = true;
      pane.connected = true;
      pane.hadConnection = true;
      pane.status.textContent = p.text;
      pane.reconnect.hidden = true;
    }
  }
  if (type === "localStatus") {
    const pane = panes.get(p.sessionId);
    if (pane) {
      pane.localStateReceived = true;
      pane.connected = p.connected;
      pane.status.textContent = p.text;
      pane.reconnect.hidden = pane.local ? false : p.connected;
      pane.term.writeln("\r\n\x1B[33m" + p.text + "\x1B[0m");
    }
  }
  if (type === "desktopStatus") {
    const pane = panes.get(p.sessionId);
    if (pane) {
      pane.desktopStateReceived = true;
      pane.connected = p.connected;
      pane.status.textContent = p.text;
      pane.reconnect.hidden = p.connected || p.pending;
      scheduleDesktopLayout();
    }
  }
  if (type === "editorAutoPaused") toast(uiText("Connessione a consumo: salvataggio esterno conservato sul PC. Caricalo manualmente oppure disattiva la modalità a consumo."));
  if (type === "output") {
    const pane = panes.get(p.sessionId);
    if (pane) pane.term.write(Uint8Array.from(atob(p.data), (c) => c.charCodeAt(0)));
  }
  if (type === "assistantDisconnected") {
    const pane = panes.get(p.sessionId);
    if (pane) {
      pane.connected = false;
      pane.files = false;
      pane.status.textContent = uiText("Chiusa su richiesta API autorizzata");
      pane.reconnect.hidden = false;
    }
  }
  if (type === "recoveryNeeded") recoveryNeeded(p);
  if (type === "status") {
    const pane = panes.get(p.sessionId);
    if (pane) {
      pane.status.textContent = p.text;
      if (p.connected === false) {
        pane.connected = false;
        pane.files = false;
        pane.reconnect.hidden = false;
      }
      pane.term.writeln("\r\n\x1B[33m" + p.text + "\x1B[0m");
      window.autoReconnectStatus?.(pane, p);
    }
  }
  if (type === "vault") {
    if (state) {
      state.vault.unlocked = p.unlocked;
      state.vault.keys = [];
      state.vault.passwords = [];
      window.renderPersonalHome?.();
      window.refreshConnectionVault?.();
      window.refreshSettingsAccess?.();
      renderProfiles();
      if (modal.open && modal.dataset.keyContext === "true") keysDialog();
    }
    toast(uiText("Archivio chiavi bloccato"));
  }
  if (type === "helloPending" && !window.vaultPromptActive) {
    const body = showModal(uiText("Windows Hello"), "VERIFICA LOCALE");
    notice(body, uiText("Completa la richiesta di Sicurezza di Windows con il PIN, volto o impronta. Puoi annullarla nella finestra di Windows: puoi accedere online con il tuo account e la 2FA."));
  }
  if (type === "helloFinished" && !window.vaultPromptActive && $("modal-title").textContent === "Windows Hello") archiveAccessDialog();
  if (type === "transfer") {
    $("transfer-bar").hidden = false;
    $("transfer-name").textContent = p.name;
    $("transfer-progress").value = p.progress;
    $("transfer-status").textContent = p.status + " " + Math.round(p.progress) + "%";
    $("cancel-transfer").disabled = !!p.finished;
  }
  if (type === "shutdown") {
    document.getElementById("access-gate")?.remove();
    if (modal.open) modal.close();
    stopStartupTheme();
    stopAppMusic();
    if (p.enabled) playSequence("shutdown", p.volume, (failed) => rpc("shutdownDone", { failed }));
    else rpc("shutdownDone");
  }
}
if (native) window.chrome.webview.addEventListener("message", (e) => receive(e.data));
function update(snapshot) {
  if (snapshot?.data) {
    const previousMusic = state?.data?.settings;
    state = snapshot;
    if (previousMusic && (previousMusic.appMusicEnabled !== snapshot.data.settings.appMusicEnabled || previousMusic.appMusicVolume !== snapshot.data.settings.appMusicVolume) && !document.body.classList.contains("startup-covered") && !state.appLocked && state.vault?.unlocked) window.startAppMusic?.();
    applyResourceSaving();
    setInterfaceLanguage(snapshot.data.settings.language || "it");
    renderProfiles();
    window.renderConnectionViews?.();
    window.renderPersonalHome?.();
    $("version").textContent = snapshot.version;
    if (snapshot.criticalUpdateRequired) {
      window.relayAvailableUpdate = { ...window.relayAvailableUpdate, required: true };
      queueMicrotask(() => updatesDialog().catch(() => {
      }));
    }
  }
}
function renderProfiles() {
  interfaceLabel($("vault-state"), state?.vault.unlocked ? "SBLOCCATO" : "BLOCCATO");
  const list = $("profiles");
  list.replaceChildren();
  const query = $("profile-search").value.toLowerCase();
  const profiles = (state?.data.profiles ?? []).filter((p) => [p.name, p.host, p.folder, p.tags].join(" ").toLowerCase().includes(query));
  const folders = [.../* @__PURE__ */ new Set([...query ? [] : state?.data.folders ?? [], ...profiles.map((p) => p.folder || "Connessioni")])].sort();
  if (!profiles.length) {
    const empty = document.createElement("div");
    empty.className = "empty";
    empty.textContent = query ? "Nessuna connessione trovata." : "Nessuna connessione salvata.\nUsa + per aggiungere un host.";
    list.append(empty);
  }
  const roots = /* @__PURE__ */ new Map();
  for (const folder of folders) {
    let branch = roots;
    let prefix = "";
    for (const part of folder.split("/").filter(Boolean)) {
      prefix = prefix ? prefix + "/" + part : part;
      if (!branch.has(part)) branch.set(part, { path: prefix, children: /* @__PURE__ */ new Map() });
      branch = branch.get(part).children;
    }
  }
  function renderBranch(branch, parent) {
    for (const [name, node] of branch) {
      const folder = node.path;
      const details = document.createElement("details");
      details.className = "folder-branch";
      details.dataset.folderPath = folder;
      details.open = !!query || !closedFolders.has(folder);
      const heading = document.createElement("summary");
      heading.className = "folder-label";
      const label = document.createElement("span");
      label.textContent = name;
      const edit = button("⋯", () => folderDialog(folder));
      interfaceAttribute(edit, 'title', "Gestisci cartella");
      edit.onclick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        folderDialog(folder);
      };
      edit.classList.add("folder-manage");
      heading.append(label);
      details.append(heading, edit);
      const children = document.createElement("div");
      children.className = "folder-children";
      details.append(children);
      details.ontoggle = () => {
        if (query) return;
        details.open ? closedFolders.delete(folder) : closedFolders.add(folder);
      };
      parent.append(details);
      for (const p of profiles.filter((p2) => (p2.folder || "Connessioni") === folder).sort((a, b) => Number(b.favorite) - Number(a.favorite) || a.name.localeCompare(b.name))) {
        const row = document.createElement("button");
        row.className = "profile-item" + (selectedProfile === p.id ? " active" : "");
        row.style.borderLeftColor = p.environment === "produzione" ? "#d68d7e" : p.environment === "test" ? "#c4b17a" : "#799f77";
        row.innerHTML = '<span class="server-icon">▤</span><div><strong>' + escapeText(p.name) + "</strong><small>" + escapeText(p.protocol === "local" || p.protocol === "native" ? profileEndpoint(p) : p.username + "@" + p.host + ":" + p.port) + '</small></div><span class="edit-profile" title="Modifica">⋯</span>';
        if (["produzione", "production"].includes((p.environment || "").trim().toLowerCase())) {
          const badge = document.createElement("small");
          badge.className = "production-label";
          badge.textContent = "⚠ " + uiText("Produzione");
          row.querySelector("div").append(badge);
        }
        row.dataset.profileId = p.id;
        row.onclick = (e) => {
          selectedProfile = p.id;
          document.querySelectorAll(".profile-item").forEach((item) => item.classList.toggle("active", item.dataset.profileId === p.id));
          if (e.target.closest(".edit-profile")) profileDialog(p);
        };
        row.ondblclick = () => connect(p);
        row.title = uiText("Doppio clic per connettere · ") + p.protocol.toUpperCase();
        children.append(row);
      }
      renderBranch(node.children, children);
    }
  }
  renderBranch(roots, list);
}
function folderDialog(existing) {
  const body = showModal(existing ? "Gestisci cartella" : "Nuova cartella", "ORGANIZZAZIONE / CONNESSIONI");
  const form = document.createElement("form");
  field(form, uiText("Percorso della cartella"), "path", existing || "");
  notice(form, uiText("Usa / per le sottocartelle, ad esempio Università/Laboratorio. Le cartelle vuote restano salvate."));
  const actions = document.createElement("div");
  actions.className = "form-actions";
  if (existing) actions.append(localizedButton("Elimina cartella vuota", async () => {
    update(await call("folderDelete", { path: existing }));
    modal.close();
  }, "danger"));
  actions.append(localizedButton("Annulla", () => modal.close()));
  const save = document.createElement("button");
  save.className = "primary";
  save.textContent = existing ? "Rinomina cartella" : "Crea cartella";
  actions.append(save);
  form.append(actions);
  form.onsubmit = async (e) => {
    e.preventDefault();
    const path = new FormData(form).get("path");
    try {
      update(await call(existing ? "folderRename" : "folderCreate", existing ? { path: existing, replacement: path } : { path }));
      modal.close();
    } catch {
    }
  };
  body.append(form);
}
function showModal(title, kicker = "UNSC RELAY", onBack = null, wide = false) {
  if ([...panes.values()].some((p) => p.detachedWindow)) rpc("activateMain").catch(() => {
  });
  window.disposeSettingsPreview?.();
  window.refreshSettingsAccess = null;
  window.refreshConnectionVault = null;
  $("modal-title").textContent = title;
  $("modal-kicker").textContent = kicker;
  $("modal-body").replaceChildren();
  $("modal-body").removeAttribute("tabindex");
  modal.classList.toggle("manager-modal", wide);
  modal.classList.remove("key-manager-modal", "native-profile-modal", "algorithm-modal", "session-picker-modal", "conversion-modal", "sync-modal", "workflow-modal", "connection-modal", "conflict-modal", "diff-modal", "settings-modal", "snippets-modal", "updates-modal");
  modal.dataset.keyContext = String(!!onBack || title === "Client Key Manager");
  const back = $("modal-back");
  back.textContent = uiText("← Chiavi salvate");
  back.hidden = !onBack;
  back.onclick = onBack;
  if (!modal.open) modal.showModal();
  raiseToasts();
  scheduleDesktopLayout();
  return $("modal-body");
}
function button(text, action, cls = "") {
  const b = document.createElement("button");
  b.type = "button";
  b.textContent = text;
  b.className = cls;
  b.onclick = () => Promise.resolve(action()).catch(() => {
  });
  return b;
}
function notice(parent, text) {
  const p = document.createElement("p");
  p.className = "notice";
  interfaceUiLabel(p, text);
  parent.append(p);
}
function field(form, label, name, value = "", type = "text", options) {
  const l = document.createElement("label"), caption = interfaceUiLabel(document.createElement("span"), label);
  l.append(caption);
  let input = document.createElement(options ? "select" : type === "textarea" ? "textarea" : "input");
  if (!options && type !== "textarea") input.type = type;
  input.name = name;
  input.id = "relay-field-" + crypto.randomUUID();
  l.htmlFor = input.id;
  const interfaceKey = interfaceKnownKey(label);
  if (interfaceKey !== null) {
    input.dataset.interfaceLabel = interfaceKey;
    input.setAttribute("aria-label", uiText(interfaceKey));
  } else input.setAttribute("aria-label", label);
  if (options) for (const [v, t] of options) {
    const o = document.createElement("option");
    o.value = v;
    o.textContent = t;
    input.append(o);
  }
  input.value = value ?? "";
  l.append(input);
  form.append(l);
  return input;
}
function profileDialog(existing) {
  return existing ? renderConnectionProfile(existing) : sessionTypeDialog();
}
function profileEndpoint(profile) {
  if (profile.storage?.browser) return profile.native.kind === "s3" ? "S3 / " + profile.storage.bucket : profile.host + ":" + profile.port;
  if (profile.protocol === "native") return "CLIENT / " + profile.native.kind + " / " + profile.native.target;
  return profile.protocol === "local" ? "LOCALE / " + { cmd: "CMD", powershell: "Windows PowerShell", pwsh: "PowerShell 7", wsl: "WSL" }[profile.localShell || "cmd"] + (profile.wslDistribution ? " · " + profile.wslDistribution : "") : profile.host;
}
function refreshStage() {
  for (const pane of panes.values()) {
    const production = pane.profile?.environment === "produzione";
    pane.container.classList.toggle("production-session", production);
    const header = pane.container.querySelector(".pane-header");
    if (header && !header.querySelector(".environment-warning") && production) {
      const warning = document.createElement("strong");
      warning.className = "environment-warning";
      warning.textContent = uiText("PRODUZIONE");
      header.prepend(warning);
    }
    if (!production) header?.querySelector(".environment-warning")?.remove();
  }
  renderSplitStage();
}
async function connect(profile) {
  if (profile.storage?.browser) return openStorageWorkspace(profile);
  if (profile.protocol === "native" && profile.native.kind === "browser") return connectDesktopPane(profile);
  if (profile.protocol === "native" && profile.native.kind === "file") return connectLocalFiles(profile);
  if (!await ensureConnectionVault(profile)) return null;
  if (profile.protocol === "sftp") return openFileWorkspace(profile);
  if (["rdp", "vnc"].includes(profile.protocol)) return connectDesktopPane(profile);
  const id = crypto.randomUUID(), container = document.createElement("section");
  container.className = "terminal-pane";
  container.dataset.sessionId = id;
  const header = document.createElement("div");
  header.className = "pane-header";
  const name = document.createElement("span");
  name.textContent = profile.name + " / " + profileEndpoint(profile);
  const status = document.createElement("span");
  status.className = "pane-status";
  status.textContent = uiText("Connessione…");
  header.append(name, status);
  const surface = document.createElement("div");
  surface.className = "terminal-surface";
  container.append(header, surface);
  container.onpointerdown = () => {
    activeId = id;
    refreshStage();
  };
  $("terminals").append(container);
  const tab = document.createElement("button");
  tab.className = "tab";
  tab.setAttribute("role", "tab");
  const title = document.createElement("span");
  title.textContent = profile.name;
  tab.append(title);
  const close = document.createElement("span");
  close.className = "tab-close";
  close.textContent = "×";
  tab.append(close);
  tab.onclick = (e) => {
    if (e.target === close) closePane(id);
    else {
      activeId = id;
      refreshStage();
    }
  };
  $("tabs").append(tab);
  const term = new Terminal({ fontFamily: "Consolas, monospace", fontSize: 13, cursorBlink: !state?.data.settings.resourceSaving, scrollback: 1e4, theme: { background: "#071114", foreground: "#dfe8dc", cursor: "#b6cc86", selectionBackground: "#3b513b", scrollbarSliderBackground: "#61784d99", scrollbarSliderHoverBackground: "#91aa6dbb", scrollbarSliderActiveBackground: "#b6cc86bb", black: "#1c2a25", red: "#e58b7b", green: "#a3bb71", yellow: "#d0ba78", blue: "#87a9cc", magenta: "#b29cbb", cyan: "#8fbbb1", white: "#d2ded2" } });
  const fit = new FitAddon.FitAddon(), search = new SearchAddon.SearchAddon();
  term.loadAddon(fit);
  term.loadAddon(search);
  term.open(surface);
  const reconnect = localizedButton("Riconnetti", () => reconnectPane(id));
  reconnect.className = "reconnect-button";
  if (profile.protocol === "local") reconnect.textContent = uiText("Riavvia");
  reconnect.hidden = true;
  header.append(reconnect);
  const pane = { id, profile, container, tab, title, name, term, fit, search, status, reconnect, connected: false, files: false, connecting: false, local: ["local", "native"].includes(profile.protocol) };
  pane.outputBatch = installTerminalOutputBatcher(term, () => !!state?.data.settings.resourceSaving && (!pane.container.isConnected || pane.container.style.display === "none" || pane.container.ownerDocument.hidden));
  wireTab(pane);
  wireTerminalClipboard(pane);
  if (!pane.local) {
    wireDirectoryReporting(pane);
    wireTerminalDrop(pane, surface);
    wireAutoReconnect(pane);
    wireRecording(pane);
  } else if (profile.protocol === "native" && ["ssh", "sftp"].includes(profile.native?.kind) && !profile.native?.executable) {
    wireAutoReconnect(pane);
    if (profile.native.kind === "ssh") {
      wireDirectoryReporting(pane);
      wireRecording(pane);
    }
  }
  panes.set(id, pane);
  activeId = id;
  refreshStage();
  term.writeln("\x1B[32mUNSC RELAY // " + profile.name + "\x1B[0m\r\n");
  term.onData((data) => {
    if (pane.connected) rpc("input", { sessionId: id, data }).catch(() => {
    });
  });
  term.onResize(({ cols, rows }) => {
    if (pane.connected) rpc("resize", { sessionId: id, cols, rows }).catch(() => {
    });
  });
  await startPane(pane);
  return pane;
}
async function startPane(pane, automatic = false) {
  if (pane.connecting) return;
  pane.connecting = true;
  pane.localStateReceived = false;
  pane.reconnect.disabled = true;
  pane.status.textContent = pane.local ? "Avvio…" : "Connessione…";
  try {
    const result = await call("connect", { profileId: pane.profile.id, sessionId: pane.id, cols: pane.term.cols, rows: pane.term.rows, automatic });
    pane.connectError = null;
    if (!(pane.profile.protocol === "native" && ["ssh", "sftp"].includes(pane.profile.native?.kind))) pane.hadConnection = true;
    if (!pane.desktopStateReceived && !pane.localStateReceived) pane.connected = pane.local ? result.running !== false : !result.pending;
    pane.files = result.filesAvailable;
    if (!pane.localStateReceived && !pane.desktopStateReceived) pane.status.textContent = pane.profile.protocol === "native" && ["ssh", "sftp"].includes(pane.profile.native?.kind) ? "Autenticazione OpenSSH…" : pane.local ? "In esecuzione" : result.pending ? "Negoziazione RDP…" : "Connesso";
    pane.reconnect.hidden = pane.local ? false : pane.connected;
    pane.fit.fit();
    pane.term.focus();
  } catch (e) {
    pane.connectError = e;
    pane.connected = false;
    pane.files = false;
    pane.reconnect.hidden = false;
    pane.status.textContent = uiText("Non connesso");
    pane.term.writeln("\r\n\x1B[31m" + e.message + "\x1B[0m");
  } finally {
    pane.connecting = false;
    pane.reconnect.disabled = false;
  }
}
async function reconnectPane(id, automatic = false) {
  const pane = panes.get(id);
  if (pane && pane.hadConnection && !pane.desktop && (["ssh", "sftp"].includes(pane.profile.protocol) || pane.profile.protocol === "native" && ["ssh", "sftp"].includes(pane.profile.native?.kind) && !pane.profile.native?.executable)) return recoverProfilePanes(pane, automatic);
  if (!pane || !pane.local && pane.connected || pane.connecting || pane.reconnecting) return;
  if (pane.local && pane.connected && !await call("confirmAction", { text: "Riavviare questa shell locale? I programmi al suo interno saranno chiusi e i comandi di avvio eseguiti di nuovo." })) return;
  if (!automatic) resetAutoReconnect(pane);
  pane.reconnecting = true;
  pane.reconnect.disabled = true;
  try {
    const saved = state.data.profiles.find((p) => p.id === pane.profile.id);
    if (!saved) return toast(uiText("Il profilo non esiste più. Crea una nuova connessione."), true);
    if (saved.protocol !== pane.profile.protocol) return toast(uiText("Il tipo di sessione è cambiato: apri una nuova scheda."), true);
    if (automatic && JSON.stringify(saved) !== JSON.stringify(pane.profile)) throw new Error("Profilo modificato: riconnetti manualmente.");
    await call("disconnect", { sessionId: id, automatic });
    pane.profile = saved;
    pane.term.writeln("\r\n\x1B[32m--- " + (pane.local ? "RIAVVIO SHELL LOCALE" : "RICONNESSIONE SSH") + " ---\x1B[0m");
    await startPane(pane, automatic);
  } finally {
    pane.reconnecting = false;
    pane.reconnect.disabled = false;
  }
}
function renameTab(pane) {
  const body = showModal(uiText("Rinomina scheda"), "SESSIONE / IL PROFILO RESTA INVARIATO");
  const form = document.createElement("form");
  const input = field(form, uiText("Nome della scheda"), "tabName", pane.title.textContent);
  input.required = true;
  input.maxLength = 100;
  const save = document.createElement("button");
  save.textContent = uiText("Rinomina");
  save.className = "primary";
  form.append(save);
  form.onsubmit = (e) => {
    e.preventDefault();
    const value = input.value.trim();
    if (!value) return;
    pane.title.textContent = value;
    pane.name.textContent = value + " / " + profileEndpoint(pane.profile);
    modal.close();
  };
  body.append(form);
}
function wireTab(pane) {
  const tab = pane.tab;
  tab.addEventListener("click", (e) => {
    if (pane.detachedWindow && !e.target.closest(".tab-close")) {
      e.stopImmediatePropagation();
      call("detachedFocus", { id: pane.id });
    }
  }, true);
  tab.dataset.sessionId = pane.id;
  tab.draggable = true;
  interfaceAttribute(tab, 'title', "Doppio clic per rinominare · trascina per riordinare");
  tab.ondblclick = () => renameTab(pane);
  tab.ondragstart = (e) => {
    e.dataTransfer.setData("application/x-unsc-session", pane.id);
    e.dataTransfer.effectAllowed = "move";
  };
  tab.ondragover = (e) => {
    if ([...e.dataTransfer.types].includes("application/x-unsc-session")) e.preventDefault();
  };
  tab.ondrop = (e) => {
    e.preventDefault();
    const moving = panes.get(e.dataTransfer.getData("application/x-unsc-session"));
    if (!moving || moving.id === pane.id) return;
    tab.before(moving.tab);
    const ordered = [...$("tabs").children].map((t) => panes.get(t.dataset.sessionId)).filter(Boolean);
    panes.clear();
    for (const p of ordered) {
      panes.set(p.id, p);
      if (!p.detachedWindow) $("terminals").append(p.container);
    }
    refreshStage();
  };
}
async function closePane(id) {
  const p = panes.get(id);
  if (!p) return;
  if (p.editor) return closeEditorPane(p);
  if (p.local && p.connected && !await call("confirmAction", { text: "Chiudere la shell locale «" + p.title.textContent + "» e i programmi avviati al suo interno?" })) return;
  if (p.connecting) return toast(uiText("Attendi il completamento della connessione."), true);
  try {
    await call("disconnect", { sessionId: id });
    forgetExternalEdits(id);
    window.cancelAutoReconnect?.(p);
    clearTimeout(p.followTimer);
    finishDetachedPane(p);
    p.term.dispose();
    p.container.remove();
    p.tab.remove();
    panes.delete(id);
    activeId = [...panes.keys()].at(-1) ?? null;
    refreshStage();
  } catch {
  }
}
async function toggleFiles() {
  const pane = panes.get(activeId);
  const profile = pane?.profile || state.data.profiles.find((p) => p.id === selectedProfile);
  if (profile?.protocol === "native" && ["ssh", "sftp"].includes(profile.native?.kind)) {
    const source = pane?.nativeSourceId ? panes.get(pane.nativeSourceId) : pane;
    if (!source || !source.local || !source.connected) return toast(uiText("Apri prima il terminale OpenSSH di questo profilo."), true);
    return openFileWorkspace(profile, source);
  }
  if (!profile || !["ssh", "sftp"].includes(profile.protocol)) return toast(uiText("Seleziona una connessione SSH o SFTP."), true);
  return openFileWorkspace(profile);
}
async function listFiles() {
  return panes.get(activeId)?.refreshFiles?.();
}
function formatSize(n) {
  return n > 1048576 ? (n / 1048576).toFixed(1) + " MiB" : n > 1024 ? (n / 1024).toFixed(1) + " KiB" : n + " B";
}
function keysDialog() {
  return renderKeysManager();
}
function generateKeyDialog() {
  return renderKeyGeneration();
}
function archiveAccessDialog() {
  settingsDialog("access");
}
function hostsDialog() {
  return renderHostsManager();
}
function snippetsDialog() {
  return renderSnippetLibrary();
}
function palette() {
  return renderCommandPalette();
}
async function saveWorkspace() {
  const body = showModal(uiText("Salva spazio di lavoro"));
  const form = document.createElement("form");
  field(form, uiText("Nome"), "name", "Workspace");
  const note = document.createElement("p");
  note.className = "muted";
  note.textContent = uiText("Salva schede, riquadri, proporzioni e posizione delle finestre separate. La riapertura riconnette le sessioni solo su richiesta.");
  form.append(note);
  const save = document.createElement("button");
  save.textContent = uiText("Salva disposizione");
  save.className = "primary";
  form.append(save);
  form.onsubmit = async (e) => {
    e.preventDefault();
    save.disabled = true;
    try {
      const saved = [...panes.values()].filter((p) => !p.editor && !p.replay);
      const windowPlacements = await call("workspaceWindowCapture", { sessionIds: saved.map((p) => p.id) });
      update(await call("saveWorkspace", { name: new FormData(form).get("name"), layout, profileIds: saved.map((p) => p.profile.id), tabNames: saved.map((p) => p.title.textContent), paneKinds: saved.map((p) => p.fileWorkspace ? "sftp" : "session"), windowPlacements, activePaneIndex: saved.findIndex((p) => p.id === activeId) < 0 ? null : saved.findIndex((p) => p.id === activeId), splitPaneIndices: splitPaneIds.slice(0, splitSlotCount()).map((id) => saved.findIndex((p) => p.id === id)), splitRatios }));
      modal.close();
      toast(uiText("Workspace salvato, comprese finestre e disposizione."));
    } catch {
    } finally {
      save.disabled = false;
    }
  };
  body.append(form);
}
async function tunnelsDialog() {
  const pane = panes.get(activeId);
  if (!pane?.connected || pane.desktop || pane.fileWorkspace || pane.storageWorkspace || pane.editor) return toast(uiText("Seleziona una sessione SSH connessa."), true);
  const sid = activeId;
  const body = showModal(uiText("Tunnel SSH"), "MAPPA / " + pane.profile.name, null, true);
  modal.classList.add("workflow-modal");
  let rules = await call("tunnelStatus", { sessionId: sid });
  const list = document.createElement("div");
  body.append(list);
  const editor = document.createElement("section");
  body.append(editor);
  function render() {
    pane.profile.tunnels = rules.map((x) => x.rule);
    const saved = state.data.profiles.find((p) => p.id === pane.profile.id);
    if (saved) saved.tunnels = pane.profile.tunnels;
    list.replaceChildren();
    if (!rules.length) notice(list, uiText("Nessuna regola. Crea un tunnel locale, remoto o SOCKS."));
    for (const item of rules) {
      const card = document.createElement("section");
      card.className = "tunnel-card";
      const heading = document.createElement("h3");
      heading.textContent = item.rule.kind.toUpperCase() + " · " + (item.active ? "ATTIVO" : "DISATTIVATO") + (item.rule.automatic ? " · AUTOMATICO" : "");
      const map = document.createElement("div");
      map.className = "tunnel-map";
      const source = document.createElement("span"), via = document.createElement("span"), target = document.createElement("span");
      source.textContent = (item.rule.kind === "remote" ? "Server" : "Questo PC") + " / 127.0.0.1:" + item.rule.localPort;
      via.textContent = uiText("⇄ SSH / ") + pane.profile.host + " ⇄";
      target.textContent = item.rule.kind === "socks" ? "Proxy SOCKS · destinazione dinamica" : item.rule.destination + ":" + item.rule.destinationPort;
      map.append(source, via, target);
      const actions = document.createElement("div");
      actions.className = "toolbar";
      actions.append(button(item.active ? "Disattiva" : "Attiva", async () => {
        rules = await call("toggleTunnel", { sessionId: sid, ruleId: item.rule.id, enabled: !item.active });
        render();
        edit();
      }), localizedButton("Modifica", () => edit(item.rule)), localizedButton("Elimina", async () => {
        rules = await call("deleteTunnel", { sessionId: sid, ruleId: item.rule.id });
        render();
        edit();
      }, "danger"));
      card.append(heading, map, actions);
      list.append(card);
    }
  }
  function edit(rule) {
    editor.replaceChildren();
    const title = document.createElement("h3");
    title.textContent = rule ? "Modifica regola" : "Nuova regola";
    editor.append(title);
    if (rule && rules.find((x) => x.rule.id === rule.id)?.active) {
      notice(editor, uiText("Disattiva il tunnel prima di modificarlo."));
      return;
    }
    const form = document.createElement("form");
    form.className = "form-grid";
    const kind = field(form, uiText("Tipo"), "kind", rule?.kind || "local", "text", [["local", "Locale"], ["remote", "Remoto"], ["socks", "SOCKS"]]);
    const port = field(form, uiText("Porta di ascolto"), "localPort", rule?.localPort || 8080, "number");
    port.min = 1;
    port.max = 65535;
    port.required = true;
    const dest = field(form, uiText("Destinazione"), "destination", rule?.destination || "127.0.0.1");
    const destPort = field(form, uiText("Porta destinazione"), "destinationPort", rule?.destinationPort || 80, "number");
    destPort.min = 1;
    destPort.max = 65535;
    function type() {
      dest.disabled = destPort.disabled = kind.value === "socks";
    }
    kind.onchange = type;
    type();
    const automatic = document.createElement("label");
    automatic.className = "check-label wide";
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.name = "automatic";
    checkbox.checked = !!rule?.automatic;
    automatic.append(checkbox, " Attiva automaticamente alla prossima connessione");
    form.append(automatic);
    const save = document.createElement("button");
    save.className = "primary";
    save.textContent = rule ? "Salva modifiche" : "Aggiungi regola";
    form.append(save);
    form.onsubmit = async (e) => {
      e.preventDefault();
      save.disabled = true;
      try {
        const values = Object.fromEntries(new FormData(form));
        rules = await call("saveTunnel", { sessionId: sid, rule: { id: rule?.id || crypto.randomUUID(), kind: values.kind, localPort: Number(values.localPort), destination: values.destination || "", destinationPort: values.kind === "socks" ? 0 : Number(values.destinationPort), automatic: !!values.automatic } });
        pane.profile.tunnels = rules.map((x) => x.rule);
        render();
        edit();
      } catch {
        save.disabled = false;
      }
    };
    editor.append(form);
  }
  render();
  edit();
}
const capabilities = [
  [
    "Profili, cartelle, schede e split",
    true
  ],
  [
    "SSH interattivo e SFTP in tab dedicate",
    true
  ],
  [
    "Deposito chiavi cifrato e verifica host",
    true
  ],
  [
    "Tunnel locali / remoti / SOCKS",
    true
  ],
  [
    "Snippet con variabili e workspace",
    true
  ],
  [
    "Ricerca snippet e scorciatoie con anteprima",
    true
  ],
  [
    "Macro di avvio per SSH integrato, OpenSSH incluso e shell locali",
    true
  ],
  [
    "Sequenze di avvio e chiusura",
    true
  ],
  [
    "Controllo aggiornamenti GitHub",
    true
  ],
  [
    "RDP/VNC dentro le schede",
    true
  ],
  [
    "Password cifrate per connessione",
    true
  ],
  [
    "Agent OpenSSH/Pageant e identità hardware compatibili",
    true
  ],
  [
    "Certificati SSH / CA locale / scadenza",
    true
  ],
  [
    "Jump host concatenati (fino a tre)",
    true
  ],
  [
    "Riconnessione manuale/automatica opzionale e tmux",
    true
  ],
  [
    "Subsystem SFTP personalizzato",
    true
  ],
  [
    "Coda parallela e ripresa verificata",
    true
  ],
  [
    "Editor remoto con controllo conflitti",
    true
  ],
  [
    "Follow Directory via OSC 7 / integrazione Bash su richiesta",
    true
  ],
  [
    "Conversione guidata PPK/OpenSSH con PuTTYgen e verifica impronta",
    true
  ],
  [
    "Generazione -sk e firma ibrida con OpenSSH incluso",
    true
  ],
  [
    "Ed448 e ML-DSA-87 come firme SSH",
    false
  ],
  [
    "CA esterna e rinnovo con provider configurato",
    true
  ],
  [
    "RD Gateway e opzioni multimonitor",
    true
  ],
  [
    "Client portatili inclusi e PowerShell 7",
    true
  ],
  [
    "Multi-invio esplicito con conferma destinatari",
    true
  ],
  [
    "Registrazioni cifrate, replay e conservazione configurabile",
    true
  ],
  [
    "Drop su terminale con scelta percorso/upload SFTP",
    true
  ],
  [
    "Hook locali/remoti di connessione e disconnessione",
    true
  ],
  [
    "Import/export endpoint OpenSSH con anteprima e limiti espliciti",
    true
  ],
  [
    "Finestre separate con sessione condivisa",
    true
  ],
  [
    "Account salvati, email precompilata e dati locali separati",
    true
  ],
  [
    "Più archivi con accesso in lettura o lettura e scrittura",
    true
  ],
  [
    "Proprietà unica, revoca e trasferimento con nuova verifica 2FA",
    true
  ],
  [
    "Copie locali cifrate e modifiche offline con controllo dei conflitti",
    true
  ],
  [
    "Recupero e backup degli archivi di tutti gli account",
    true
  ],
  [
    "Windows Hello offline abilitato manualmente per account",
    true
  ],
  [
    "Centro assistenza e segnalazioni private",
    true
  ],
  [
    "Codici di recupero da 64 caratteri ed esportazione PDF",
    true
  ],
  [
    "Esportazione in chiaro con 2FA e codice email",
    true
  ],
  [
    "Cancellazione dell’account con periodo di ripensamento",
    true
  ],
  [
    "Verifica dell’integrità delle copie cifrate",
    true
  ],
  [
    "Richieste di accesso agli archivi con approvazione del titolare",
    true
  ],
  [
    "Discussioni cifrate sugli elementi condivisi",
    true
  ],
  [
    "Esplorazione dello spazio di archivi e allegati",
    true
  ],
  [
    "Controlli aggiornamenti automatici e livelli di sicurezza",
    true
  ],
  [
    "Modalità presentazione e risparmio risorse",
    true
  ],
  [
    "Trasferimenti tra server con verifica e ripresa",
    true
  ],
  [
    "Installazione guidata della chiave pubblica SSH",
    true
  ],
  [
    "Interfaccia in italiano, inglese, francese, tedesco e spagnolo",
    true
  ],
  [
    "Centro dati e privacy con pulizia locale controllata",
    true
  ],
  [
    "Revisioni periodiche degli accessi e rapporti dei permessi",
    true
  ],
  [
    "Home personale e viste salvate",
    true
  ],
  [
    "Anteprima protetta degli allegati dei ticket",
    true
  ],
  [
    "Sincronizzazione selettiva di lingua e audio",
    true
  ],
  [
    "Centro operazioni con progressi e annullamento",
    true
  ],
  [
    "Ricerca dei file remoti con limiti e annullamento",
    true
  ],
  [
    "Visualizzatore dei log remoti",
    true
  ],
  [
    "Topologia delle connessioni e dei tunnel",
    true
  ],
  [
    "Modelli e verifica guidata delle connessioni",
    true
  ]
];
function settingsDialog(section = "general") {
  renderSettingsDialog(section);
}
function renderUpdateProgress(p) {
  const status = $("update-progress"), meter = $("update-meter"), details = $("update-download-details");
  if (!status) return;
  status.textContent = p.status || "";
  if (!meter) return;
  meter.hidden = !p.stage;
  details.textContent = "";
  if (p.stage === "download") {
    if (p.totalBytes > 0) {
      meter.value = Math.min(100, p.receivedBytes / p.totalBytes * 100);
      details.textContent = Math.floor(meter.value) + "% · " + updateBytes(p.receivedBytes) + " / " + updateBytes(p.totalBytes);
    } else {
      meter.removeAttribute("value");
      details.textContent = updateBytes(p.receivedBytes) + " scaricati";
    }
    if (p.bytesPerSecond > 0) details.textContent += " · " + updateBytes(p.bytesPerSecond) + "/s";
    details.textContent += Number.isFinite(p.remainingSeconds) ? " · circa " + updateTime(p.remainingSeconds) + " al termine del download" : " · calcolo del tempo rimanente…";
  } else if (p.stage) {
    meter.value = 100;
    details.textContent = p.stage === "verify" ? "Download completato. Verifica di integrità e firma prima dell’installazione." : p.stage === "ready" ? "Pacchetto verificato. Conferma l’installazione nella finestra aperta." : "Preparazione dell’installer. La console si chiuderà senza outro.";
  }
}
function updateBytes(n) {
  return n >= 1048576 ? (n / 1048576).toFixed(1) + " MiB" : (n / 1024).toFixed(1) + " KiB";
}
function updateTime(s) {
  s = Math.ceil(s);
  return s < 60 ? s + " s" : Math.floor(s / 60) + " min " + s % 60 + " s";
}
async function updatesDialog() {
  const required = window.relayAvailableUpdate?.required === true;
  const body = showModal(uiText("Aggiornamenti"), "GITHUB RELEASES / CANALE STABILE", null, true);
  modal.classList.add("updates-modal");
  if (required) {
    notice(body, uiText("Aggiornamento di sicurezza critico richiesto. La console resta bloccata finché non installi una versione sicura. Profili e dati locali vengono conservati. Puoi riprovare il controllo o uscire da Relay."));
    body.append(localizedButton("Esci da Relay", () => call("applicationExit")));
  }
  const controls = document.createElement("section");
  controls.className = "update-control";
  const releasePanel = document.createElement("section");
  releasePanel.className = "update-release";
  body.append(controls, releasePanel);
  const heading = document.createElement("h3");
  heading.textContent = uiText("La tua installazione");
  controls.append(heading);
  notice(controls, uiText("Versione installata ") + state.version + ". Profili, archivio, Windows Hello e impostazioni vengono conservati. Prima dell’installazione viene creata una copia dei dati locali.");
  const credential = document.createElement("section");
  credential.className = "update-credential";
  const tokenState = document.createElement("p");
  tokenState.id = "update-token-state";
  credential.append(tokenState);
  const toolbar = document.createElement("div");
  toolbar.className = "toolbar";
  const save = localizedButton("Salva token GitHub", async () => {
    const result = await call("updateTokenSave");
    renderToken(result.saved);
    releasePanel.replaceChildren();
    renderUpdateProgress({ status: "" });
  });
  const remove = localizedButton("Rimuovi token", async () => {
    const result = await call("updateTokenDelete");
    renderToken(result.saved);
    releasePanel.replaceChildren();
    renderUpdateProgress({ status: "" });
  }, "danger");
  toolbar.append(save, remove);
  credential.append(toolbar);
  controls.append(credential);
  notice(credential, uiText("Token cifrato per il tuo account Windows. Usa Contents: Read-only per sup3r93/unsc-relay-desktop."));
  function renderToken(saved) {
    tokenState.textContent = saved ? "Token salvato e protetto su questo PC." : "Nessun token salvato. Gli aggiornamenti pubblici non richiedono un token GitHub.";
    save.textContent = saved ? "Sostituisci token GitHub" : "Salva token GitHub";
    remove.hidden = !saved;
  }
  const check = localizedButton("Controlla aggiornamenti", async () => {
    check.disabled = true;
    install.hidden = true;
    releasePanel.replaceChildren();
    renderUpdateProgress({ status: "Verifica delle release stabili…" });
    try {
      const release = await call("checkUpdates");
      if (!release) {
        renderUpdateProgress({ status: "Stai usando la versione più recente." });
        return;
      }
      renderUpdateProgress({ status: "Disponibile " + release.version });
      const title = document.createElement("h3");
      title.textContent = uiText("Novità di ") + release.version;
      const classification = document.createElement("p");
      const names = { interface: "Grafica e accessibilità", features: "Nuove funzionalità", security: "Sicurezza" };
      classification.textContent = release.policy ? release.policy.categories.map((c) => names[c] || c).join(" · ") + " — " + { normal: "Aggiornamento ordinario", important: "Sicurezza importante", critical: "Sicurezza critica" }[release.policy.security] : "Versione precedente ai metadati di classificazione firmati";
      releasePanel.append(classification);
      const notes = document.createElement("pre");
      notes.textContent = release.notes;
      releasePanel.append(title, notes);
      install.hidden = false;
    } catch {
      renderUpdateProgress({ status: "Verifica non completata. Controlla token e connessione." });
    } finally {
      check.disabled = false;
    }
  }, "primary");
  const progress = document.createElement("p");
  progress.id = "update-progress";
  progress.setAttribute("role", "status");
  const meter = document.createElement("progress");
  meter.id = "update-meter";
  meter.className = "update-meter";
  meter.max = 100;
  meter.hidden = true;
  interfaceAttribute(meter, 'aria-label', "Avanzamento download aggiornamento");
  const details = document.createElement("div");
  details.id = "update-download-details";
  details.className = "update-download-details";
  const install = localizedButton("Scarica e installa", async () => {
    install.disabled = true;
    check.disabled = true;
    save.disabled = true;
    remove.disabled = true;
    renderUpdateProgress({ status: "Preparazione del download…", stage: "download", receivedBytes: 0 });
    try {
      await call("installUpdate");
      renderUpdateProgress({ status: "Operazione terminata. Puoi riprovare quando vuoi." });
    } catch (e) {
      renderUpdateProgress({ status: e.message || "Aggiornamento non completato." });
    } finally {
      install.disabled = false;
      check.disabled = false;
      save.disabled = false;
      remove.disabled = false;
    }
  }, "primary");
  install.hidden = true;
  controls.append(check, progress, meter, details, install);
  notice(releasePanel, uiText("Controlla gli aggiornamenti per leggere le novità della prossima versione."));
  renderToken((await call("updateTokenStatus")).saved);
  if (required || window.relayAvailableUpdate?.release) check.click();
}
function playSequence(kind, requestedVolume, done) {
  const overlay = $("sequence"), video = $("sequence-video"), volume = Number.isFinite(requestedVolume) ? Math.max(0, Math.min(1, requestedVolume)) : sequenceVolume(kind);
  overlay.hidden = false;
  if (kind === "startup") startStartupTheme();
  video.controls = false;
  video.disablePictureInPicture = true;
  video.muted = false;
  video.volume = volume;
  video.playbackRate = 1;
  video.src = "media/" + kind + ".mp4";
  $("sequence-poster").src = "media/" + kind + "-poster.png";
  $("sequence-label").textContent = kind === "startup" ? "UNSC RELAY / AVVIO" : "UNSC RELAY / CHIUSURA";
  let completed = false;
  const finish = (failed) => {
    if (completed) return;
    completed = true;
    video.onvolumechange = null;
    video.pause();
    if (kind === "startup") presentStartupFrame();
    overlay.hidden = true;
    done?.(failed);
  };
  video.onplaying = () => {
    if (kind === "startup") startStartupTheme();
    presentStartupFrame();
  };
  video.onended = () => finish(false);
  video.onerror = () => {
    presentStartupFrame();
    toast(uiText("Riproduzione della sequenza non disponibile."), true);
    finish(true);
  };
  video.onvolumechange = () => {
    if (!completed) {
      if (video.muted) video.muted = false;
      if (video.volume !== volume) video.volume = volume;
    }
  };
  video.play().catch(() => {
    presentStartupFrame();
    if (completed) return;
    const play = localizedButton("Avvia riproduzione con audio", () => {
      if (kind === "startup") {
        startStartupTheme();
        startupTheme?.play().catch(() => {
        });
      }
      video.play();
    });
    play.className = "sequence-play";
    $("sequence-label").replaceChildren(play);
    play.focus();
  });
}
$("sequence").addEventListener("contextmenu", (e) => e.preventDefault());
$("sequence").addEventListener("pointerdown", (e) => {
  if (!e.target.closest(".sequence-play")) e.preventDefault();
});
document.addEventListener("keydown", (e) => {
  if (!$("sequence").hidden && !e.target.closest(".sequence-play")) {
    e.preventDefault();
    e.stopImmediatePropagation();
  }
}, true);
$("profile-search").oninput = renderProfiles;
$("new-profile").onclick = $("welcome-new").onclick = () => profileDialog();
$("modal-close").onclick = () => {
  if (window.relayAvailableUpdate?.required) toast(uiText("Installa l’aggiornamento critico oppure esci da Relay."), true);
  else modal.close();
};
modal.addEventListener("cancel", (e) => {
  if (window.relayAvailableUpdate?.required) e.preventDefault();
});
$("new-folder").onclick = () => folderDialog();
for (const [id, fn] of [["keys-btn", keysDialog], ["hosts-btn", hostsDialog], ["snippets-btn", snippetsDialog], ["settings-btn", settingsDialog], ["updates-btn", updatesDialog], ["palette-btn", palette], ["save-workspace", saveWorkspace], ["tunnels-btn", tunnelsDialog]]) $(id).onclick = () => Promise.resolve(fn()).catch(() => {
});
$("split-h").onclick = () => setSplitLayout(layout === "columns" ? "tabs" : "columns");
$("split-v").onclick = () => setSplitLayout(layout === "rows" ? "tabs" : "rows");
$("files-btn").onclick = () => toggleFiles().catch(() => {
});
$("remote-form").onsubmit = (e) => {
  e.preventDefault();
  listFiles();
};
$("refresh-files").onclick = listFiles;
$("upload-btn").onclick = () => call("transfer", { sessionId: activeId, direction: "upload", remote: $("remote-path").value }).then(listFiles).catch(() => {
});
addEventListener("resize", () => refreshStage());
document.addEventListener("keydown", (e) => {
  if (document.body.classList.contains("startup-covered") || state?.appLocked || state?.account?.requiresLink) return;
  if (e.ctrlKey && e.key.toLowerCase() === "k") {
    e.preventDefault();
    e.stopImmediatePropagation();
    palette();
  }
  if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === "f") {
    e.preventDefault();
    const query = prompt(uiText("Cerca nel terminale"));
    if (query) panes.get(activeId)?.search.findNext(query);
  }
}, true);
function applyResourceSaving() {
  const saving = !!state?.data.settings.resourceSaving;
  document.documentElement.classList.toggle("resource-saving", saving);
  for (const pane of panes.values()) {
    if (pane.term?.options) pane.term.options.cursorBlink = !saving;
    if (!saving) pane.outputBatch?.flush();
  }
}
setInterval(() => {
  if (!document.hidden || !state?.data.settings.resourceSaving) $("clock").textContent = (/* @__PURE__ */ new Date()).toLocaleTimeString(document.documentElement.lang);
}, 1e3);
async function boot() {
  try {
    update(await rpc("bootstrap"));
    if (state.diagnostic) window.chrome.webview.postMessage({ id: crypto.randomUUID(), action: "diagnosticReady", payload: {} });
    if (!state.diagnostic) {
      await rpc("startupStarted");
      playSequence("startup", sequenceVolume("startup"), async (failed) => {
        try {
          await rpc("startupFinished", { failed });
          await finishApplicationStartup();
        } catch (e) {
          toast(e.message, true);
        }
      });
    } else {
      $("startup-curtain").hidden = true;
      document.body.classList.remove("startup-covered");
      startupVaultPrompt();
    }
  } catch (e) {
    toast(e.message, true);
    state = { data: { profiles: [], snippets: [], workspaces: [], settings: { startupVideo: false, shutdownVideo: true, startupVolume: 0.8, shutdownVolume: 1, autoLockMinutes: 10 } }, vault: { unlocked: false, exists: false, keys: [] }, hostKeys: [], version: "0.1.0" };
    renderProfiles();
  }
}
$("cancel-transfer").onclick = () => call("cancelTransfer").catch(() => {
});
