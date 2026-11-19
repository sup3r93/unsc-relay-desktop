"use strict";
async function buildIntegrityView(body, text, check) {
  const help = document.createElement("p"), status = document.createElement("p"), summary = document.createElement("dl"), findings = document.createElement("ul"), run = document.createElement("button");
  help.textContent = text("Verifica autenticità, formato e riferimenti della copia cifrata selezionata. Non verifica le credenziali sui server e non ripara né modifica i dati.");
  status.setAttribute("role", "status");
  status.setAttribute("aria-live", "polite");
  run.type = "button";
  run.textContent = text("Verifica copia cifrata");
  body.append(help, run, status, summary, findings);
  run.onclick = async () => {
    if (run.disabled) return;
    run.disabled = true;
    status.textContent = text("Verifica in corso…");
    summary.replaceChildren();
    findings.replaceChildren();
    try {
      const value = await check();
      if (!body.isConnected) return;
      const report = value.report;
      status.textContent = text(report.warningCount ? "Verifica completata con riferimenti da controllare." : "Autenticità e struttura della copia verificate.");
      const add = (label, value2) => {
        const dt = document.createElement("dt"), dd = document.createElement("dd");
        dt.textContent = text(label);
        dd.textContent = String(value2);
        summary.append(dt, dd);
      };
      add("Revisione", value.revision);
      add("Copia verificata", text(value.pending ? "Copia locale con modifiche da inviare" : value.offline ? "Copia locale offline" : "Copia cifrata caricata"));
      add("Data della verifica", new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(report.checkedAt)));
      add("Elementi", report.records);
      add("Connessioni", report.profiles);
      add("Chiavi host", report.hostKeys);
      add("Avvisi", report.warningCount);
      const labels = { "missing-key": "Chiave SSH non presente", "missing-password": "Password non presente", "missing-profile": "Connessione non presente", "missing-profile-id": "Identificativo connessione non presente" };
      for (const finding of report.findings) {
        const li = document.createElement("li");
        li.textContent = text(labels[finding.code] || "Riferimento da controllare") + " · " + finding.subject;
        findings.append(li);
      }
      if (report.warningCount > report.findings.length) {
        const note = document.createElement("li");
        note.textContent = text("Sono mostrati solo i primi 100 avvisi.");
        findings.append(note);
      }
    } catch (error) {
      if (body.isConnected) status.textContent = error.message || text("Verifica non riuscita. La copia non è stata modificata.");
    } finally {
      run.disabled = false;
    }
  };
}
function archiveIntegrityDialog(archive) {
  const previous = document.activeElement, dialog = document.createElement("dialog"), heading = document.createElement("h2"), close = document.createElement("button"), header = document.createElement("header"), body = document.createElement("div");
  dialog.className = "protected-attachment-dialog";
  dialog.dataset.archiveIntegrity = "";
  heading.id = "integrity-" + crypto.randomUUID();
  heading.textContent = uiText("Verifica integrità archivio");
  dialog.setAttribute("aria-labelledby", heading.id);
  close.type = "button";
  close.textContent = "×";
  interfaceAttribute(close, 'aria-label', "Chiudi finestra");
  close.onclick = () => dialog.close();
  header.append(heading, close);
  body.className = "attachment-preview-body";
  const name = document.createElement("p");
  name.textContent = archive.name;
  body.append(name);
  dialog.append(header, body);
  document.body.append(dialog);
  const parentClose = () => dialog.close();
  modal.addEventListener("close", parentClose, { once: true });
  dialog.addEventListener("close", () => {
    modal.removeEventListener("close", parentClose);
    dialog.remove();
    if (previous?.isConnected) previous.focus();
  }, { once: true });
  dialog.showModal();
  close.focus();
  void rpc("archiveIntegrityLast", { archiveId: archive.id, localOnly: !!archive.offline }).then((last) => {
    if (last && body.isConnected) {
      const previous2 = document.createElement("p");
      previous2.textContent = uiText("Ultima verifica completata su questo dispositivo") + " · " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(last.checkedAt)) + " · " + uiText("Revisione") + " " + last.revision + " · " + uiText("Avvisi") + " " + last.warnings;
      body.prepend(previous2);
    }
  }).catch(() => {
  });
  void buildIntegrityView(body, uiText, () => rpc("archiveIntegrity", { archiveId: archive.id, localOnly: !!archive.offline }));
}
