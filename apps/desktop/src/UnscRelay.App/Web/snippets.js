"use strict";
let selectedSnippetId = null;
function renderSnippetLibrary() {
  const body = showModal(uiText("Snippet e workspace"), "LIBRERIA PERSONALE", null, true);
  modal.classList.add("snippets-modal");
  const bar = document.createElement("div");
  bar.className = "snippet-toolbar";
  bar.append(localizedButton("Nuovo snippet", () => snippetEditor(), "primary"), localizedButton("Importa snippet", async () => {
    update(await call("importSnippets"));
    snippetsDialog();
  }), localizedButton("Esporta snippet", () => call("exportSnippets")), localizedButton("Salva workspace", saveWorkspace));
  body.append(bar);
  const filters = document.createElement("div");
  filters.className = "snippet-filters";
  const search = document.createElement("input");
  search.type = "search";
  interfaceAttribute(search, 'placeholder', "Cerca nome, cartella o comando");
  interfaceAttribute(search, 'aria-label', "Cerca snippet");
  const folder = document.createElement("select");
  interfaceAttribute(folder, 'aria-label', "Cartella snippet");
  for (const value of ["", ...new Set(state.data.snippets.map((s) => s.folder))].sort((a, b) => a.localeCompare(b))) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = value || "Tutte le cartelle";
    folder.append(option);
  }
  filters.append(search, folder);
  body.append(filters);
  const workspace = document.createElement("div");
  workspace.className = "snippet-workspace";
  const list = document.createElement("div");
  list.className = "snippet-list";
  list.setAttribute("role", "list");
  interfaceAttribute(list, 'aria-label', "Snippet salvati");
  const detail = document.createElement("section");
  detail.className = "snippet-detail";
  workspace.append(list, detail);
  body.append(workspace);
  function render() {
    const query = search.value.trim().toLocaleLowerCase();
    const items = state.data.snippets.filter((s) => (!folder.value || s.folder === folder.value) && [s.name, s.folder, s.text].some((v) => v.toLocaleLowerCase().includes(query))).sort((a, b) => a.name.localeCompare(b.name));
    if (!items.some((s) => s.id === selectedSnippetId)) selectedSnippetId = items[0]?.id || null;
    list.replaceChildren();
    if (!items.length) {
      const empty = document.createElement("div");
      empty.className = "snippet-empty";
      empty.textContent = state.data.snippets.length ? "Nessuno snippet corrisponde alla ricerca." : "Salva il tuo primo comando: potrai richiamarlo dal Centro comandi o con una scorciatoia.";
      list.append(empty);
    }
    for (const item2 of items) {
      const row = button("", () => {
        selectedSnippetId = item2.id;
        render();
      });
      row.className = "snippet-item" + (item2.id === selectedSnippetId ? " selected" : "");
      row.setAttribute("aria-pressed", String(item2.id === selectedSnippetId));
      const title = document.createElement("strong"), meta = document.createElement("small");
      title.textContent = item2.name;
      meta.textContent = item2.folder + (item2.shortcut ? " · " + item2.shortcut : "");
      row.append(title, meta);
      list.append(row);
    }
    detail.replaceChildren();
    const item = items.find((s) => s.id === selectedSnippetId);
    if (!item) {
      notice(detail, uiText("Gli snippet sono testo salvato. La selezione e la scorciatoia aprono un’anteprima: non eseguono comandi."));
      return;
    }
    const h = document.createElement("h3");
    h.textContent = item.name;
    detail.append(h);
    notice(detail, item.folder);
    const code = document.createElement("pre");
    code.className = "snippet-code";
    code.textContent = item.text;
    interfaceAttribute(code, 'aria-label', "Testo dello snippet");
    detail.append(code);
    const variables = [...new Set([...item.text.matchAll(/\{\{([a-zA-Z0-9_]+)\}\}/g)].map((x) => x[1]))];
    notice(detail, variables.length ? "Variabili da compilare: " + variables.join(", ") : "Nessuna variabile da compilare.");
    notice(detail, item.shortcut ? "Scorciatoia: " + item.shortcut : "Nessuna scorciatoia assegnata.");
    const actions = document.createElement("div");
    actions.className = "settings-actions";
    actions.append(localizedButton("Inserisci", () => insertSnippet(item), "primary"), localizedButton("Modifica", () => snippetEditor(item)), localizedButton("Elimina", async () => {
      update(await call("deleteSnippet", { snippetId: item.id }));
      snippetsDialog();
    }, "danger"));
    detail.append(actions);
  }
  search.oninput = folder.onchange = render;
  render();
  const saved = document.createElement("details");
  saved.className = "snippet-workspaces";
  const heading = document.createElement("summary");
  heading.textContent = uiText("Workspace salvati · ") + state.data.workspaces.length;
  saved.append(heading);
  if (!state.data.workspaces.length) notice(saved, uiText("Salva la disposizione delle tue sessioni con Salva workspace."));
  for (const w of state.data.workspaces) {
    const row = document.createElement("div");
    row.className = "list-row";
    const label = document.createElement("span");
    label.textContent = w.name + " · " + w.profileIds.length + " sessioni";
    row.append(label, localizedButton("Riapri", async () => {
      modal.close();
      await restoreWorkspace(w);
    }));
    saved.append(row);
  }
  body.append(saved);
}
function snippetEditor(existing) {
  const body = showModal(existing ? "Modifica snippet" : "Nuovo snippet", "COMANDI RIUTILIZZABILI", null, true);
  modal.classList.add("workflow-modal");
  const back = $("modal-back");
  back.hidden = false;
  back.textContent = uiText("← Libreria");
  back.onclick = snippetsDialog;
  const form = document.createElement("form");
  const name = field(form, uiText("Nome"), "name", existing?.name || "");
  name.required = true;
  name.maxLength = 100;
  const folder = field(form, uiText("Cartella"), "folder", existing?.folder || "Personali");
  folder.maxLength = 200;
  const text = field(form, uiText("Testo; variabili nel formato {{nome}}"), "text", existing?.text || "", "textarea");
  text.required = true;
  text.maxLength = 65536;
  text.rows = 8;
  const shortcut = field(form, uiText("Scorciatoia"), "shortcut", existing?.shortcut || "", "text", [["", "Nessuna"], ..."ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".split("").map((key) => ["Ctrl+Alt+Shift+" + key, "Ctrl + Alt + Shift + " + key])]);
  const error = document.createElement("p");
  error.className = "snippet-error";
  error.setAttribute("role", "alert");
  form.append(error);
  notice(form, uiText("La scorciatoia vale solo in UNSC Relay e apre l’anteprima sul terminale attivo. Gli snippet importati non attivano scorciatoie: assegnale esplicitamente dopo l’importazione."));
  const actions = document.createElement("div");
  actions.className = "settings-actions";
  const save = document.createElement("button");
  save.className = "primary";
  save.type = "submit";
  save.textContent = uiText("Salva");
  actions.append(localizedButton("Annulla", snippetsDialog), save);
  form.append(actions);
  form.onsubmit = async (e) => {
    e.preventDefault();
    error.textContent = "";
    const conflict = state.data.snippets.find((s) => s.id !== existing?.id && shortcut.value && s.shortcut === shortcut.value);
    if (conflict) {
      error.textContent = uiText("Scorciatoia già assegnata a ") + conflict.name + ".";
      shortcut.focus();
      return;
    }
    save.disabled = true;
    try {
      const id = existing?.id || crypto.randomUUID();
      update(await call("saveSnippet", { id, ...Object.fromEntries(new FormData(form)) }));
      selectedSnippetId = id;
      snippetsDialog();
    } catch {
    } finally {
      save.disabled = false;
    }
  };
  body.append(form);
}
function insertSnippet(snippet) {
  const pane = panes.get(activeId);
  if (!pane?.connected || !pane.term || pane.desktop || pane.fileWorkspace || pane.editor || pane.replay) return toast(uiText("Seleziona un terminale connesso."), true);
  const body = showModal(uiText("Inserisci ") + snippet.name, "ANTEPRIMA DEL COMANDO", null, true);
  modal.classList.add("workflow-modal");
  notice(body, uiText("Destinazione: ") + pane.title.textContent);
  const form = document.createElement("form");
  const names = [...new Set([...snippet.text.matchAll(/\{\{([a-zA-Z0-9_]+)\}\}/g)].map((x) => x[1]))];
  const values = /* @__PURE__ */ new Map();
  for (const name of names) {
    const input = field(form, name, name);
    input.required = true;
    input.maxLength = 4096;
    values.set(name, input);
  }
  const resolved = () => snippet.text.replace(/\{\{([a-zA-Z0-9_]+)\}\}/g, (_, name) => values.get(name)?.value || "");
  const preview = document.createElement("pre");
  preview.className = "snippet-code";
  interfaceAttribute(preview, 'aria-label', "Anteprima del comando");
  preview.textContent = snippet.text;
  form.addEventListener("input", () => preview.textContent = resolved());
  form.append(preview);
  notice(form, uiText("Nessun Invio finale aggiunto. Un testo su più righe può comunque eseguire i comandi intermedi."));
  const actions = document.createElement("div");
  actions.className = "settings-actions";
  const insert = document.createElement("button");
  insert.type = "submit";
  insert.className = "primary";
  insert.textContent = uiText("Inserisci nel terminale");
  actions.append(localizedButton("Annulla", () => modal.close()), insert);
  form.append(actions);
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (panes.get(pane.id) !== pane || !pane.connected) return toast(uiText("Il terminale destinatario non è più connesso."), true);
    const text = resolved();
    if (/[\r\n]/.test(text) && !confirm(uiText("Lo snippet contiene più righe: incollarlo può eseguire i comandi intermedi. Continuare?"))) return;
    insert.disabled = true;
    try {
      await call("input", { sessionId: pane.id, data: text });
      modal.close();
      pane.term.focus();
    } catch {
    } finally {
      insert.disabled = false;
    }
  };
  body.append(form);
}
function renderCommandPalette() {
  const body = showModal(uiText("Centro comandi"), "CERCA E APRI", null, true);
  modal.classList.add("workflow-modal");
  const input = document.createElement("input");
  input.type = "search";
  interfaceAttribute(input, 'placeholder', "Cerca comandi, connessioni o snippet");
  interfaceAttribute(input, 'aria-label', "Cerca comandi, connessioni o snippet");
  body.append(input);
  const list = document.createElement("div");
  list.className = "palette-list";
  body.append(list);
  const commands = [["Azioni", "Nuova connessione", () => profileDialog()], ["Azioni", "Client Key Manager", keysDialog], ["Azioni", "Host Key Manager", hostsDialog], ["Azioni", "Snippet e workspace", snippetsDialog], ["Azioni", "Impostazioni", settingsDialog], ["Azioni", "Aggiornamenti", updatesDialog], ...state.data.profiles.map((p) => ["Connessioni", p.name + " / " + profileEndpoint(p), () => {
    modal.close();
    connect(p);
  }]), ...state.data.snippets.map((s) => ["Snippet", s.name + " / " + s.folder, () => insertSnippet(s), s.text, s.shortcut])];
  function render() {
    list.replaceChildren();
    const query = input.value.trim().toLocaleLowerCase();
    const matches = commands.filter((c) => (c[1] + " " + (c[3] || "")).toLocaleLowerCase().includes(query));
    for (const category of ["Azioni", "Connessioni", "Snippet"]) {
      const items = matches.filter((c) => c[0] === category);
      if (!items.length) continue;
      const h = document.createElement("h3");
      h.textContent = category;
      list.append(h);
      for (const [, label, fn, , shortcut] of items) {
        const b = button(label, fn);
        if (shortcut) {
          const hint = document.createElement("kbd");
          hint.textContent = shortcut;
          b.append(hint);
        }
        list.append(b);
      }
    }
    if (!matches.length) notice(list, uiText("Nessun risultato. Prova un nome, una cartella o parte di un comando."));
  }
  input.oninput = render;
  body.onkeydown = (e) => {
    const buttons = [...list.querySelectorAll("button")];
    if (!buttons.length) return;
    const at = buttons.indexOf(document.activeElement);
    if (e.key === "ArrowDown") {
      e.preventDefault();
      buttons[(at + 1) % buttons.length].focus();
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      buttons[(at - 1 + buttons.length) % buttons.length].focus();
    } else if (e.key === "Enter" && document.activeElement === input) {
      e.preventDefault();
      buttons[0].click();
    }
  };
  render();
  input.focus();
}
document.addEventListener("keydown", (e) => {
  if (e.repeat || e.isComposing || !e.ctrlKey || !e.altKey || !e.shiftKey || e.metaKey || modal.open || !$("sequence").hidden || document.body.classList.contains("startup-covered")) return;
  const target = e.target;
  if (target.closest("input,textarea,select,[contenteditable=true]") && !target.closest(".xterm")) return;
  const key = /^Key[A-Z]$/.test(e.code) ? e.code.slice(3) : /^Digit[0-9]$/.test(e.code) ? e.code.slice(5) : "";
  if (!key) return;
  const matches = state?.data.snippets.filter((s) => s.shortcut === "Ctrl+Alt+Shift+" + key) || [];
  if (matches.length !== 1) return;
  e.preventDefault();
  e.stopImmediatePropagation();
  insertSnippet(matches[0]);
}, true);
