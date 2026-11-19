"use strict";
function remoteSearchDialog(pane) {
  const body = showModal(uiText("Ricerca remota dei file"), pane.profile.name, null, true), marker = document.createElement("div"), form = document.createElement("form"), help = document.createElement("p"), status = document.createElement("p"), results = document.createElement("ul"), actions = document.createElement("div");
  help.textContent = uiText("Ricerca in sola lettura nella cartella scelta: fino a 10.000 elementi, 500 risultati, 8 livelli e 30 secondi. Non legge il contenuto dei file e salta i collegamenti simbolici rilevati. Non avvia comandi sul server.");
  status.setAttribute("role", "status");
  form.className = "remote-search-form";
  results.className = "remote-search-results";
  body.append(marker, help, form, actions, status, results);
  const root = field(form, uiText("Cartella da cercare"), "root", pane.remotePath), name = field(form, uiText("Il nome contiene"), "name", ""), extension = field(form, uiText("Estensione (facoltativa)"), "extension", ""), minimum = field(form, uiText("Dimensione minima (MiB)"), "minimum", "", "number"), maximum = field(form, uiText("Dimensione massima (MiB)"), "maximum", "", "number"), after = field(form, uiText("Modificato dal (ora locale)"), "after", "", "datetime-local"), before = field(form, uiText("Modificato fino al (ora locale)"), "before", "", "datetime-local");
  root.required = true;
  root.maxLength = 4096;
  name.maxLength = 160;
  extension.maxLength = 30;
  for (const input of [minimum, maximum]) {
    input.min = 0;
    input.max = 1048576;
    input.step = "any";
  }
  let active = null, sequence = 0;
  const search = localizedButton("Avvia ricerca", () => {
  }, "primary");
  search.type = "submit";
  form.append(search);
  const cancel = localizedButton("Annulla ricerca", () => {
    if (active) {
      sequence++;
      call("remoteSearchCancel", { sessionId: pane.id, requestId: active }).catch(() => {
      });
      status.textContent = uiText("Annullamento richiesto.");
      cancel.disabled = true;
    }
  });
  cancel.disabled = true;
  actions.append(cancel);
  modal.addEventListener("close", () => {
    sequence++;
    if (active) rpc("remoteSearchCancel", { sessionId: pane.id, requestId: active }).catch(() => {
    });
    results.replaceChildren();
  }, { once: true });
  form.onsubmit = async (event) => {
    event.preventDefault();
    if (active || !form.reportValidity()) return;
    const requestId = crypto.randomUUID(), run = ++sequence;
    active = requestId;
    search.disabled = true;
    cancel.disabled = false;
    results.replaceChildren();
    status.textContent = uiText("Ricerca in corso…");
    try {
      const query = { root: root.value, name: name.value, extension: extension.value, minimumBytes: minimum.value === "" ? null : Math.round(Number(minimum.value) * 1048576), maximumBytes: maximum.value === "" ? null : Math.round(Number(maximum.value) * 1048576), modifiedAfter: after.value ? new Date(after.value).toISOString() : null, modifiedBefore: before.value ? new Date(before.value).toISOString() : null };
      const value = await rpc("remoteSearch", { sessionId: pane.id, requestId, query });
      if (run !== sequence || !marker.isConnected) return;
      for (const file of value.files) {
        const row = document.createElement("li"), title = document.createElement("strong"), details = document.createElement("span");
        title.textContent = file.name;
        details.textContent = file.path + " · " + file.size + " B";
        const open = localizedButton("Apri cartella", async () => {
          modal.close();
          await loadFileSide(pane, "remote", file.path.slice(0, file.path.lastIndexOf("/")) || "/");
        });
        row.append(title, details, open);
        results.append(row);
      }
      status.textContent = uiText(value.truncated ? "Ricerca parziale: raggiunto un limite. Restringi i filtri o la cartella." : "Ricerca completata.") + " " + uiText("Risultati") + ": " + value.files.length + " · " + uiText("Elementi esaminati") + ": " + value.examined + " · " + uiText("Collegamenti saltati") + ": " + value.skippedLinks + " · " + uiText("Cartelle non leggibili") + ": " + value.unreadable;
    } catch (error) {
      if (marker.isConnected && run === sequence) status.textContent = error.message;
    } finally {
      active = null;
      if (marker.isConnected) {
        search.disabled = false;
        cancel.disabled = true;
        if (run !== sequence) status.textContent = uiText("Ricerca annullata.");
      }
    }
  };
}
