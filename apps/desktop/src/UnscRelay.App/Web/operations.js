"use strict";
function operationsDialog() {
  const body = showModal(uiText("Centro operazioni"), uiText("ATTIVITÀ DEL DISPOSITIVO / ACCOUNT SELEZIONATO"), null, true), marker = document.createElement("div"), actions = document.createElement("div"), status = document.createElement("p"), summary = document.createElement("div"), jobs = document.createElement("div"), other = document.createElement("div"), paging = document.createElement("div");
  status.setAttribute("role", "status");
  jobs.className = "operation-cards";
  actions.className = paging.className = "form-actions";
  let page = 0, busy = false, timer, stopped = false;
  const completedLabel = document.createElement("label"), completed = document.createElement("input");
  completed.type = "checkbox";
  completedLabel.append(completed, document.createTextNode(uiText("Includi completati")));
  const refresh = localizedButton("Aggiorna adesso", read);
  actions.append(refresh, completedLabel, localizedButton("Centro sincronizzazione", () => synchronizationCenterDialog()));
  body.append(marker, actions, status, summary, jobs, other, paging);
  const live = () => !stopped && marker.isConnected && modal.open;
  modal.addEventListener("close", () => {
    stopped = true;
    clearTimeout(timer);
    jobs.replaceChildren();
    other.replaceChildren();
  }, { once: true });
  completed.onchange = () => {
    page = 0;
    read();
  };
  async function perform(action, payload) {
    try {
      await rpc(action, payload);
      await read();
    } catch (error) {
      if (live()) status.textContent = error.message;
    }
  }
  function schedule() {
    timer = setTimeout(() => {
      if (!live()) return;
      if (document.hidden) schedule();
      else read();
    }, 3e3);
  }
  async function read() {
    if (!live() || busy) return;
    clearTimeout(timer);
    busy = true;
    refresh.disabled = true;
    completed.disabled = true;
    for (const control of paging.querySelectorAll("button")) control.disabled = true;
    try {
      const value = await rpc("operationsOverview", { page, includeCompleted: completed.checked });
      if (!live()) return;
      summary.replaceChildren();
      const sync = document.createElement("p");
      sync.textContent = uiText("Sincronizzazione") + ": " + uiText(value.sync.busy ? "In corso" : value.sync.conflict ? "Conflitto" : value.sync.meteredConnection ? "Automatica in pausa" : "Nessuna sincronizzazione in corso");
      summary.append(sync);
      jobs.replaceChildren();
      for (const job of value.jobs) {
        const card = document.createElement("section"), title = document.createElement("h3"), detail = document.createElement("p"), progress = document.createElement("progress"), controls = document.createElement("div");
        card.className = "settings-card";
        title.textContent = (job.direction === "bridge" ? "↔ " : job.direction === "upload" ? "↑ " : "↓ ") + job.name;
        detail.textContent = job.host + " · " + uiText(job.status);
        progress.max = 100;
        progress.value = Math.max(0, Math.min(100, job.progress));
        progress.setAttribute("aria-label", uiText("Avanzamento") + " · " + job.name);
        controls.className = "form-actions";
        if (["In coda", "Trasferimento", "Pausa richiesta", "Attesa connessione"].includes(job.status)) controls.append(localizedButton("Pausa", () => perform("filePause", { id: job.id })));
        if (["In pausa", "Errore"].includes(job.status)) controls.append(localizedButton("Riprendi", () => perform("fileResume", { id: job.id })));
        card.append(title, detail, progress, controls);
        if (job.error) {
          const error = document.createElement("p");
          error.textContent = uiText(job.error);
          card.append(error);
        }
        jobs.append(card);
      }
      if (!value.jobs.length) {
        const empty = document.createElement("p");
        empty.textContent = uiText("Nessun trasferimento da mostrare.");
        jobs.append(empty);
      }
      other.replaceChildren();
      if (value.plainExport) {
        const row = document.createElement("p");
        row.textContent = uiText("Esportazione in chiaro") + " · " + uiText({ ready: "Archivio pronto", email: "Attesa codice email", verification: "Verifica identità", preparing: "Preparazione archivio", saving: "Salvataggio in corso", saved: "Esportazione completata" }[value.plainExport.phase] || "In corso");
        other.append(row, localizedButton("Annulla esportazione", () => perform("archivePlainCancel", { token: value.plainExport.token })));
      }
      for (const search of value.searches) other.append(button(uiText("Annulla ricerca") + " · " + search.sessionId, () => perform("remoteSearchCancel", { sessionId: search.sessionId, requestId: search.id })));
      for (const scanId of value.folderScans) other.append(localizedButton("Annulla confronto cartelle", () => perform("folderCancel", { scanId })));
      for (const [label2, count] of [["Esportazioni di file", value.otherExports], ["Editor con modifiche non salvate", value.editors.filter((edit) => edit.dirty).length], ["Salvataggi editor in corso", value.editors.filter((edit) => edit.busy).length], ["Altre operazioni in corso", value.otherOperations]]) {
        if (count) {
          const row = document.createElement("p");
          row.textContent = uiText(label2) + ": " + count;
          other.append(row);
        }
      }
      if (value.legacyTransfer) {
        const row = document.createElement("p");
        row.textContent = uiText("Trasferimento della sessione in corso.");
        other.append(row, localizedButton("Annulla trasferimento", () => perform("cancelTransfer", {})));
      }
      paging.replaceChildren();
      const previous = localizedButton("Precedente", () => {
        page--;
        read();
      }), next = localizedButton("Successiva", () => {
        page++;
        read();
      });
      previous.disabled = page === 0;
      next.disabled = !value.hasMore;
      const label = document.createElement("span");
      label.textContent = uiText("Pagina") + " " + (page + 1);
      paging.append(previous, label, next);
      status.textContent = uiText("Ultimo aggiornamento") + ": " + new Intl.DateTimeFormat(document.documentElement.lang, { timeStyle: "short" }).format(new Date(value.checkedAt));
    } catch (error) {
      if (live()) status.textContent = error.message;
    } finally {
      busy = false;
      refresh.disabled = false;
      completed.disabled = false;
      if (live()) schedule();
    }
  }
  void read();
}
