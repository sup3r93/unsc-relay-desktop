"use strict";
function renderConnectionViews() {
  const host = $("connection-views");
  if (!host) return;
  const previous = host.querySelector("select")?.value || "";
  host.replaceChildren();
  host.className = "saved-connection-views";
  const label = document.createElement("label"), select = document.createElement("select");
  label.htmlFor = "connection-view-choice";
  label.textContent = uiText("Ricerche salvate");
  select.id = label.htmlFor;
  select.append(new Option(uiText("Ricerca libera"), ""));
  for (const view of state?.data.connectionViews || []) select.append(new Option(view.name, view.id));
  select.value = (state?.data.connectionViews || []).some((view) => view.id === previous) ? previous : "";
  const save = localizedButton("Salva ricerca", () => saveSearch()), remove = localizedButton("Rimuovi vista", async () => {
    if (!select.value) return;
    if (!await call("confirmAction", { text: uiText("Rimuovere la vista salvata? I dati e le connessioni restano invariati.") })) return;
    update(await call("connectionViewDelete", { id: select.value }));
  });
  remove.disabled = !select.value;
  select.onchange = () => {
    const view = (state.data.connectionViews || []).find((value) => value.id === select.value);
    if (view) {
      $("profile-search").value = view.query;
      renderProfiles();
    }
    remove.disabled = !view;
  };
  host.append(label, select, save, remove);
  const search = $("profile-search");
  if (!search.dataset.savedViewHook) {
    search.dataset.savedViewHook = "true";
    search.addEventListener("input", () => {
      const select2 = $("connection-view-choice");
      if (select2) select2.value = "";
      const remove2 = $("connection-views").querySelector("[data-remove-view]");
      if (remove2) remove2.disabled = true;
    });
  }
  remove.dataset.removeView = "true";
  function saveSearch() {
    const query = search.value, body = showModal(uiText("Salva ricerca"), uiText("VISTE PERSONALI"), null, true), form = document.createElement("form"), label2 = document.createElement("label"), name = document.createElement("input"), description = document.createElement("p"), status = document.createElement("p");
    name.id = "saved-search-name";
    name.required = true;
    name.maxLength = 80;
    label2.htmlFor = name.id;
    label2.textContent = uiText("Nome della vista");
    name.value = query.slice(0, 80);
    description.textContent = uiText("La ricerca viene conservata nel tuo profilo e nelle copie cifrate sincronizzate. Non inserire segreti.");
    const preview = document.createElement("p");
    preview.className = "user-content";
    preview.textContent = query || uiText("Tutte le connessioni");
    status.setAttribute("role", "status");
    const submit = localizedButton("Salva vista", () => {
    }, "primary");
    submit.type = "submit";
    form.append(label2, name, description, preview, submit, status);
    body.append(form);
    form.onsubmit = async (event) => {
      event.preventDefault();
      if (!form.reportValidity()) return;
      submit.disabled = true;
      try {
        update(await call("connectionViewSave", { view: { id: crypto.randomUUID(), name: name.value.trim(), query } }));
        if (form.isConnected) {
          modal.close();
          toast(uiText("Vista salvata."));
        }
      } catch (error) {
        if (form.isConnected) status.textContent = error.message;
      } finally {
        submit.disabled = false;
      }
    };
    name.focus();
  }
}
