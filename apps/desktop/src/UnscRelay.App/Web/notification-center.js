async function mountNotificationCenter(root, text, adapter) {
  const categories = ["support", "sync", "sharing", "system"], labels = { security: "Sicurezza", support: "Assistenza", sync: "Sincronizzazione", sharing: "Condivisioni", system: "Servizio" }, heading = document.createElement("h2"), controls = document.createElement("div"), status = document.createElement("p"), summary = document.createElement("p"), list = document.createElement("div"), settings = document.createElement("details"), legend = document.createElement("summary"), form = document.createElement("form"), fieldset = document.createElement("fieldset"), group = document.createElement("legend");
  root.dataset.notificationCenter = "";
  heading.textContent = text("Le tue notifiche");
  controls.className = "form-actions";
  list.className = "notification-list";
  status.setAttribute("role", "status");
  status.setAttribute("aria-live", "polite");
  legend.textContent = text("Personalizza notifiche");
  settings.append(legend, form);
  group.textContent = text("Categorie visibili");
  fieldset.append(group);
  const choices = /* @__PURE__ */ new Map();
  function choice(label, checked) {
    const row = document.createElement("label"), input = document.createElement("input");
    row.className = "toggle-row";
    input.type = "checkbox";
    input.checked = checked;
    row.append(input, document.createTextNode(text(label)));
    return { row, input };
  }
  const security = choice("Sicurezza: sempre visibile", true);
  security.input.disabled = true;
  fieldset.append(security.row);
  for (const category2 of categories) {
    const item = choice(labels[category2], true);
    choices.set(category2, item.input);
    fieldset.append(item.row);
  }
  const quiet = choice("Modalit\xE0 silenziosa", false), summaries = choice("Raggruppa per categoria", false), help = document.createElement("p");
  help.textContent = text("La modalit\xE0 silenziosa raccoglie le notifiche ordinarie in un riepilogo. Gli avvisi di sicurezza restano visibili. Nessuna notifica viene eliminata.");
  form.append(fieldset, quiet.row, summaries.row, help);
  const submit = document.createElement("button");
  submit.type = "submit";
  submit.textContent = text("Salva preferenze notifiche");
  form.append(submit);
  function button(label, run) {
    const value = document.createElement("button");
    value.type = "button";
    value.textContent = text(label);
    value.onclick = () => void perform(run);
    return value;
  }
  const refresh = button("Aggiorna elenco", load), all = button("Segna tutto come letto", async () => {
    await adapter.readAll();
    adapter.changed();
    await load();
    status.textContent = text("Tutte le notifiche sono state segnate come lette.");
  }), expand = button("Mostra notifiche ordinarie", () => {
    expanded = !expanded;
    expand.textContent = text(expanded ? "Nascondi notifiche ordinarie" : "Mostra notifiche ordinarie");
    render();
  });
  all.id = "notifications-read-all";
  status.id = "notifications-status";
  controls.append(refresh, all);
  root.append(heading, controls, status, summary, settings, expand, list);
  let state = null, preferences = { categories: [...categories], quiet: false, summaries: false }, version = 0, busy = false, expanded = false, initialized = false;
  async function perform(run) {
    if (busy) return;
    busy = true;
    root.setAttribute("aria-busy", "true");
    for (const control of root.querySelectorAll("button,input")) control.disabled = true;
    try {
      await run();
    } catch (error) {
      if (root.isConnected) status.textContent = error instanceof Error ? error.message : String(error);
    } finally {
      busy = false;
      root.removeAttribute("aria-busy");
      for (const control of root.querySelectorAll("button,input")) control.disabled = false;
      security.input.disabled = true;
      all.disabled = !state?.summary.unread;
    }
  }
  async function load() {
    const value = await adapter.load();
    if (!root.isConnected) return;
    state = value;
    if (!initialized) {
      version = value.workspace.version;
      const selected = value.workspace.settings.notificationDisplay;
      if (selected && Array.isArray(selected.categories)) {
        preferences = { categories: selected.categories.filter((value2) => categories.includes(value2)), quiet: selected.quiet === true, summaries: selected.summaries === true };
      }
      for (const [key, input] of choices) input.checked = preferences.categories.includes(key);
      quiet.input.checked = preferences.quiet;
      summaries.input.checked = preferences.summaries;
      initialized = true;
    }
    render();
  }
  function category(row) {
    return row.category && categories.includes(row.category) ? row.category : "security";
  }
  function card(row) {
    const item = document.createElement("article"), content = document.createElement("div"), time = document.createElement("small"), title = document.createElement("h3"), description = document.createElement("p"), open = document.createElement("button");
    item.className = "panel notification" + (row.readAt ? "" : " unread");
    item.dataset.category = category(row);
    time.textContent = adapter.date(row.createdAt);
    title.textContent = row.title;
    description.textContent = row.body;
    description.className = "user-content";
    open.type = "button";
    open.textContent = text("Apri");
    open.onclick = () => adapter.open(row);
    content.append(time, title, description, open);
    item.append(content);
    if (!row.readAt) item.append(button("Segna come letta", async () => {
      await adapter.read(row.id);
      adapter.changed();
      await load();
    }));
    return item;
  }
  function render() {
    if (!state) return;
    const visible = state.rows.filter((row) => category(row) === "security" || preferences.categories.includes(category(row))), deferred = visible.filter((row) => category(row) !== "security"), shown = preferences.quiet && !expanded ? visible.filter((row) => category(row) === "security") : visible;
    summary.textContent = text("Non lette") + ": " + state.summary.unread + " \xB7 " + text("Ultime notifiche caricate") + ": " + state.rows.length + " \xB7 " + text("Notifiche ordinarie nel riepilogo") + ": " + deferred.length;
    expand.hidden = !preferences.quiet || !deferred.length;
    all.disabled = state.summary.unread === 0;
    list.replaceChildren();
    if (!shown.length) {
      const empty = document.createElement("p");
      empty.textContent = text("Nessuna notifica da mostrare con queste preferenze.");
      list.append(empty);
    }
    if (preferences.summaries) {
      for (const key of ["security", ...categories]) {
        const rows = shown.filter((row) => category(row) === key);
        if (!rows.length) continue;
        const group2 = document.createElement("details"), title = document.createElement("summary");
        group2.open = key === "security";
        title.textContent = text(labels[key]) + " \xB7 " + rows.length;
        group2.append(title, ...rows.map(card));
        list.append(group2);
      }
    } else list.append(...shown.map(card));
  }
  form.onsubmit = (event) => {
    event.preventDefault();
    void perform(async () => {
      if (!state) throw Error(text("Aggiorna l\u2019elenco prima di salvare."));
      const desired = { categories: [...choices].filter(([, input]) => input.checked).map(([key]) => key), quiet: quiet.input.checked, summaries: summaries.input.checked };
      await adapter.save(desired, version);
      if (!root.isConnected) return;
      version++;
      preferences = desired;
      expanded = false;
      render();
      status.textContent = text("Preferenze notifiche salvate.");
    });
  };
  await perform(load);
}
