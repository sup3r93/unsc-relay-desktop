"use strict";
async function buildAccessReview(body, text, load, save) {
  const help = document.createElement("p"), status = document.createElement("p"), list = document.createElement("ul"), form = document.createElement("form"), label = document.createElement("label"), interval = document.createElement("select"), verifiedLabel = document.createElement("label"), verified = document.createElement("input"), submit = document.createElement("button"), refresh = document.createElement("button");
  help.textContent = text("Controlla destinatari, permessi e scadenze. Registrare la revisione non modifica gli accessi. Il promemoria appare quando consulti gli archivi; non invia email.");
  status.setAttribute("role", "status");
  label.textContent = text("Ripeti la revisione ogni");
  for (const days of [7, 30, 90]) interval.append(new Option(days + " " + text("giorni"), String(days)));
  label.append(interval);
  verified.type = "checkbox";
  verified.required = true;
  verifiedLabel.append(verified, document.createTextNode(text("Ho verificato tutti gli accessi elencati.")));
  submit.type = "submit";
  submit.textContent = text("Registra revisione");
  refresh.type = "button";
  refresh.textContent = text("Aggiorna elenco");
  form.className = "access-review-form";
  form.append(label, verifiedLabel, submit, refresh);
  body.append(help, status, list, form);
  let intent = "", busy = false;
  async function read() {
    submit.disabled = refresh.disabled = true;
    verified.checked = false;
    try {
      const value = await load();
      if (!body.isConnected) return;
      intent = value.intent;
      interval.value = String(value.intervalDays);
      list.replaceChildren();
      for (const member of value.members) {
        const item = document.createElement("li");
        item.textContent = (member.email || member.account) + " · " + text(member.account === value.ownerAccount ? "Titolare" : member.access === "read" ? "Sola lettura" : "Lettura e scrittura") + " · " + (member.expiresAt ? new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(member.expiresAt)) : text("Senza scadenza"));
        list.append(item);
      }
      status.textContent = text(value.due ? "Revisione accessi necessaria" : "Prossima revisione") + " · " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(value.nextReview));
      submit.disabled = false;
    } catch (error) {
      if (body.isConnected) status.textContent = error.message;
    } finally {
      refresh.disabled = false;
    }
  }
  refresh.onclick = () => {
    if (!busy) void read();
  };
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (busy || !intent || !form.reportValidity()) return;
    busy = true;
    submit.disabled = refresh.disabled = true;
    try {
      await save(Number(interval.value), intent);
      await read();
      if (body.isConnected) status.textContent = text("Revisione registrata.") + " " + status.textContent;
    } catch (error) {
      if (body.isConnected) status.textContent = error.message;
    } finally {
      busy = false;
      submit.disabled = refresh.disabled = false;
    }
  };
  await read();
}
function archiveAccessReviewDialog(archive) {
  const previous = document.activeElement, dialog = document.createElement("dialog"), heading = document.createElement("h2"), close = document.createElement("button"), header = document.createElement("header"), body = document.createElement("div");
  dialog.className = "protected-attachment-dialog";
  dialog.dataset.accessReview = "";
  heading.id = "access-review-" + crypto.randomUUID();
  heading.textContent = uiText("Revisione periodica degli accessi");
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
  void buildAccessReview(body, uiText, () => rpc("archiveAccessReview", { archiveId: archive.id }), (intervalDays, intent) => rpc("archiveAccessReviewed", { archiveId: archive.id, intervalDays, intent }));
}
