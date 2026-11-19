"use strict";
const sessionTypes = [
  { id: "ftp-files", protocol: "native", kind: "ftp", storage: true, name: "FTP / FTPS", group: "Connessioni remote", detail: "Browser file · TLS e credenziali cifrate", icon: "folder" },
  { id: "s3-files", protocol: "native", kind: "s3", storage: true, name: "S3 · Browser file", group: "Connessioni remote", detail: "Amazon S3 e provider compatibili", icon: "folder" },
  { id: "ssh", protocol: "ssh", name: "SSH", group: "Connessioni remote", detail: "Terminale sicuro con accesso SFTP", icon: "terminal" },
  { id: "sftp", protocol: "sftp", name: "SFTP", group: "Connessioni remote", detail: "Browser file e trasferimenti", icon: "folder" },
  { id: "rdp", protocol: "rdp", name: "RDP", group: "Connessioni remote", detail: "Desktop Windows · diretto o via SSH", icon: "monitor" },
  { id: "vnc", protocol: "vnc", name: "VNC", group: "Connessioni remote", detail: "Controllo remoto · diretto o via SSH", icon: "screen" },
  { id: "cmd", protocol: "local", shell: "cmd", name: "CMD", group: "Terminali locali", detail: "Prompt dei comandi Windows", icon: "terminal" },
  { id: "powershell", protocol: "local", shell: "powershell", name: "Windows PowerShell", group: "Terminali locali", detail: "Shell Windows PowerShell 5.1", icon: "powershell" },
  { id: "pwsh", protocol: "local", shell: "pwsh", name: "PowerShell 7", group: "Terminali locali", detail: "PowerShell portatile incluso", icon: "powershell" },
  { id: "wsl", protocol: "local", shell: "wsl", name: "WSL", group: "Terminali locali", detail: "Linux · distribuzioni gia installate", icon: "linux" },
  ...["ssh", "sftp", "mosh", "telnet", "serial", "ftp", "s3", "rsh", "xdmcp", "bash", "zsh", "browser", "file"].map((kind) => ({ id: "native-" + kind, protocol: "native", kind, name: { ssh: "OpenSSH nativo", sftp: "SFTP OpenSSH", mosh: "Mosh", telnet: "Telnet", serial: "Seriale", ftp: "FTP · console", s3: "Rclone · console", rsh: "Rsh", xdmcp: "XDMCP / X server", bash: "Bash inclusa", zsh: "Zsh inclusa", browser: "Browser", file: "File locali" }[kind], group: "Componenti inclusi", detail: kind === "ssh" ? "Config completo · Include, Match, ProxyCommand" : kind === "sftp" ? "Console SFTP con config OpenSSH" : "Componente portatile incluso", icon: "terminal" }))
];
function sessionIcon(kind) {
  const paths = { terminal: '<path d="m8 10 4 4-4 4m8 0h6"/>', folder: '<path d="M4 9h9l3 3h12v13H4Zm0 0V6h10l3 3"/><path d="m13 16-3 3 3 3m6-6 3 3-3 3"/>', monitor: '<rect x="3" y="4" width="26" height="18" rx="2"/><path d="M16 22v6m-6 0h12m-10-13 3 3 6-7"/>', screen: '<rect x="3" y="4" width="26" height="18" rx="2"/><path d="M16 22v6m-6 0h12m-9-17 8 4-8 4Z"/>', powershell: '<path d="m8 7 20 0-5 18H3Zm2 5 5 4-7 4m8 1h5"/>', linux: '<path d="M11 13V9a5 5 0 0 1 10 0v4l4 11H7Zm1 12-3 3H4m16-3 3 3h5m-15-13 3 2 3-2"/><circle cx="14" cy="10" r=".5"/><circle cx="18" cy="10" r=".5"/>' };
  return '<svg viewBox="0 0 32 32" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">' + (paths[kind] || paths.terminal) + "</svg>";
}
function sessionTypeDialog(draft = null) {
  const body = showModal(uiText("Che sessione vuoi creare?"), "NUOVA CONNESSIONE / SCEGLI IL TIPO", null, true);
  modal.classList.add("session-picker-modal");
  notice(body, uiText("Scegli una categoria e il tipo di sessione. La ricerca considera tutte le categorie."));
  const search = document.createElement("input");
  search.type = "search";
  interfaceAttribute(search, 'placeholder', "Cerca un tipo di sessione…");
  interfaceAttribute(search, 'aria-label', "Cerca tipo di sessione");
  body.append(search, localizedButton("Modelli di connessione", () => connectionTemplatesDialog()));
  const tablist = document.createElement("div");
  tablist.className = "session-category-tabs";
  tablist.setAttribute("role", "tablist");
  interfaceAttribute(tablist, 'aria-label', "Categorie delle connessioni");
  body.append(tablist);
  const summary = document.createElement("p");
  summary.className = "session-search-status";
  summary.setAttribute("role", "status");
  body.append(summary);
  const groups = [];
  let selected = 0;
  function select(index, focus = false) {
    selected = index;
    groups.forEach((g, i) => {
      g.tab.setAttribute("aria-selected", String(i === index));
      g.tab.tabIndex = i === index ? 0 : -1;
      g.panel.hidden = i !== index;
    });
    if (focus) groups[index].tab.focus();
  }
  for (const [index, group] of [...new Set(sessionTypes.map((t) => t.group))].entries()) {
    const tab = button(group, () => select(index));
    tab.id = "session-category-" + index;
    tab.setAttribute("role", "tab");
    tab.setAttribute("aria-controls", "session-category-panel-" + index);
    tablist.append(tab);
    const panel = document.createElement("section");
    panel.id = "session-category-panel-" + index;
    panel.className = "session-type-group";
    panel.setAttribute("role", "tabpanel");
    panel.setAttribute("aria-labelledby", tab.id);
    const grid = document.createElement("div");
    grid.className = "session-type-grid";
    const cards = [];
    for (const type of sessionTypes.filter((t) => t.group === group)) {
      const card = button("", () => renderConnectionProfile(null, type, draft));
      card.className = "session-type-card";
      card.dataset.sessionType = type.id;
      card.setAttribute("aria-label", type.name);
      card.innerHTML = sessionIcon(type.icon);
      const title = document.createElement("strong");
      title.textContent = type.name;
      const detail = document.createElement("small");
      detail.textContent = type.detail;
      card.append(title, detail);
      grid.append(card);
      cards.push({ card, type });
    }
    const empty = document.createElement("p");
    empty.textContent = uiText("Nessun risultato in questa categoria.");
    empty.hidden = true;
    panel.append(grid, empty);
    body.append(panel);
    groups.push({ tab, panel, cards, group, empty });
  }
  tablist.onkeydown = (e) => {
    const i = groups.findIndex((g) => g.tab === document.activeElement);
    if (i < 0) return;
    let n = i;
    if (e.key === "ArrowRight") n = (i + 1) % groups.length;
    else if (e.key === "ArrowLeft") n = (i + groups.length - 1) % groups.length;
    else if (e.key === "Home") n = 0;
    else if (e.key === "End") n = groups.length - 1;
    else return;
    e.preventDefault();
    select(n, true);
  };
  search.oninput = () => {
    const query = search.value.trim().toLocaleLowerCase("it");
    let total = 0;
    const matches = [];
    for (const [i, g] of groups.entries()) {
      let count = 0;
      for (const { card, type } of g.cards) {
        card.hidden = !(type.name + " " + type.detail + " " + type.group).toLocaleLowerCase("it").includes(query);
        if (!card.hidden) count++;
      }
      total += count;
      if (count) matches.push(i);
      g.tab.textContent = g.group + (query ? " (" + count + ")" : "");
      g.empty.hidden = count !== 0;
    }
    summary.textContent = query ? total + " risultati in " + matches.length + " categorie." : "";
    if (query && !matches.includes(selected) && matches.length) select(matches[0]);
  };
  const actions = document.createElement("div");
  actions.className = "form-actions";
  actions.append(localizedButton("Annulla", () => modal.close()));
  body.append(actions);
  select(0);
  search.focus();
}
