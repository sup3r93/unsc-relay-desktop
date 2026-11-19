"use strict";
let fileQueue = [], fileClipboard = null;
const externalEdits = /* @__PURE__ */ new Map();
function externalEditorSelected() {
  return !!state.data.settings.externalEditor || state.data.settings.editorPreference === "notepad";
}
function renderExternalEdits(owner) {
  if (!owner.externalEditStatus) {
    const box2 = document.createElement("section");
    box2.className = "external-edit-status";
    interfaceAttribute(box2, 'aria-label', "Salvataggi editor esterno");
    owner.surface.prepend(box2);
    owner.externalEditStatus = box2;
  }
  const box = owner.externalEditStatus;
  box.replaceChildren();
  const edits = [...externalEdits.values()].filter((e) => e.owner === owner.id);
  box.hidden = !edits.length;
  for (const edit of edits) {
    const row = document.createElement("div");
    row.className = "external-edit-row";
    const label = document.createElement("span");
    label.setAttribute("role", "status");
    label.textContent = edit.path + " · " + edit.status;
    row.append(label);
    if (edit.error) row.append(localizedButton("Riprova caricamento", async () => {
      await call("editorPublish", { id: edit.id });
      await call("editorAuto", { id: edit.id, enabled: true });
      edit.error = false;
      edit.status = "Salvataggio caricato · sincronizzazione automatica attiva";
      renderExternalEdits(owner);
    }));
    row.append(localizedButton("Interrompi collegamento", async () => {
      if (!await call("confirmAction", { text: "Interrompere i caricamenti di " + edit.path + "? La copia locale resta disponibile nell’editor esterno." })) return;
      await call("editorClose", { id: edit.id });
      externalEdits.delete(edit.id);
      renderExternalEdits(owner);
    }));
    box.append(row);
  }
}
function forgetExternalEdits(ownerId) {
  for (const [id, edit] of externalEdits) if (edit.owner === ownerId) externalEdits.delete(id);
}
function toolPane(profile, label, kind, id = crypto.randomUUID()) {
  const container = document.createElement("section");
  container.className = "terminal-pane " + kind + "-pane";
  container.dataset.sessionId = id;
  const header = document.createElement("div");
  header.className = "pane-header";
  const name = document.createElement("span");
  name.textContent = label;
  const status = document.createElement("span");
  status.className = "pane-status";
  header.append(name, status);
  const surface = document.createElement("div");
  surface.className = kind + "-workspace";
  container.append(header, surface);
  $("terminals").append(container);
  const tab = document.createElement("button");
  tab.className = "tab";
  tab.setAttribute("role", "tab");
  const title = document.createElement("span");
  title.textContent = label;
  const close = document.createElement("span");
  close.className = "tab-close";
  close.textContent = "×";
  tab.append(title, close);
  tab.onclick = (e) => {
    if (e.target === close) closePane(id);
    else {
      activeId = id;
      refreshStage();
    }
  };
  $("tabs").append(tab);
  const pane = { id, profile, container, tab, title, name, status, surface, term: { cols: 100, rows: 30, dispose() {
  }, focus() {
  }, writeln() {
  }, write() {
  } }, fit: { fit() {
  } }, search: { findNext() {
  } }, reconnect: localizedButton("Riconnetti", () => {
  }), connected: true, files: false, connecting: false };
  container.onpointerdown = () => {
    activeId = id;
    refreshStage();
  };
  wireTab(pane);
  panes.set(id, pane);
  activeId = id;
  refreshStage();
  return pane;
}
function filePath(dir, name, local = false) {
  if (!name || name === "." || name === ".." || /[\\/\x00-\x1f]/.test(name) || local && /[:*?"<>|]/.test(name)) throw Error("Nome file non valido per la destinazione.");
  return dir.replace(/[\\/]$/, "") + (local ? "\\" : "/") + name;
}
async function openFileWorkspace(profile, source = null) {
  if (profile.protocol === "native") {
    source = source || [...panes.values()].find((p) => p.local && p.connected && p.profile.id === profile.id);
    if (!source) return toast(uiText("Apri prima il terminale OpenSSH di questo profilo."), true);
  }
  const prior = [...panes.values()].find((p) => p.fileWorkspace && p.profile.id === profile.id);
  if (prior) {
    if (source && !prior.connected && prior.nativeSourceId !== source.id) {
      await call("nativeFilesOpen", { profileId: profile.id, sessionId: prior.id, sourceSessionId: source.id });
      prior.nativeSourceId = source.id;
      prior.setFileConnection(true);
      prior.reconnect.hidden = true;
      await prior.refreshFiles(true);
    }
    activeId = prior.id;
    refreshStage();
    return prior;
  }
  if (!await ensureConnectionVault(profile)) return null;
  const pane = toolPane(profile, "SFTP · " + profile.name, "file");
  pane.fileWorkspace = true;
  pane.nativeSourceId = source?.id;
  pane.term.dispose = () => clearTimeout(pane.followTimer);
  pane.connected = false;
  pane.connecting = true;
  pane.status.textContent = uiText("Connessione SFTP…");
  const remembered = state.data.fileLocations?.[profile.id];
  const useLast = profile.rememberFileDirectories !== false && remembered && remembered.host.toLowerCase() === profile.host.toLowerCase() && remembered.port === profile.port && remembered.username === profile.username;
  pane.localPath = useLast ? remembered.local : profile.localDirectory || "";
  pane.remotePath = useLast ? remembered.remote : profile.remoteDirectory || ".";
  pane.selection = { local: /* @__PURE__ */ new Set(), remote: /* @__PURE__ */ new Set() };
  pane.entries = { local: [], remote: [] };
  pane.paths = {};
  pane.lists = {};
  const toolbar = document.createElement("div");
  toolbar.className = "file-workspace-toolbar";
  const policy = field(toolbar, uiText("File esistente"), "conflict", "ask", "text", [["ask", "Chiedi / blocca conflitto"], ["replace", "Sostituisci"], ["skip", "Salta"], ["new", "Non sovrascrivere"]]);
  const permissions = field(toolbar, uiText("Permessi dopo upload (opzionali)"), "permissions", "");
  permissions.placeholder = "es. 640";
  permissions.maxLength = 4;
  pane.policy = policy;
  pane.permissions = permissions;
  const parallel = field(toolbar, uiText("Trasferimenti paralleli"), "parallelism", "2", "text", [["1", "1"], ["2", "2"], ["3", "3"], ["4", "4"]]);
  parallel.onchange = async () => {
    try {
      await call("fileParallelism", { value: Number(parallel.value) });
      document.querySelectorAll("[name=parallelism]").forEach((x) => x.value = parallel.value);
    } catch {
    }
  };
  const bandwidth = field(toolbar, uiText("Limite KiB/s per file · 0 illimitato"), "bandwidthKiB", "0", "number");
  bandwidth.min = 0;
  bandwidth.max = 1048576;
  pane.bandwidth = bandwidth;
  const start = field(toolbar, uiText("Fascia oraria · inizio locale (facoltativo)"), "windowStart", "", "time"), end = field(toolbar, uiText("Fascia oraria · fine locale"), "windowEnd", "", "time");
  pane.transferWindowStart = start;
  pane.transferWindowEnd = end;
  toolbar.append(localizedButton("↑ Carica selezionati", () => enqueueSelection(pane, "local")), localizedButton("↓ Scarica selezionati", () => enqueueSelection(pane, "remote")), localizedButton("↻ Aggiorna", () => pane.refreshFiles()), localizedButton("Confronta cartelle", () => folderSyncDialog(pane)), localizedButton("Ricerca remota dei file", () => remoteSearchDialog(pane)), localizedButton("Trascina verso Esplora file", () => prepareExplorerFiles(pane)));
  const advanced = document.createElement("details");
  advanced.className = "transfer-options";
  const summary = document.createElement("summary");
  summary.textContent = uiText("Opzioni trasferimento");
  const options = document.createElement("div");
  options.className = "transfer-option-fields";
  options.append(permissions.parentElement, parallel.parentElement, bandwidth.parentElement, start.parentElement, end.parentElement);
  options.append(localizedButton("Installa chiave pubblica", () => publicKeyInstallationDialog(pane)));
  options.append(localizedButton("Copia su altro server", () => remoteBridgeDialog(pane)));
  options.append(localizedButton("Informazioni del server", () => serverInfoDialog(pane)), localizedButton("Verifica caricamento", () => transferPreflightDialog(pane, "local")), localizedButton("Verifica download", () => transferPreflightDialog(pane, "remote")));
  advanced.append(summary, options);
  toolbar.append(advanced);
  pane.surface.append(toolbar);
  pane.setFileConnection = (value) => {
    pane.connected = value;
    pane.files = value;
    for (const control of toolbar.querySelectorAll("button")) control.disabled = !value;
  };
  const followTools = document.createElement("div");
  followTools.className = "file-follow-toolbar";
  setupFollowDirectory(pane, followTools);
  pane.surface.append(followTools);
  const columns = document.createElement("div");
  columns.className = "file-columns";
  pane.surface.append(columns);
  for (const side of ["local", "remote"]) {
    const column = document.createElement("section");
    column.className = "file-column";
    column.dataset.fileSide = side;
    column.dataset.filePane = pane.id;
    const heading = document.createElement("div");
    heading.className = "file-column-heading";
    heading.textContent = side === "local" ? "FILE LOCALI" : "FILE REMOTI";
    const form = document.createElement("form");
    form.className = "file-path-form";
    const path = document.createElement("input");
    path.setAttribute("aria-label", side === "local" ? "Cartella locale" : "Cartella remota");
    path.value = side === "local" ? "" : pane.remotePath;
    pane.paths[side] = path;
    const go = document.createElement("button");
    go.textContent = "→";
    form.append(path, go);
    form.onsubmit = (e) => {
      e.preventDefault();
      loadFileSide(pane, side, path.value);
    };
    if (side === "local") heading.append(localizedButton("Scegli cartella", async () => {
      const data = await call("localChoose");
      if (data) renderLocal(pane, data);
    }));
    else heading.append(localizedButton("Nuova cartella", () => fileNameDialog(pane, "mkdir")));
    const filter = document.createElement("input");
    filter.type = "search";
    interfaceAttribute(filter, 'placeholder', "Filtra per nome…");
    filter.setAttribute("aria-label", side === "local" ? "Filtra file locali" : "Filtra file remoti");
    filter.oninput = () => renderFileRows(pane, side, filter.value);
    pane[side + "Filter"] = filter;
    const list = document.createElement("div");
    list.className = "workspace-file-list";
    list.setAttribute("role", "listbox");
    list.setAttribute("aria-multiselectable", "true");
    list.tabIndex = 0;
    list.setAttribute("aria-label", side === "local" ? "File locali" : "File remoti");
    pane.lists[side] = list;
    column.append(heading, form, filter, list);
    columns.append(column);
    setupFileNavigation(pane, side, column, heading, form);
    list.ondragover = (e) => {
      if ([...e.dataTransfer.types].includes("application/x-relay-file") || side === "remote" && [...e.dataTransfer.types].includes("Files")) {
        e.preventDefault();
        list.classList.add("drop-target");
      }
    };
    list.ondragleave = () => list.classList.remove("drop-target");
    list.ondrop = async (e) => {
      e.preventDefault();
      list.classList.remove("drop-target");
      try {
        if (side === "remote" && e.dataTransfer.files.length) {
          const paths = await droppedPaths([...e.dataTransfer.files]);
          return enqueuePaths(pane, "local", paths, true);
        }
        const source2 = JSON.parse(e.dataTransfer.getData("application/x-relay-file"));
        if (source2.pane !== pane.id || source2.side === side) return;
        await enqueueSelection(pane, source2.side);
      } catch (error) {
        toast(error.message, true);
      }
    };
    list.onkeydown = (e) => {
      if (["ArrowDown", "ArrowUp", "Home", "End"].includes(e.key) && !e.altKey) {
        e.preventDefault();
        const rows = [...list.querySelectorAll(".workspace-file-row")];
        const index = rows.indexOf(e.target.closest(".workspace-file-row"));
        const next = e.key === "Home" ? 0 : e.key === "End" ? rows.length - 1 : Math.max(0, Math.min(rows.length - 1, index + (e.key === "ArrowDown" ? 1 : -1)));
        rows[next]?.focus();
        if (e.shiftKey && rows[next]) {
          const file = pane.entries[side].find((f) => f.path === rows[next].dataset.filePath);
          fileSelection(pane, side, file, e);
        }
      }
      if (e.ctrlKey && e.key === "a") {
        e.preventDefault();
        pane.selection[side] = new Set([...list.querySelectorAll(".workspace-file-row")].map((row) => row.dataset.filePath));
        renderFileRows(pane, side, filter.value);
      }
      if (e.ctrlKey && e.key === "c") {
        e.preventDefault();
        copyFileSelection(pane, side);
      }
      if (e.ctrlKey && e.key === "v") {
        e.preventDefault();
        pasteFiles(pane, side);
      }
    };
  }
  const queue = document.createElement("section");
  queue.className = "file-queue";
  const queueHeader = document.createElement("div");
  queueHeader.className = "file-queue-heading";
  queueHeader.textContent = uiText("CODA TRASFERIMENTI");
  queueHeader.append(localizedButton("Pausa tutti", async () => {
    for (const job of fileQueue.filter((j) => j.sessionId === pane.id && ["In coda", "Trasferimento", "Pausa richiesta", "Attesa connessione"].includes(j.status))) await call("filePause", { id: job.id });
  }), localizedButton("Pulisci completati", async () => {
    fileQueue = await call("fileClear");
    renderFileQueues();
  }));
  pane.queueList = document.createElement("div");
  pane.queueList.className = "file-queue-list";
  queue.append(queueHeader, pane.queueList);
  pane.surface.append(queue);
  pane.refreshFiles = async (recovery = false) => {
    const refreshed = await Promise.all([loadFileSide(pane, "local", pane.localPath, { history: recovery ? "refresh" : "push", preserveSelection: recovery }), loadFileSide(pane, "remote", pane.remotePath, { history: recovery ? "refresh" : "push", preserveSelection: recovery })]);
    if (recovery && refreshed.every(Boolean)) pane.status.textContent = uiText("SFTP ripristinato");
    return refreshed.every(Boolean);
  };
  pane.container.querySelector(".pane-header").append(pane.reconnect);
  pane.reconnect.hidden = true;
  wireAutoReconnect(pane);
  const reconnect = async () => {
    if (pane.hadConnection) return recoverProfilePanes(pane, false);
    pane.connecting = true;
    pane.reconnect.disabled = true;
    try {
      const result = await call(source ? "nativeFilesOpen" : "connect", { profileId: profile.id, sessionId: pane.id, filesOnly: true, sourceSessionId: pane.nativeSourceId });
      pane.connected = true;
      pane.hadConnection = true;
      pane.files = result.filesAvailable;
      pane.reconnect.hidden = true;
      pane.status.textContent = pane.files ? "SFTP connesso" : "SFTP non disponibile";
      await pane.refreshFiles();
      fileQueue = await call("fileQueue");
      renderFileQueues();
    } catch (e) {
      pane.status.textContent = e.message;
      pane.reconnect.hidden = false;
    } finally {
      pane.connecting = false;
      pane.reconnect.disabled = false;
    }
  };
  pane.reconnect.onclick = reconnect;
  await reconnect();
  return pane;
}
function renderLocal(pane, data, mode = "push", preserveSelection = false) {
  pane.localPath = data.path;
  pane.localParent = data.parent;
  pane.nav.local.home = data.home || pane.nav.local.home || data.path;
  pane.paths.local.value = data.path;
  pane.entries.local = data.entries;
  if (preserveSelection) pane.selection.local = new Set([...pane.selection.local].filter((path) => data.entries.some((f) => f.path === path)));
  else pane.selection.local.clear();
  renderFileRows(pane, "local", pane.localFilter.value);
  commitFileNavigation(pane, "local", data.path, mode);
}
async function loadFileSide(pane, side, path, options = {}) {
  const nav = pane.nav[side], sequence = ++nav.sequence;
  pane.status.textContent = uiText("Lettura cartella…");
  try {
    if (side === "local") {
      const data = await call("localList", { path });
      if (sequence !== nav.sequence) return false;
      renderLocal(pane, data, options.history || "push", options.preserveSelection);
    } else {
      if (!pane.files) return false;
      const canonical = await call("sftpDirectory", { sessionId: pane.id, path });
      const rows = await call("sftpList", { sessionId: pane.id, path: canonical });
      if (!nav.home) nav.home = await call("sftpHome", { sessionId: pane.id });
      if (sequence !== nav.sequence) return false;
      pane.remotePath = canonical;
      pane.paths.remote.value = canonical;
      pane.entries.remote = rows.filter((f) => f.name !== "." && f.name !== "..");
      if (options.preserveSelection) pane.selection.remote = new Set([...pane.selection.remote].filter((path2) => pane.entries.remote.some((f) => f.path === path2)));
      else pane.selection.remote.clear();
      renderFileRows(pane, "remote", pane.remoteFilter.value);
      commitFileNavigation(pane, "remote", canonical, options.history || "push");
    }
    return true;
  } catch (e) {
    if (sequence === nav.sequence) pane.status.textContent = e.message;
    return false;
  }
}
function renderFileRows(pane, side, filter = "") {
  const list = pane.lists[side];
  list.replaceChildren();
  list.append(renderFileHeader(pane, side));
  const groups = groupedFiles(pane, side, filter);
  let rowIndex = 0;
  for (const [group, entries] of groups) {
    if (group) {
      const groupButton = button("", () => {
        const collapsed = pane.nav[side].collapsed ??= /* @__PURE__ */ new Set();
        collapsed.has(group) ? collapsed.delete(group) : collapsed.add(group);
        renderFileRows(pane, side, filter);
      });
      groupButton.className = "file-group";
      groupButton.textContent = (pane.nav[side].collapsed?.has(group) ? "▸ " : "▾ ") + group + " (" + entries.length + ")";
      groupButton.setAttribute("aria-expanded", String(!pane.nav[side].collapsed?.has(group)));
      list.append(groupButton);
      if (pane.nav[side].collapsed?.has(group)) continue;
    }
    for (const file of entries) {
      const row = document.createElement("div");
      row.className = "workspace-file-row";
      row.id = pane.id + "-" + side + "-" + rowIndex++;
      row.tabIndex = 0;
      row.setAttribute("role", "option");
      row.setAttribute("aria-selected", String(pane.selection[side].has(file.path)));
      row.title = file.path;
      row.dataset.filePath = file.path;
      row.dataset.fileSide = side;
      row.dataset.filePane = pane.id;
      renderFileCells(row, file, side);
      row.onclick = (e) => fileSelection(pane, side, file, e);
      row.ondblclick = () => file.directory ? loadFileSide(pane, side, file.path) : side === "remote" ? openEditorPane(pane, file, true) : enqueueSelection(pane, "local", [file]);
      row.onkeydown = (e) => {
        if (e.key === "Enter") {
          e.preventDefault();
          row.ondblclick();
        }
        if (e.key === " ") {
          e.preventDefault();
          fileSelection(pane, side, file, { ctrlKey: true, shiftKey: e.shiftKey });
        }
      };
      row.draggable = true;
      row.ondragstart = (e) => {
        if (!pane.selection[side].has(file.path)) {
          pane.selection[side].clear();
          pane.selection[side].add(file.path);
        }
        e.dataTransfer.setData("application/x-relay-file", JSON.stringify({ pane: pane.id, side }));
        e.dataTransfer.effectAllowed = "copy";
      };
      list.append(row);
    }
  }
  if (!groups.length) {
    const empty = document.createElement("p");
    empty.className = "empty";
    empty.textContent = uiText("Nessun file in questa cartella.");
    list.append(empty);
  }
}
function selectedFiles(pane, side) {
  return pane.entries[side].filter((f) => pane.selection[side].has(f.path));
}
async function droppedPaths(files) {
  if (!native || !window.chrome.webview.postMessageWithAdditionalObjects) throw Error("Trascinamento da Windows non disponibile: usa il pannello locale.");
  const id = crypto.randomUUID();
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve: (result) => resolve(result.paths), reject });
    window.chrome.webview.postMessageWithAdditionalObjects({ id, action: "dropPaths", payload: {} }, files);
  });
}
async function enqueueSelection(pane, side, files = selectedFiles(pane, side)) {
  return enqueuePaths(pane, side, files.map((f) => f.path));
}
async function enqueuePaths(pane, side, paths, external = false) {
  try {
    if (!pane.files) return toast(uiText("SFTP non disponibile."), true);
    if (!paths.length) return toast(uiText("Seleziona uno o più file o cartelle."), true);
    const plan = await call("filePlan", { sessionId: pane.id, direction: side === "local" ? "upload" : "download", paths, localDirectory: pane.localPath, remoteDirectory: pane.remotePath });
    const items = plan.items;
    if (plan.skippedLinks) toast(plan.skippedLinks + " collegamenti simbolici saltati");
    if (!items.length) return;
    if (external && !await call("confirmAction", { text: "Caricare " + items.length + " elementi trascinati da Windows in " + pane.remotePath + " sul server " + pane.profile.host + "?" })) return;
    let allChoice = null;
    const decided = [];
    for (const item of items) {
      let conflict = pane.policy.value;
      if (item.target && !item.directory && conflict === "ask") {
        const choice = allChoice || await fileConflictDialog(item, side, pane);
        if (!choice) return;
        if (choice.applyAll && choice.action !== "rename") allChoice = choice;
        conflict = choice.action;
        if (conflict === "rename") {
          if (side === "local") item.remote = choice.destination;
          else item.local = choice.destination;
          conflict = "new";
        }
      }
      decided.push({ ...item, conflict });
    }
    const minutes = (value) => value ? Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5)) : null;
    const windowStartMinute = minutes(pane.transferWindowStart.value), windowEndMinute = minutes(pane.transferWindowEnd.value);
    if (windowStartMinute === null !== (windowEndMinute === null) || windowStartMinute !== null && windowStartMinute === windowEndMinute) {
      toast(uiText("Indica inizio e fine diversi per la fascia oraria."), true);
      return;
    }
    fileQueue = await call("fileEnqueue", { sessionId: pane.id, direction: side === "local" ? "upload" : "download", items: decided, conflict: pane.policy.value, permissions: pane.permissions.value.trim(), bandwidthKiB: Number(pane.bandwidth.value) || 0, windowStartMinute, windowEndMinute });
    renderFileQueues();
  } catch (e) {
    toast(e.message, true);
  }
}
function fileConflictDialog(item, side, pane) {
  return new Promise((resolve) => {
    const body = showModal(uiText("Il file esiste già a destinazione"), "CONFLITTO / " + pane.profile.name);
    modal.classList.add("conflict-modal");
    let answered = false;
    const finish = (value) => {
      if (answered) return;
      answered = true;
      modal.removeEventListener("close", cancel);
      modal.close();
      resolve(value);
    };
    const cancel = () => finish(null);
    modal.addEventListener("close", cancel, { once: true });
    const grid = document.createElement("div");
    grid.className = "conflict-comparison";
    for (const [label, stamp, path] of [[side === "local" ? "Sorgente · file locale" : "Sorgente · file remoto", item.source, side === "local" ? item.local : item.remote], [side === "local" ? "Destinazione · file remoto" : "Destinazione · file locale", item.target, side === "local" ? item.remote : item.local]]) {
      const section = document.createElement("section");
      const title = document.createElement("h3");
      title.textContent = label;
      const location = document.createElement("p");
      location.textContent = path;
      const info = document.createElement("p");
      info.textContent = uiText("Dimensione: ") + formatSize(stamp?.size || 0) + " · Modifica: " + (stamp?.modified ? new Date(stamp.modified).toLocaleString(document.documentElement.lang) : "non disponibile");
      section.append(title, location, info);
      grid.append(section);
    }
    body.append(grid);
    const compare = localizedButton("Confronta differenze", () => showFileComparison(body, item, side, pane));
    compare.className = "compare-files-button";
    body.append(compare);
    const destination = side === "local" ? item.remote : item.local;
    const name = field(body, uiText("Nome del file a destinazione"), "conflictName", destination.split(/[\\/]/).at(-1));
    const checkLabel = document.createElement("label");
    checkLabel.className = "check-label";
    const apply = document.createElement("input");
    apply.type = "checkbox";
    checkLabel.append(apply, "Applica la stessa azione agli altri conflitti (esclusa Rinomina)");
    body.append(checkLabel);
    const actions = document.createElement("div");
    actions.className = "conflict-actions";
    const rename = localizedButton("Rinomina", () => {
      try {
        const separator = side === "local" ? "/" : "\\";
        const parent = destination.slice(0, destination.lastIndexOf(separator));
        const path = filePath(parent, name.value, side === "remote");
        if (path === destination) return toast(uiText("Scegli un nome diverso."), true);
        finish({ action: "rename", destination: path, applyAll: false });
      } catch (e) {
        toast(e.message, true);
      }
    });
    actions.append(rename, localizedButton("Riprendi", () => finish({ action: "resume", applyAll: apply.checked })), localizedButton("Sostituisci", () => finish({ action: "replace", applyAll: apply.checked }), "primary"), localizedButton("Salta", () => finish({ action: "skip", applyAll: apply.checked })), localizedButton("Annulla", () => finish(null)));
    body.append(actions);
    notice(body, uiText("Riprendi verifica tutto il contenuto già presente. Se non corrisponde alla sorgente, il file originale resta invariato."));
  });
}
function renderFileQueues() {
  for (const pane of panes.values()) if (pane.fileWorkspace) {
    pane.queueList.replaceChildren();
    pane.queueList.parentElement.classList.toggle("has-jobs", fileQueue.some((j) => j.sessionId === pane.id));
    for (const job of fileQueue.filter((j) => j.sessionId === pane.id)) {
      const row = document.createElement("div");
      row.className = "queue-row";
      const label = document.createElement("span");
      label.textContent = (job.direction === "upload" ? "↑ " : "↓ ") + job.name;
      label.title = (job.host ? job.host + ":" + job.port + " · " : "") + job.local + " ⇄ " + job.remote;
      const progress = document.createElement("progress");
      progress.max = 100;
      progress.value = job.progress;
      const status = document.createElement("span");
      status.textContent = uiText(job.status) + " " + Math.round(job.progress) + "%";
      status.title = job.error || "";
      row.append(label, progress, status);
      if (["In coda", "Trasferimento", "Pausa richiesta", "Attesa connessione"].includes(job.status)) row.append(localizedButton("Pausa", () => call("filePause", { id: job.id })));
      if (job.status === "Attesa connessione") row.append(localizedButton("Riconnetti e riprendi", () => recoverProfilePanes(pane, false)));
      if (["In pausa", "Errore"].includes(job.status)) row.append(localizedButton("Riprendi", () => call("fileResume", { id: job.id })), localizedButton("Rimuovi", () => call("fileDiscard", { id: job.id })));
      if (job.error) {
        const error = document.createElement("small");
        error.className = "queue-error";
        error.textContent = uiText(job.error);
        row.append(error);
      }
      pane.queueList.append(row);
    }
    if (!pane.queueList.children.length) pane.queueList.textContent = uiText("Seleziona i file e usa Carica / Scarica, trascinali tra i pannelli oppure usa Copia / Incolla.");
  }
}
function copyFileSelection(pane, side) {
  const files = selectedFiles(pane, side);
  if (!files.length) return;
  fileClipboard = { pane: pane.id, side, files };
  toast(files.length + " file copiati: incolla nel pannello di destinazione.");
}
async function pasteFiles(pane, side) {
  if (!fileClipboard || fileClipboard.pane !== pane.id || fileClipboard.side === side) return toast(uiText("Copia i file dal pannello opposto della stessa sessione."), true);
  await enqueueSelection(pane, fileClipboard.side, fileClipboard.files);
}
function fileNameDialog(pane, operation, file) {
  if (!["mkdir", "rename", "chmod"].includes(operation) || !pane || operation !== "mkdir" && (!file || typeof file.path !== "string")) return toast(uiText("Seleziona un file ancora disponibile prima di modificarlo."), true);
  const body = showModal(operation === "mkdir" ? "Nuova cartella remota" : operation === "chmod" ? "Permessi remoti" : "Rinomina file remoto", "SFTP / " + pane.profile.name);
  const form = document.createElement("form");
  const value = field(form, operation === "chmod" ? "Permessi ottali" : "Nome", "value", operation === "chmod" ? "640" : file?.name || "");
  const save = document.createElement("button");
  save.textContent = uiText("Applica");
  save.className = "primary";
  form.append(save);
  form.onsubmit = async (e) => {
    e.preventDefault();
    try {
      const path = operation === "mkdir" ? filePath(pane.remotePath, value.value) : file.path;
      await call("sftpOperation", { sessionId: pane.id, operation, path, value: operation === "rename" ? filePath(pane.remotePath, value.value) : value.value });
      modal.close();
      await loadFileSide(pane, "remote", pane.remotePath);
    } catch {
    }
  };
  body.append(form);
}
async function openEditorPane(owner, file, external = false) {
  try {
    external = external || externalEditorSelected() || [...externalEdits.values()].some((e) => e.owner === owner.id && e.path === file.path);
    const buffer = await call("editorOpen", { sessionId: owner.id, path: file.path });
    const existing = [...panes.values()].find((p) => p.editorId === buffer.id);
    if (external) {
      if (existing?.dirty) return toast(uiText("Salva prima le modifiche nell’editor integrato."), true);
      try {
        await call("editorExternal", { id: buffer.id });
      } catch (error) {
        if (!existing && !externalEdits.has(buffer.id)) await rpc("editorClose", { id: buffer.id }).catch(() => {
        });
        throw error;
      }
      externalEdits.set(buffer.id, { id: buffer.id, owner: owner.id, path: file.path, status: "Sincronizzazione automatica attiva", error: false });
      renderExternalEdits(owner);
      if (existing) {
        finishDetachedPane(existing);
        existing.container.remove();
        existing.tab.remove();
        panes.delete(existing.id);
      }
      activeId = owner.id;
      refreshStage();
      return;
    }
    if (existing) {
      activeId = existing.id;
      refreshStage();
      existing.editorInput.focus();
      return;
    }
    const pane = toolPane(owner.profile, "Editor · " + file.name, "editor");
    pane.editor = true;
    pane.editorId = buffer.id;
    pane.owner = owner.id;
    pane.dirty = false;
    pane.status.textContent = file.path;
    const toolbar = document.createElement("div");
    toolbar.className = "editor-toolbar";
    const input = document.createElement("textarea");
    input.className = "remote-editor";
    input.spellcheck = false;
    interfaceAttribute(input, 'aria-label', "Contenuto del file remoto");
    input.value = buffer.text;
    pane.editorInput = input;
    input.oninput = () => {
      pane.dirty = true;
      rpc("editorDirty", { id: pane.editorId, dirty: true }).catch(() => {
      });
      pane.title.textContent = uiText("● Editor · ") + file.name;
    };
    const save = async () => {
      const saved = input.value;
      await call("editorSave", { id: pane.editorId, text: saved });
      pane.dirty = input.value !== saved;
      await rpc("editorDirty", { id: pane.editorId, dirty: pane.dirty });
      pane.title.textContent = (pane.dirty ? "● " : "") + "Editor · " + file.name;
      toast(uiText("File remoto salvato"));
    };
    toolbar.append(localizedButton("Salva sul server", save, "primary"), localizedButton("Ricarica dal server", async () => {
      if (pane.dirty && !await call("confirmAction", { text: "Scartare le modifiche locali e ricaricare il file remoto?" })) return;
      const result = await call("editorReload", { id: pane.editorId });
      input.value = result.text;
      pane.dirty = false;
      await rpc("editorDirty", { id: pane.editorId, dirty: false });
      pane.title.textContent = uiText("Editor · ") + file.name;
    }));
    const help = document.createElement("p");
    help.className = "editor-help";
    help.textContent = uiText("UTF-8 · Ctrl+S salva sul server · Controllo conflitti prima della sostituzione.");
    pane.surface.append(toolbar, help, input);
    input.onkeydown = (e) => {
      if (e.ctrlKey && e.key.toLowerCase() === "s") {
        e.preventDefault();
        save().catch(() => {
        });
      }
      if (e.key === "Tab") {
        e.preventDefault();
        input.setRangeText("	", input.selectionStart, input.selectionEnd, "end");
        input.dispatchEvent(new Event("input"));
      }
    };
    input.focus();
  } catch (e) {
    toast(e.message || "Impossibile aprire il file nell’editor.", true);
  }
}
async function closeEditorPane(pane) {
  if (pane.dirty && !await call("confirmAction", { text: "Chiudere l’editor e scartare le modifiche non salvate?" })) return;
  await call("editorClose", { id: pane.editorId });
  finishDetachedPane(pane);
  pane.container.remove();
  pane.tab.remove();
  panes.delete(pane.id);
  activeId = panes.has(pane.owner) ? pane.owner : [...panes.keys()].at(-1) || null;
  refreshStage();
}
window.fileMessage = (type, payload) => {
  if (type === "fileQueue") {
    const prior = fileQueue;
    fileQueue = payload;
    renderFileQueues();
    for (const pane of panes.values()) if (pane.fileWorkspace && payload.some((j) => j.sessionId === pane.id && j.status === "Completato" && !prior.some((old) => old.id === j.id && old.status === "Completato"))) pane.refreshFiles();
  }
  if (type.startsWith("editor")) {
    const external = externalEdits.get(payload.id);
    if (external) {
      external.status = type === "editorSaved" ? "Salvataggio caricato · sincronizzazione automatica attiva" : type === "editorConflict" ? "Caricamento interrotto: " + payload.error + " · copia locale conservata" : "Salvataggio rilevato · caricamento automatico…";
      external.error = type === "editorConflict";
      const owner = panes.get(external.owner);
      if (owner) renderExternalEdits(owner);
      if (external.error) toast(external.status, true);
      return;
    }
    const pane = [...panes.values()].find((p) => p.editorId === payload.id);
    if (!pane) return;
    if (type === "editorSaved") {
      if (payload.external) {
        if (!pane.dirty) pane.editorInput.value = payload.text;
        pane.status.textContent = uiText("Salvataggio esterno caricato");
      } else pane.status.textContent = uiText("Salvato sul server");
    }
    if (type === "editorExternalChanged") pane.status.textContent = uiText("File esterno modificato: carica o abilita il caricamento automatico");
    if (type === "editorConflict") {
      if (pane.externalAuto) pane.externalAuto.checked = false;
      pane.status.textContent = payload.error;
      toast(payload.error, true);
    }
  }
};
function machinesDialog() {
  const body = showModal(uiText("Rubrica macchine"), "INDIRIZZI / PORTE");
  notice(body, uiText("Aggiungi indirizzi e porte senza creare una connessione. Le credenziali restano separate."));
  body.append(localizedButton("Nuovo indirizzo", () => machineDialog()));
  for (const m of state.data.machines || []) {
    const row = document.createElement("div");
    row.className = "list-row";
    const label = document.createElement("div");
    const name = document.createElement("strong");
    name.textContent = m.name;
    const endpoint = document.createElement("small");
    endpoint.textContent = m.host + ":" + m.port;
    label.append(name, endpoint);
    row.append(label, localizedButton("Modifica", () => machineDialog(m)), localizedButton("Nuova connessione", () => profileDialog({ id: crypto.randomUUID().replaceAll("-", ""), name: m.name, folder: "Connessioni", host: m.host, port: m.port, username: "", protocol: "ssh", authentication: "password", keyId: "", passwordId: "", remoteDirectory: ".", environment: "sviluppo", notes: "", tags: "", jumpHost: "", tunnels: [] })), localizedButton("Elimina", async () => {
      update(await call("machineDelete", { id: m.id }));
      machinesDialog();
    }, "danger"));
    body.append(row);
  }
}
$("machines-btn").onclick = machinesDialog;
new ResizeObserver(() => refreshStage()).observe(document.querySelector(".sidebar"));
async function prepareExplorerFiles(pane) {
  const files = selectedFiles(pane, "remote");
  if (!files.length) return toast(uiText("Seleziona file o cartelle nel pannello remoto."), true);
  await call("prepareExplorerExport", { sessionId: pane.id, paths: files.map((f) => f.path) });
}
