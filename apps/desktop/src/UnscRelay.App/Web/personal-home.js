"use strict";
function personalHome(host, text, adapter) {
  const all = ["archives", "activity", "tickets", "notifications"], labels = { archives: "Archivi collegati", activity: "Attività", tickets: "Assistenza", notifications: "Notifiche" }, section = document.createElement("section"), heading = document.createElement("h2"), actions = document.createElement("div"), grid = document.createElement("div"), status = document.createElement("p");
  section.className = "personal-home";
  section.dataset.personalHome = "";
  heading.textContent = text("La tua panoramica");
  actions.className = "form-actions";
  grid.className = "personal-home-grid";
  status.setAttribute("role", "status");
  let cards = [...all], value = null, busy = false;
  function button(label, run) {
    const b = document.createElement("button");
    b.type = "button";
    b.textContent = text(label);
    b.onclick = () => void Promise.resolve().then(run).catch((error) => status.textContent = error.message);
    return b;
  }
  const refresh = localizedButton("Aggiorna riepiloghi", async () => {
    if (busy) return;
    busy = true;
    refresh.disabled = true;
    status.textContent = text("Caricamento…");
    try {
      const next = await adapter.load();
      if (!section.isConnected) return;
      value = next;
      render();
      status.textContent = text("Ultimo aggiornamento") + ": " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(next.checkedAt));
    } finally {
      busy = false;
      refresh.disabled = false;
    }
  });
  const configure = localizedButton("Personalizza panoramica", () => {
    const view = adapter.dialog(text("Personalizza panoramica")), form = document.createElement("form"), fieldset = document.createElement("fieldset"), legend = document.createElement("legend");
    legend.textContent = text("Riepiloghi visibili");
    fieldset.append(legend);
    const choices = [];
    for (const key of all) {
      const label = document.createElement("label"), input = document.createElement("input");
      input.type = "checkbox";
      input.checked = cards.includes(key);
      label.append(input, document.createTextNode(text(labels[key])));
      fieldset.append(label);
      choices.push([key, input]);
    }
    const state2 = document.createElement("p");
    state2.setAttribute("role", "status");
    const save = localizedButton("Salva preferenze", () => {
    });
    save.type = "submit";
    form.append(fieldset, save, state2);
    form.onsubmit = async (e) => {
      e.preventDefault();
      save.disabled = true;
      try {
        const next = choices.filter(([, input]) => input.checked).map(([key]) => key);
        await adapter.save(next);
        cards = next;
        render();
        view.close();
      } catch (error) {
        state2.textContent = error.message;
        save.disabled = false;
      }
    };
    view.body.append(form);
  });
  actions.append(refresh, configure);
  section.append(heading, actions, status, grid);
  host.append(section);
  function render() {
    grid.replaceChildren();
    for (const key of cards) {
      const card = document.createElement("section"), title = document.createElement("h3"), list = document.createElement("ul");
      card.className = "personal-home-card";
      title.textContent = text(labels[key]);
      const lines = value ? key === "activity" ? adapter.activity() : key === "archives" ? (value.archives || []).slice(0, 5).map((item) => item.name) : key === "tickets" ? (value.tickets || []).slice(0, 5).map((item) => item.title) : (value.notifications || []).filter((item) => !item.readAt).slice(0, 5).map((item) => item.title) : [];
      if (!value) {
        const empty = document.createElement("p");
        empty.textContent = text("Aggiorna i riepiloghi quando sei online.");
        card.append(title, empty);
      } else {
        for (const line of lines) {
          const li = document.createElement("li");
          li.textContent = line;
          list.append(li);
        }
        const note = document.createElement("p");
        note.textContent = text(lines.length ? "Mostra fino a cinque elementi recenti." : "Nessun elemento da mostrare.");
        card.append(title, list, note);
      }
      if (adapter.open[key]) card.append(localizedButton("Apri sezione", () => adapter.open[key]()));
      grid.append(card);
    }
  }
  render();
  Promise.resolve().then(() => adapter.settings()).then((saved) => {
    if (!section.isConnected) return;
    if (Array.isArray(saved) && saved.every((key) => all.includes(key))) {
      cards = [...new Set(saved)];
      render();
    }
  }).catch((error) => {
    if (section.isConnected) status.textContent = error.message;
  });
  return { refresh: () => refresh.click(), destroy: () => section.remove() };
}
let personalHomeOwner = null, personalHomeView = null;
function renderPersonalHome() {
  const owner = state?.account?.owner || "local", binding = owner + ":" + String(!!state?.appLocked || !state?.vault?.unlocked);
  if (binding === personalHomeOwner) return;
  personalHomeOwner = binding;
  personalHomeView?.destroy();
  personalHomeView = personalHome($("welcome"), uiText, { settings: async () => state.data.settings.homeCards, save: async (cards) => update(await rpc("homeSettings", { cards })), load: () => rpc("homeOverview"), activity: () => [uiText("Sessioni aperte") + ": " + panes.size, uiText("Profili") + ": " + state.data.profiles.length], dialog: (title) => ({ body: showModal(title, "", null, true), close: () => modal.close() }), open: { archives: () => archivesDialog(), activity: () => synchronizationCenterDialog(), tickets: () => supportDialog() } });
}
window.addEventListener("relay-language-changed", () => {
  personalHomeOwner = null;
  if (state?.data) renderPersonalHome();
});
