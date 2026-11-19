"use strict";
function fileType(file) {
  if (file.directory) return "Cartelle";
  const dot = file.name.lastIndexOf(".");
  return dot > 0 && dot < file.name.length - 1 ? "File " + file.name.slice(dot + 1).toUpperCase() : "File senza estensione";
}
function fileView(side) {
  try {
    const view = JSON.parse(localStorage.getItem("relay.fileView." + side) || "{}");
    return { group: ["type", "name", "size", "modified", "none"].includes(view.group) ? view.group : "type", sort: ["name", "type", "size", "modified"].includes(view.sort) ? view.sort : "name", descending: !!view.descending };
  } catch {
    return { group: "type", sort: "name", descending: false };
  }
}
function saveFileView(pane, side) {
  localStorage.setItem("relay.fileView." + side, JSON.stringify(pane.nav[side].view));
  renderFileRows(pane, side, pane[side + "Filter"].value);
}
function setupFileNavigation(pane, side, column, heading, form) {
  pane.nav ??= {};
  pane.nav[side] = { back: [], forward: [], home: null, current: "", sequence: 0, view: fileView(side) };
  const nav = pane.nav[side];
  const controls = document.createElement("div");
  controls.className = "file-navigation";
  function navButton(label, text, action) {
    const b = button(text, action);
    b.setAttribute("aria-label", label);
    b.title = label;
    controls.append(b);
    return b;
  }
  nav.backButton = navButton(side === "local" ? "Indietro locale" : "Indietro remoto", "←", () => fileHistory(pane, side, -1));
  nav.forwardButton = navButton(side === "local" ? "Avanti locale" : "Avanti remoto", "→", () => fileHistory(pane, side, 1));
  nav.upButton = navButton(side === "local" ? "Cartella superiore locale" : "Cartella superiore remota", "↑", () => loadFileSide(pane, side, side === "local" ? pane.localParent || pane.localPath : pane.remotePath.replace(/\/[^/]+\/?$/, "") || "/"));
  navButton(side === "local" ? "Home locale" : "Home remota", "⌂", () => loadFileSide(pane, side, nav.home || ""));
  navButton("Aggiorna " + (side === "local" ? "locale" : "remoto"), "↻", () => loadFileSide(pane, side, nav.current, { history: "refresh" }));
  const crumbs = document.createElement("nav");
  crumbs.className = "file-breadcrumbs";
  crumbs.setAttribute("aria-label", side === "local" ? "Percorso locale navigabile" : "Percorso remoto navigabile");
  nav.crumbs = crumbs;
  const views = document.createElement("div");
  views.className = "file-view-options";
  const group = field(views, uiText("Raggruppa ") + (side === "local" ? "locale" : "remoto"), "group-" + side, nav.view.group, "text", [["type", "Tipo"], ["name", "Nome"], ["modified", "Data modifica"], ["size", "Dimensione"], ["none", "Nessuno"]]);
  group.onchange = () => {
    nav.view.group = group.value;
    nav.collapsed = /* @__PURE__ */ new Set();
    saveFileView(pane, side);
  };
  const sort = field(views, uiText("Ordina ") + (side === "local" ? "locale" : "remoto"), "sort-" + side, nav.view.sort, "text", [["name", "Nome"], ["type", "Tipo"], ["size", "Dimensione"], ["modified", "Data modifica"]]);
  sort.onchange = () => {
    nav.view.sort = sort.value;
    saveFileView(pane, side);
  };
  const order = field(views, uiText("Ordine ") + (side === "local" ? "locale" : "remoto"), "order-" + side, nav.view.descending ? "desc" : "asc", "text", [["asc", "Crescente"], ["desc", "Decrescente"]]);
  order.onchange = () => {
    nav.view.descending = order.value === "desc";
    saveFileView(pane, side);
  };
  const pathControls = document.createElement("div");
  pathControls.className = "file-path-controls";
  column.insertBefore(pathControls, form);
  pathControls.append(controls, form);
  column.insertBefore(crumbs, pathControls.nextSibling);
  column.insertBefore(views, crumbs.nextSibling);
  nav.backButton.disabled = nav.forwardButton.disabled = true;
  column.addEventListener("keydown", (e) => {
    if (e.altKey && ["ArrowLeft", "ArrowRight", "ArrowUp", "Home"].includes(e.key)) {
      e.preventDefault();
      if (e.key === "ArrowLeft") fileHistory(pane, side, -1);
      if (e.key === "ArrowRight") fileHistory(pane, side, 1);
      if (e.key === "ArrowUp") nav.upButton.click();
      if (e.key === "Home") loadFileSide(pane, side, nav.home || "");
    }
    if (e.key === "F5") {
      e.preventDefault();
      loadFileSide(pane, side, nav.current, { history: "refresh" });
    }
  });
}
function commitFileNavigation(pane, side, path, mode = "push") {
  const nav = pane.nav[side];
  const previous = nav.current;
  if (path !== previous) {
    if (mode === "back") {
      nav.back.pop();
      if (previous) nav.forward.push(previous);
    } else if (mode === "forward") {
      nav.forward.pop();
      if (previous) nav.back.push(previous);
    } else if (mode === "push") {
      if (previous) nav.back.push(previous);
      nav.forward = [];
    }
    nav.current = path;
  }
  nav.backButton.disabled = !nav.back.length;
  nav.forwardButton.disabled = !nav.forward.length;
  nav.crumbs.replaceChildren();
  const local = side === "local";
  const separator = local ? "\\" : "/";
  let prefix = local && path.startsWith("\\\\") ? "\\\\" : local ? "" : "/";
  const parts = path.split(/[\\/]/).filter(Boolean);
  if (!local) {
    const root = button("/", () => loadFileSide(pane, side, "/"));
    interfaceAttribute(root, 'title', "Radice remota");
    nav.crumbs.append(root);
  }
  for (const part of parts) {
    prefix = prefix + (prefix && !prefix.endsWith(separator) ? separator : "") + part;
    if (local && /^[a-z]:$/i.test(prefix)) prefix += "\\";
    const target = prefix;
    const b = button(part, () => loadFileSide(pane, side, target));
    b.title = target;
    b.setAttribute("aria-label", uiText("Apri ") + target);
    nav.crumbs.append(b);
  }
  if (mode !== "refresh" && path !== previous && pane.profile.rememberFileDirectories !== false && pane.localPath && pane.remotePath) {
    const value = { host: pane.profile.host, port: pane.profile.port, username: pane.profile.username, local: pane.localPath, remote: pane.remotePath };
    (state.data.fileLocations ??= {})[pane.profile.id] = value;
    rpc("fileLocation", { sessionId: pane.id, local: value.local, remote: value.remote }).catch(() => {
    });
  }
  pane.status.textContent = uiText("SFTP connesso · ") + pane.entries[side].length + " elementi";
}
function fileHistory(pane, side, direction) {
  const nav = pane.nav[side], stack = direction < 0 ? nav.back : nav.forward;
  if (!stack.length) return;
  return loadFileSide(pane, side, stack.at(-1), { history: direction < 0 ? "back" : "forward" });
}
function groupedFiles(pane, side, filter) {
  const view = pane.nav[side].view, files = pane.entries[side].filter((f) => f.name.toLocaleLowerCase().includes(filter.toLocaleLowerCase()));
  const direction = view.descending ? -1 : 1;
  const compare = (a, b) => view.sort === "size" ? a.size - b.size : view.sort === "modified" ? new Date(a.modified) - new Date(b.modified) : view.sort === "type" ? fileType(a).localeCompare(fileType(b), "it") : a.name.localeCompare(b.name, "it", { numeric: true });
  files.sort((a, b) => Number(b.directory) - Number(a.directory) || direction * compare(a, b) || a.name.localeCompare(b.name, "it", { numeric: true }));
  const groups = /* @__PURE__ */ new Map();
  const dateKey = (f) => {
    const days = Math.floor(((/* @__PURE__ */ new Date()).setHours(0, 0, 0, 0) - new Date(f.modified).setHours(0, 0, 0, 0)) / 864e5);
    return days < 0 ? "Date future" : days === 0 ? "Oggi" : days === 1 ? "Ieri" : days < 7 ? "Ultimi 7 giorni" : days < 30 ? "Ultimi 30 giorni" : "Meno recenti";
  };
  for (const f of files) {
    const key = view.group === "none" ? "" : f.directory ? "Cartelle" : view.group === "type" ? fileType(f) : view.group === "name" ? f.name[0]?.toLocaleUpperCase() || "#" : view.group === "modified" ? dateKey(f) : f.size === 0 ? "Vuoti" : f.size < 1024 ? "Meno di 1 KiB" : f.size < 1048576 ? "Da 1 KiB a 1 MiB" : f.size < 1073741824 ? "Da 1 MiB a 1 GiB" : "Oltre 1 GiB";
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(f);
  }
  const keys = [...groups.keys()];
  if (view.group === "modified" || view.group === "size") {
    const ordered = view.group === "modified" ? ["Cartelle", "Date future", "Oggi", "Ieri", "Ultimi 7 giorni", "Ultimi 30 giorni", "Meno recenti"] : ["Cartelle", "Vuoti", "Meno di 1 KiB", "Da 1 KiB a 1 MiB", "Da 1 MiB a 1 GiB", "Oltre 1 GiB"];
    keys.sort((a, b) => a === "Cartelle" ? -1 : b === "Cartelle" ? 1 : direction * (ordered.indexOf(a) - ordered.indexOf(b)));
  }
  if (view.group === "type" || view.group === "name") keys.sort((a, b) => a === "Cartelle" ? -1 : b === "Cartelle" ? 1 : direction * a.localeCompare(b, "it", { numeric: true }));
  return keys.map((key) => [key, groups.get(key)]);
}
function fileSelection(pane, side, file, event) {
  const selected = pane.selection[side], visible = groupedFiles(pane, side, pane[side + "Filter"].value).flatMap((g) => g[1]);
  const anchor = pane.nav[side].anchor;
  if (event.shiftKey && anchor) {
    const first = visible.findIndex((f) => f.path === anchor), last = visible.findIndex((f) => f.path === file.path);
    if (first >= 0 && last >= 0) {
      if (!event.ctrlKey) selected.clear();
      for (const item of visible.slice(Math.min(first, last), Math.max(first, last) + 1)) selected.add(item.path);
    }
  } else {
    if (!event.ctrlKey && !event.metaKey) selected.clear();
    selected.has(file.path) ? selected.delete(file.path) : selected.add(file.path);
    pane.nav[side].anchor = file.path;
  }
  for (const row of pane.lists[side].querySelectorAll(".workspace-file-row")) row.setAttribute("aria-selected", String(selected.has(row.dataset.filePath)));
}
