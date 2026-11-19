"use strict";
async function mountAccountErasure(root) {
  const heading = document.createElement("h3");
  heading.textContent = uiText("Dati e cancellazione dell’account");
  const description = document.createElement("p");
  description.textContent = uiText("Esporta gli archivi o trasferiscine la proprietà prima di richiedere la cancellazione. Le copie scaricate e i dispositivi offline non possono essere cancellati a distanza.");
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  root.replaceChildren(heading, description, status);
  async function load() {
    try {
      const result = await rpc("accountErasureStatus"), value = result.status;
      root.querySelector("[data-erasure-actions]")?.remove();
      status.textContent = value.state === "pending" ? uiText("Cancellazione prevista dal ") + erasureDate(value.executeAfter) + ". Gli archivi sono in sola lettura; puoi annullare fino all’avvio effettivo della distruzione." : value.state === "cancelled" ? uiText("La precedente richiesta è stata annullata.") : value.state === "none" ? uiText("Nessuna cancellazione programmata.") : "La cancellazione è iniziata e l’accesso è stato revocato.";
      if (["none", "cancelled", "pending"].includes(value.state)) {
        const actions = document.createElement("div");
        actions.dataset.erasureActions = "";
        actions.append(button(value.state === "pending" ? uiText("Annulla richiesta di cancellazione") : uiText("Richiedi cancellazione completa"), () => accountErasureDialog(value.state === "pending", load)));
        root.append(actions);
      }
    } catch (error) {
      status.textContent = error.message || uiText("Stato non disponibile.");
    }
  }
  await load();
}
function erasureDate(value) {
  return new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "long", timeStyle: "short" }).format(new Date(value));
}
async function accountErasureDialog(cancelling, refresh) {
  let opened;
  try {
    opened = await rpc("accountErasureReview", { cancel: cancelling });
  } catch (error) {
    toast(error.message, true);
    return;
  }
  const previous = document.activeElement, review = opened.review, token = opened.token, dialog = document.createElement("dialog");
  dialog.dataset.accountErasure = token;
  dialog.className = "support-create-dialog account-personal-dialog";
  dialog.setAttribute("aria-labelledby", "account-erasure-heading");
  dialog.setAttribute("aria-describedby", "account-erasure-description");
  const heading = document.createElement("h2");
  heading.id = "account-erasure-heading";
  heading.textContent = cancelling ? uiText("Annulla la cancellazione") : uiText("Cancellazione completa dell’account");
  const help = document.createElement("p");
  help.id = "account-erasure-description";
  help.textContent = cancelling ? uiText("Una nuova verifica 2FA e un nuovo codice email proteggono l’annullamento.") : "La distruzione potrà iniziare dopo 30 giorni completi. Verranno rimossi l’identità, gli archivi di tua proprietà, i dati associati e i tuoi accessi agli archivi di altri titolari. Questi ultimi archivi saranno conservati. Le copie esportate e i dispositivi offline non possono essere cancellati a distanza. Le copie di backup seguono la conservazione indicata nel centro privacy.";
  dialog.append(heading, help);
  if (cancelling) {
    const date = document.createElement("p");
    date.textContent = uiText("Cancellazione prevista dal ") + erasureDate(review.executeAfter);
    dialog.append(date);
  } else {
    const title = document.createElement("h3");
    title.textContent = uiText("Archivi che verranno distrutti (") + review.archives.length + ")";
    const list = document.createElement("ul");
    for (const archive of review.archives) {
      const row = document.createElement("li");
      row.textContent = archive.name + uiText(" · revisione ") + archive.version + uiText(" · altri utenti interessati: ") + archive.otherMembers;
      list.append(row);
    }
    dialog.append(title, list);
  }
  const form = document.createElement("form");
  const label = document.createElement("label");
  label.className = "check";
  const consent = document.createElement("input");
  consent.type = "checkbox";
  consent.required = true;
  label.append(consent, cancelling ? uiText("Voglio mantenere l’account e i suoi dati.") : "Ho letto l’elenco e richiedo la cancellazione dell’account e dei dati associati.");
  form.append(label);
  const code = field(form, uiText("Codice ricevuto via email"), "code");
  code.inputMode = "numeric";
  code.autocomplete = "one-time-code";
  code.pattern = "[0-9 -]{8,16}";
  code.maxLength = 16;
  code.closest("label").hidden = true;
  const destination = document.createElement("p");
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  status.tabIndex = -1;
  const actions = document.createElement("div");
  actions.className = "form-actions";
  let busy = false, requested = false, finished = false;
  const close = localizedButton("Annulla", () => dialog.close()), submit = localizedButton("Verifica 2FA e invia codice email", () => {
  }, "primary");
  submit.type = "submit";
  actions.append(close, submit);
  form.append(destination, actions, status);
  dialog.append(form);
  document.body.append(dialog);
  dialog.showModal();
  consent.focus();
  dialog.addEventListener("close", () => {
    code.value = "";
    dialog.remove();
    void rpc("accountErasureClose", { token }).catch(() => {
    });
    void refresh();
    if (previous?.isConnected) previous.focus();
  }, { once: true });
  form.onsubmit = async (event) => {
    event.preventDefault();
    if (busy || finished) return;
    busy = true;
    dialog.setAttribute("aria-busy", "true");
    submit.disabled = consent.disabled = code.disabled = true;
    status.textContent = uiText("Conferma la tua identità nella finestra protetta…");
    try {
      if (!requested) {
        const value = await rpc("accountErasureBegin", { token });
        if (!dialog.isConnected) return;
        requested = true;
        destination.textContent = uiText("Codice inviato a ") + value.email + uiText(" · scadenza ") + erasureDate(value.expiresAt);
        code.required = true;
        code.closest("label").hidden = false;
        submit.textContent = cancelling ? uiText("Conferma annullamento") : uiText("Programma cancellazione tra 30 giorni");
        status.textContent = cancelling ? "La richiesta rimane attiva finché non confermi l’annullamento con il codice email." : "Inserisci il codice email. Nessuna cancellazione è stata ancora programmata.";
      } else {
        const result = await rpc("accountErasureConfirm", { token, code: code.value });
        if (!dialog.isConnected) return;
        finished = true;
        code.value = "";
        code.required = false;
        code.closest("label").hidden = true;
        submit.hidden = true;
        close.textContent = uiText("Chiudi");
        status.textContent = cancelling ? uiText("Richiesta annullata. Il tuo account e i dati sono conservati.") : uiText("Richiesta registrata. La cancellazione potrà iniziare dal ") + erasureDate(result.status.executeAfter) + ". Puoi annullarla fino all’avvio effettivo della distruzione.";
        if (!result.notificationQueued) status.textContent += " La notifica aggiuntiva non è stata accodata; lo stato qui mostrato conferma comunque l’esito.";
        await refresh();
        status.focus();
      }
    } catch (error) {
      if (dialog.isConnected) status.textContent = error.message + " Controlla lo stato prima di riprovare.";
    } finally {
      busy = false;
      dialog.setAttribute("aria-busy", "false");
      submit.disabled = code.disabled = false;
      consent.disabled = requested;
      if (dialog.isConnected && requested && !finished) code.focus();
    }
  };
}
