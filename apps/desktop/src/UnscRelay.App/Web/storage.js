"use strict";
function storageProfileDialog(existing, selection, draft) {
  const p = existing ? structuredClone(existing) : { id: crypto.randomUUID().replaceAll("-", ""), name: "", folder: "Connessioni", host: "", port: 21, username: "", protocol: "native", authentication: "password", remoteDirectory: ".", localDirectory: "", passwordId: "", native: { kind: selection.kind, argumentsJson: "[]" }, storage: { browser: true, tls: true, implicitTls: false, provider: "Other", region: "us-east-1", bucket: "", endpoint: "" } };
  const kind = p.native.kind, s3 = kind === "s3";
  if (!existing && draft) {
    for (const key of ["name", "folder", "notes", "tags"]) if (draft[key]) p[key] = draft[key];
  }
  const body = showModal(existing ? "Modifica " + (s3 ? "S3" : "FTP / FTPS") : "Nuovo browser " + (s3 ? "S3" : "FTP / FTPS"), "FILE REMOTI / CONFIGURAZIONE GUIDATA", null, true);
  modal.classList.add("connection-modal");
  const form = document.createElement("form");
  form.className = "connection-form";
  const grid = document.createElement("div");
  grid.className = "form-grid";
  form.append(grid);
  field(grid, uiText("Nome"), "name", p.name).required = true;
  field(grid, uiText("Cartella"), "folder", p.folder, "text", folderChoices(p.folder));
  let host, port, tls, endpoint, region, bucket, provider;
  if (s3) {
    provider = field(grid, uiText("Provider"), "provider", p.storage.provider, "text", [["Other", "S3 compatibile"], ["AWS", "Amazon S3"]]);
    endpoint = field(grid, uiText("Endpoint HTTPS · vuoto per AWS"), "endpoint", p.storage.endpoint);
    endpoint.placeholder = "https://storage.example.com";
    region = field(grid, uiText("Regione"), "region", p.storage.region);
    bucket = field(grid, uiText("Bucket"), "bucket", p.storage.bucket);
    bucket.required = true;
    provider.onchange = () => {
      endpoint.required = provider.value === "Other";
    };
    provider.onchange();
  } else {
    host = field(grid, uiText("Host FTP"), "host", p.host);
    host.required = true;
    port = field(grid, uiText("Porta"), "port", p.port, "number");
    port.min = 1;
    port.max = 65535;
    tls = field(grid, uiText("Protezione della connessione"), "tls", p.storage.tls ? p.storage.implicitTls ? "implicit" : "explicit" : "plain", "text", [["explicit", "FTPS · TLS esplicito (consigliato)"], ["implicit", "FTPS · TLS implicito"], ["plain", "FTP · senza cifratura"]]);
    tls.onchange = () => {
      if ([21, 990].includes(Number(port.value))) port.value = tls.value === "implicit" ? 990 : 21;
    };
  }
  field(grid, s3 ? "Access key ID" : "Nome utente", "username", p.username).required = true;
  const password = field(grid, s3 ? "Secret access key" : "Password", "password", "", "password");
  password.autocomplete = "new-password";
  password.maxLength = 8192;
  password.placeholder = p.passwordId ? "Salvata · lascia vuoto per mantenerla" : "Richiesta alla connessione se lasci vuoto";
  const rememberLabel = document.createElement("label");
  rememberLabel.className = "check-label";
  const remember = document.createElement("input");
  remember.type = "checkbox";
  remember.checked = !!p.passwordId;
  rememberLabel.append(remember, "Salva la credenziale nell’archivio cifrato");
  form.append(rememberLabel);
  const root = field(grid, uiText("Cartella remota iniziale · radice del browser"), "remoteDirectory", p.remoteDirectory || ".");
  notice(form, s3 ? "Access key e segreto sono inviati soltanto al motore incluso. Il collegamento usa HTTPS; i file nel bucket non vengono cifrati da Relay." : "Il certificato FTPS viene verificato. FTP senza TLS richiede una conferma prima della connessione. I file sul server non vengono cifrati da Relay.");
  notice(form, uiText("La navigazione parte dalla cartella iniziale. Per caricare o scaricare viene aperta la finestra di scelta file di Windows."));
  const actions = document.createElement("div");
  actions.className = "connection-actions";
  actions.append(localizedButton("Annulla", () => modal.close()));
  const save = document.createElement("button");
  save.className = "primary";
  save.textContent = uiText("Salva connessione");
  actions.append(save);
  form.append(actions);
  form.onsubmit = async (event) => {
    event.preventDefault();
    save.disabled = true;
    try {
      const values = Object.fromEntries(new FormData(form));
      const payload = { ...p, name: values.name, folder: values.folder, username: values.username, host: s3 ? "" : host.value, port: s3 ? 443 : Number(port.value), remoteDirectory: root.value || ".", authentication: "password", native: { kind, argumentsJson: "[]" }, storage: { browser: true, tls: s3 || tls.value !== "plain", implicitTls: !s3 && tls.value === "implicit", provider: s3 ? provider.value : "Other", endpoint: s3 ? endpoint.value.trim() : "", region: s3 ? region.value.trim() : "us-east-1", bucket: s3 ? bucket.value.trim() : "" }, password: password.value, rememberPassword: remember.checked };
      update(await call("saveProfile", payload));
      password.value = "";
      modal.close();
      toast(uiText("Browser salvato"));
    } finally {
      save.disabled = false;
    }
  };
  body.append(form);
}
function setupStorageColumns(table) {
  const widths = fileColumnWidths("storage"), columns = document.createElement("colgroup");
  const definitions = fileColumnDefinitions.storage;
  for (const [key] of definitions) {
    const col = document.createElement("col");
    col.dataset.column = key;
    columns.append(col);
  }
  table.prepend(columns);
  const apply = () => {
    let total = 0;
    for (const [key] of definitions) {
      columns.querySelector('[data-column="' + key + '"]').style.width = widths[key] + "px";
      total += widths[key];
    }
    table.style.width = total + "px";
  };
  const save = () => {
    try {
      localStorage.setItem("relay.fileColumns.v1.storage", JSON.stringify(widths));
    } catch {
      toast(uiText("Impossibile memorizzare la larghezza delle colonne."), true);
    }
  };
  for (const [index, [key, label, initial, min]] of definitions.entries()) {
    const head = table.querySelectorAll("thead th")[index], grip = document.createElement("span");
    grip.className = "file-column-resizer";
    grip.tabIndex = 0;
    grip.setAttribute("role", "separator");
    grip.setAttribute("aria-orientation", "vertical");
    grip.setAttribute("aria-label", uiText("Larghezza ") + label + " storage");
    grip.setAttribute("aria-valuemin", String(min));
    grip.setAttribute("aria-valuemax", "1400");
    interfaceAttribute(grip, 'title', "Trascina o usa le frecce. Doppio clic per ripristinare.");
    const set = (value) => {
      widths[key] = Math.round(Math.max(min, Math.min(1400, value)));
      grip.setAttribute("aria-valuenow", String(widths[key]));
      apply();
    };
    set(widths[key]);
    grip.onpointerdown = (e) => {
      if (e.button !== 0) return;
      e.preventDefault();
      const start = e.clientX, width = widths[key];
      grip.setPointerCapture(e.pointerId);
      grip.onpointermove = (move) => {
        if (grip.hasPointerCapture(move.pointerId)) set(width + move.clientX - start);
      };
      const done = (event) => {
        if (grip.hasPointerCapture(event.pointerId)) grip.releasePointerCapture(event.pointerId);
        grip.onpointermove = null;
        save();
      };
      grip.onpointerup = done;
      grip.onpointercancel = done;
    };
    grip.onkeydown = (e) => {
      if (!["ArrowLeft", "ArrowRight", "Home"].includes(e.key)) return;
      e.preventDefault();
      e.stopPropagation();
      set(e.key === "Home" ? initial : widths[key] + (e.key === "ArrowLeft" ? -1 : 1) * (e.shiftKey ? 40 : 10));
      save();
    };
    grip.ondblclick = (e) => {
      e.preventDefault();
      set(initial);
      save();
    };
    head.append(grip);
  }
}
async function openStorageWorkspace(profile, source = null) {
  if (!await ensureConnectionVault(profile)) return null;
  const pane = toolPane(profile, (source ? "SFTP" : profile.native.kind === "s3" ? "S3" : "FTP") + " · " + profile.name, "storage");
  pane.storageWorkspace = true;
  pane.nativeSourceId = source?.id;
  pane.connected = false;
  pane.storagePath = "";
  pane.storageHistory = [""];
  pane.storageIndex = 0;
  pane.storageEntries = [];
  pane.storageSelection = null;
  const toolbar = document.createElement("div");
  toolbar.className = "storage-toolbar";
  const back = localizedButton("← Indietro", () => navigate(pane.storageHistory[pane.storageIndex - 1], "back")), forward = localizedButton("Avanti →", () => navigate(pane.storageHistory[pane.storageIndex + 1], "forward")), up = localizedButton("↑ Cartella superiore", () => navigate(pane.storagePath.split("/").slice(0, -1).join("/"))), home = localizedButton("⌂ Home", () => navigate("")), refresh = localizedButton("↻ Aggiorna", () => navigate(pane.storagePath, "refresh"));
  const address = document.createElement("input");
  interfaceAttribute(address, 'aria-label', "Percorso storage");
  interfaceAttribute(address, 'placeholder', "/ · cartella iniziale");
  address.onkeydown = (e) => {
    if (e.key === "Enter") {
      e.preventDefault();
      navigate(address.value);
    }
  };
  const upload = localizedButton("↑ Carica file", () => transfer(true)), download = localizedButton("↓ Scarica file", () => transfer(false)), mkdir = localizedButton("＋ Nuova cartella", () => changeDialog("mkdir")), rename = localizedButton("Rinomina", () => changeDialog("moveto")), remove = localizedButton("Elimina", () => change("deletefile", pane.storageSelection?.path), "danger");
  const compare = localizedButton("Confronta con file locale", compareSelected);
  toolbar.append(back, forward, up, home, refresh, address, upload, download, compare, mkdir, rename, remove);
  let follow = null;
  if (source?.profile.native?.kind === "ssh") {
    const label = document.createElement("label");
    label.className = "check-label";
    follow = document.createElement("input");
    follow.type = "checkbox";
    follow.disabled = true;
    label.append(follow, "Segui cartella del terminale");
    interfaceAttribute(label, 'title', "La shell deve comunicare la cartella con OSC 7. La navigazione resta nella radice iniziale SFTP.");
    toolbar.append(label, localizedButton("Attiva rilevamento Bash", () => enableBashDirectory(source, follow)));
    follow.onchange = () => {
      if (follow.checked && source.cwd) pane.storageFollowDirectory(source.cwd);
    };
  }
  const search = document.createElement("input");
  search.type = "search";
  interfaceAttribute(search, 'placeholder', "Filtra per nome");
  interfaceAttribute(search, 'aria-label', "Filtra file storage");
  search.oninput = render;
  const table = document.createElement("table");
  table.className = "storage-table";
  table.innerHTML = '<thead><tr><th scope="col">Nome</th><th scope="col">Dimensione</th><th scope="col">Tipo</th><th scope="col">Modifica</th></tr></thead>';
  setupStorageColumns(table);
  const rows = document.createElement("tbody");
  table.append(rows);
  const viewport = document.createElement("div");
  viewport.className = "storage-viewport";
  viewport.append(table);
  const activity = document.createElement("div");
  activity.className = "storage-activity";
  const progress = document.createElement("progress");
  interfaceAttribute(progress, 'aria-label', "Operazione storage in corso");
  const cancel = localizedButton("Annulla operazione", () => call("storageCancel", { sessionId: pane.id }));
  activity.append(progress, cancel);
  activity.hidden = true;
  pane.storageView = fileView("storage");
  const collapsed = /* @__PURE__ */ new Set(), viewControls = document.createElement("div");
  viewControls.className = "file-view-options";
  const group = field(viewControls, uiText("Raggruppa file"), "storageGroup", pane.storageView.group, "text", [["type", "Tipo"], ["name", "Nome"], ["size", "Dimensione"], ["modified", "Data modifica"], ["none", "Nessuno"]]);
  const sort = field(viewControls, uiText("Ordina file"), "storageSort", pane.storageView.sort, "text", [["name", "Nome"], ["size", "Dimensione"], ["type", "Tipo"], ["modified", "Data modifica"]]);
  const direction = field(viewControls, uiText("Ordine"), "storageDirection", pane.storageView.descending ? "desc" : "asc", "text", [["asc", "Crescente"], ["desc", "Decrescente"]]);
  for (const input of [group, sort, direction]) input.onchange = () => {
    pane.storageView = { group: group.value, sort: sort.value, descending: direction.value === "desc" };
    collapsed.clear();
    try {
      localStorage.setItem("relay.fileView.storage", JSON.stringify(pane.storageView));
    } catch {
      toast(uiText("Impossibile salvare la vista dei file."), true);
    }
    render();
  };
  pane.surface.append(toolbar, viewControls, search, viewport, activity);
  pane.status.setAttribute("role", "status");
  let busy = false;
  function setBusy(value, text) {
    busy = value;
    pane.surface.setAttribute("aria-busy", String(value));
    activity.hidden = !value;
    pane.status.textContent = text || "Connesso";
    for (const b of toolbar.querySelectorAll("button")) b.disabled = value;
    address.disabled = value;
    updateButtons();
  }
  function updateButtons() {
    back.disabled = busy || pane.storageIndex <= 0;
    forward.disabled = busy || pane.storageIndex >= pane.storageHistory.length - 1;
    up.disabled = busy || !pane.storagePath;
    compare.disabled = download.disabled = rename.disabled = remove.disabled = busy || !pane.storageSelection || pane.storageSelection.directory;
  }
  function render() {
    rows.replaceChildren();
    const groups = groupedFiles({ nav: { remote: { view: pane.storageView } }, entries: { remote: pane.storageEntries } }, "remote", search.value);
    for (const [key, entries] of groups) {
      if (key) {
        const row = document.createElement("tr"), cell = document.createElement("td");
        cell.colSpan = 4;
        cell.className = "storage-group-cell";
        const toggle = button((collapsed.has(key) ? "▸ " : "▾ ") + key + " (" + entries.length + ")", () => {
          if (collapsed.has(key)) collapsed.delete(key);
          else {
            collapsed.add(key);
            if (entries.some((e) => e.path === pane.storageSelection?.path)) pane.storageSelection = null;
          }
          render();
          updateButtons();
        });
        toggle.className = "file-group";
        toggle.setAttribute("aria-expanded", String(!collapsed.has(key)));
        cell.append(toggle);
        row.append(cell);
        rows.append(row);
        if (collapsed.has(key)) continue;
      }
      for (const entry of entries) {
        const row = document.createElement("tr");
        row.dataset.storagePath = entry.path;
        row.tabIndex = 0;
        row.setAttribute("aria-selected", String(pane.storageSelection?.path === entry.path));
        const name = document.createElement("td");
        name.append(fileGlyph(entry), document.createTextNode(entry.name));
        row.append(name);
        for (const text of [entry.directory ? "—" : formatSize(entry.size), entry.type || "File", entry.modified ? new Date(entry.modified).toLocaleString(document.documentElement.lang) : "—"]) {
          const td = document.createElement("td");
          td.textContent = text;
          row.append(td);
        }
        const select = () => {
          pane.storageSelection = entry;
          for (const other of rows.querySelectorAll("tr")) other.setAttribute("aria-selected", String(other === row));
          updateButtons();
        };
        row.onclick = select;
        const open = () => entry.directory ? navigate(entry.path) : (pane.storageSelection = entry, transfer(false));
        row.ondblclick = open;
        row.onkeydown = (e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            open();
          } else if (e.key === " ") {
            e.preventDefault();
            select();
          }
        };
        rows.append(row);
      }
    }
    if (!rows.children.length) {
      const row = document.createElement("tr"), cell = document.createElement("td");
      cell.colSpan = 4;
      cell.textContent = search.value ? "Nessun risultato" : "Cartella vuota";
      row.append(cell);
      rows.append(row);
    }
  }
  async function perform(action) {
    if (busy) return;
    setBusy(true, "Operazione in corso…");
    try {
      await action();
    } catch (error) {
      pane.status.textContent = error.message;
      toast(error.message, true);
    } finally {
      setBusy(false, pane.status.textContent === "Operazione in corso…" ? "Connesso" : pane.status.textContent);
    }
  }
  async function navigate(path, mode = "push") {
    if (path === void 0) return;
    await perform(async () => {
      const canonical = path.replace(/^\/+|\/+$/g, "");
      const entries = await call("storageList", { sessionId: pane.id, path: canonical });
      pane.storageEntries = entries;
      pane.storagePath = canonical;
      pane.storageSelection = null;
      address.value = canonical;
      if (mode === "back") pane.storageIndex--;
      else if (mode === "forward") pane.storageIndex++;
      else if (mode === "push" && pane.storageHistory[pane.storageIndex] !== canonical) {
        pane.storageHistory.splice(pane.storageIndex + 1);
        pane.storageHistory.push(canonical);
        pane.storageIndex++;
      }
      render();
    });
  }
  async function compareSelected() {
    if (busy || !pane.connected || !pane.storageSelection || pane.storageSelection.directory) return;
    const path = pane.storageSelection.path;
    const body = showModal(uiText("Confronta file"), "SFTP / STORAGE · " + profile.name);
    notice(body, uiText("Scegli un file locale: le differenze sono calcolate su questo PC. Nessun file viene modificato. Testo UTF-8, massimo 2 MiB per file."));
    const launch = localizedButton("Scegli file locale e confronta", async () => {
      launch.disabled = true;
      body.querySelector(".file-diff")?.remove();
      try {
        await showFileComparison(body, {}, "local", pane, () => call("storageCompare", { sessionId: pane.id, path }));
      } finally {
        launch.disabled = false;
      }
    });
    launch.className = "compare-files-button";
    body.append(launch, localizedButton("Chiudi", () => modal.close()));
    launch.click();
  }
  async function transfer(uploading) {
    if (!uploading && !pane.storageSelection) return;
    await perform(async () => {
      await call("storageTransfer", { sessionId: pane.id, path: uploading ? pane.storagePath : pane.storageSelection.path, upload: uploading });
      pane.storageEntries = await call("storageList", { sessionId: pane.id, path: pane.storagePath });
      pane.storageSelection = null;
      render();
    });
  }
  async function change(verb, path, destination) {
    if (!path) return;
    await perform(async () => {
      await call("storageChange", { sessionId: pane.id, verb, path, destination });
      pane.storageEntries = await call("storageList", { sessionId: pane.id, path: pane.storagePath });
      pane.storageSelection = null;
      render();
    });
  }
  function changeDialog(verb) {
    if (busy) return;
    const body = showModal(verb === "mkdir" ? "Nuova cartella" : "Rinomina file", "STORAGE / " + profile.name);
    const name = field(body, uiText("Nome"), "storageName", verb === "moveto" ? pane.storageSelection?.name || "" : "");
    body.append(localizedButton("Annulla", () => modal.close()), localizedButton("Conferma", async () => {
      const value = name.value.trim();
      if (!value || /[\\/:\x00-\x1f]/.test(value) || [".", ".."].includes(value)) {
        toast(uiText("Indica un nome senza separatori di percorso."), true);
        return;
      }
      const target = (pane.storagePath ? pane.storagePath + "/" : "") + value;
      const source2 = pane.storageSelection?.path;
      modal.close();
      await change(verb, verb === "mkdir" ? target : source2, target);
    }, "primary"));
    name.focus();
  }
  pane.storageContext = (path) => {
    const selected = pane.storageEntries.find((entry) => entry.path === path);
    if (selected) {
      pane.storageSelection = selected;
      for (const row of rows.querySelectorAll("tr")) row.setAttribute("aria-selected", String(row.dataset.storagePath === path));
      updateButtons();
    }
    const actions = [];
    const add = (label, run, disabled = busy || !pane.connected) => actions.push({ label, run, disabled });
    if (selected) {
      if (selected.directory) add("Apri cartella", () => navigate(selected.path));
      else {
        add("Confronta con file locale", compareSelected);
        add("Scarica file", () => transfer(false));
        add("Rinomina", () => changeDialog("moveto"));
        add("Elimina dal server", () => change("deletefile", selected.path));
      }
      add("Copia percorso relativo", () => copyText(selected.path), false);
    }
    add("Carica file", () => transfer(true));
    add("Nuova cartella", () => changeDialog("mkdir"));
    add("Aggiorna", () => navigate(pane.storagePath, "refresh"));
    return actions;
  };
  pane.storageFollowDirectory = (path) => {
    if (!follow?.checked || !pane.connected || !pane.followRoot) return;
    const root = pane.followRoot.replace(/\/+$/, "");
    if (path !== root && !path.startsWith(root + "/")) {
      pane.status.textContent = uiText("Cartella del terminale fuori dalla radice SFTP iniziale.");
      return;
    }
    const relative = path.slice(root.length).replace(/^\/+/, "");
    if (relative === pane.storagePath) return;
    clearTimeout(pane.followTimer);
    pane.followTimer = setTimeout(() => navigate(relative), 150);
  };
  pane.storageRecovered = async (result) => {
    clearTimeout(pane.followTimer);
    pane.followRoot = result?.followRoot || null;
    if (follow) {
      follow.disabled = !pane.followRoot;
      if (!pane.followRoot) follow.checked = false;
    }
    pane.connected = true;
    setBusy(false, "SFTP ripristinato");
    await navigate(pane.storagePath, "refresh");
  };
  pane.storageDisconnected = (text) => {
    clearTimeout(pane.followTimer);
    pane.connected = false;
    setBusy(false, text);
    toolbar.querySelectorAll("button").forEach((b) => b.disabled = true);
    address.disabled = true;
  };
  setBusy(true, "Connessione…");
  try {
    const result = await call(source ? "storageNativeOpen" : "storageOpen", { sessionId: pane.id, profileId: profile.id, ...source ? { sourceSessionId: source.id } : {} });
    pane.storageEntries = result.entries;
    pane.followRoot = result.followRoot || null;
    if (follow) follow.disabled = !pane.followRoot;
    pane.connected = true;
    render();
    setBusy(false, "Connesso");
  } catch (error) {
    setBusy(false, error.message);
    pane.connected = false;
    toolbar.querySelectorAll("button").forEach((b) => b.disabled = true);
    address.disabled = true;
  }
  return pane;
}
