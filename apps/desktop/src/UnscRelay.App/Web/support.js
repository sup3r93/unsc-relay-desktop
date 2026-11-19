"use strict";
let supportEpoch = 0;
async function supportDialog() {
  const epoch = ++supportEpoch;
  const body = showModal(uiText("Assistenza"), "UNSC RELAY / SUPPORTO", null, true);
  notice(body, uiText("Scrivi titolo, descrizione e risposte in inglese. Non inserire password, chiavi private o token. Nessun allegato viene inviato automaticamente."));
  const actions = document.createElement("div");
  actions.className = "form-actions";
  const list = document.createElement("div");
  list.className = "support-ticket-list";
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  actions.append(localizedButton("Nuova segnalazione", () => createSupportTicket(refresh)), localizedButton("Aggiorna elenco", refresh));
  body.append(actions, status, list);
  async function refresh() {
    status.textContent = uiText("Caricamento…");
    try {
      const tickets = await rpc("ticketList");
      if (!list.isConnected || epoch !== supportEpoch) return;
      list.replaceChildren();
      for (const ticket of tickets) {
        const row = button("", () => supportTicketDialog(ticket.id));
        row.className = "support-ticket-row";
        const title = document.createElement("strong");
        title.textContent = ticket.title;
        const detail = document.createElement("small");
        detail.textContent = ({ open: "Aperto", in_progress: "In lavorazione", waiting: "In attesa", resolved: "Risolto", closed: "Chiuso" }[ticket.status] || ticket.status) + " · " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "medium" }).format(new Date(ticket.updatedAt));
        row.append(title, detail);
        list.append(row);
      }
      status.textContent = tickets.length ? "" : "Non ci sono segnalazioni.";
    } catch (e) {
      status.textContent = e.message;
    }
  }
  await refresh();
}
function createSupportTicket(refresh) {
  const previous = document.activeElement, dialog = document.createElement("dialog");
  dialog.className = "support-create-dialog";
  dialog.setAttribute("aria-labelledby", "support-create-title");
  const title = document.createElement("h2");
  title.id = "support-create-title";
  title.textContent = uiText("Nuova segnalazione");
  const help = document.createElement("p");
  help.textContent = uiText("Scrivi la segnalazione in inglese. Controllala prima di inviarla. Non includere credenziali.");
  help.lang = "en";
  dialog.append(title, help);
  const form = document.createElement("form");
  let dirty = false, busy = false;
  function field(label, name, kind = "input") {
    const wrapper = document.createElement("label");
    wrapper.textContent = label;
    const control = document.createElement(kind);
    control.name = name;
    wrapper.append(control);
    form.append(wrapper);
    return control;
  }
  const subject = field("Titolo in inglese", "title");
  subject.required = true;
  subject.minLength = 4;
  subject.maxLength = 160;
  subject.lang = "en";
  const category = field("Categoria", "category", "select");
  for (const [value, text] of [["technical", "Bug o problema tecnico"], ["account", "Account e accesso"], ["sync", "Sincronizzazione"], ["suggestion", "Suggerimento"], ["other", "Altro"]]) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = text;
    category.append(option);
  }
  const description = field("Descrizione in inglese", "body", "textarea");
  description.required = true;
  description.minLength = 4;
  description.maxLength = 8e3;
  description.rows = 7;
  description.lang = "en";
  const review = document.createElement("label");
  review.className = "support-review";
  const consent = document.createElement("input");
  consent.type = "checkbox";
  consent.required = true;
  review.append(consent, "Ho controllato il testo e non contiene credenziali.");
  form.append(review);
  const status = document.createElement("p");
  status.setAttribute("role", "alert");
  const controls = document.createElement("div");
  controls.className = "form-actions";
  const close = localizedButton("Annulla", () => tryClose()), send = localizedButton("Invia segnalazione", () => {
  }, "primary");
  send.type = "submit";
  controls.append(close, send);
  form.append(status, controls);
  dialog.append(form);
  document.body.append(dialog);
  dialog.showModal();
  subject.focus();
  form.oninput = () => {
    dirty = true;
  };
  async function tryClose() {
    if (busy) return;
    if (dirty && !await rpc("confirmAction", { message: "Scartare la segnalazione non inviata?" })) return;
    dialog.close();
  }
  dialog.oncancel = (e) => {
    e.preventDefault();
    void tryClose();
  };
  dialog.onclose = () => {
    dialog.remove();
    previous?.focus();
  };
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (busy) return;
    busy = true;
    send.disabled = close.disabled = true;
    dialog.setAttribute("aria-busy", "true");
    try {
      await rpc("ticketCreate", { title: subject.value, body: description.value, category: category.value, priority: "normal" });
      dirty = false;
      dialog.close();
      toast(uiText("Segnalazione inviata."));
      await refresh();
    } catch (error) {
      status.textContent = error.message + " Il testo resta aperto: aggiorna l’elenco prima di tentare un nuovo invio.";
    } finally {
      busy = false;
      send.disabled = close.disabled = false;
      dialog.setAttribute("aria-busy", "false");
    }
  };
}
async function supportTicketDialog(id) {
  const epoch = ++supportEpoch;
  const body = showModal(uiText("Segnalazione"), "ASSISTENZA / CONVERSAZIONE", () => supportDialog(), true);
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  body.append(status);
  try {
    const result = await rpc("ticketRead", { id });
    if (!body.isConnected || epoch !== supportEpoch) return;
    const heading = document.createElement("h3");
    heading.textContent = result.ticket.title;
    body.append(heading);
    for (const message of result.messages) {
      const article = document.createElement("article");
      article.className = "support-message";
      const author = document.createElement("strong");
      author.textContent = message.author + (message.staff ? " · Assistenza" : "");
      const text = document.createElement("p");
      text.className = "user-content";
      text.textContent = message.body;
      text.lang = "en";
      article.append(author, text);
      body.append(article);
    }
    mountDesktopTicketLinks(body, id, result.ticket.title);
    if (result.attachments?.length) {
      const attachments = document.createElement("section"), title = document.createElement("h3");
      title.textContent = uiText("Allegati");
      attachments.append(title);
      for (const item of result.attachments) {
        const row = document.createElement("div"), name = document.createElement("p");
        name.className = "user-content";
        name.textContent = item.name;
        row.append(name, localizedButton("Anteprima protetta", () => ticketAttachmentDialog(id, item)), localizedButton("Scarica allegato", async () => {
          await rpc("ticketAttachmentSave", { id, attachment: item.id });
        }));
        attachments.append(row);
      }
      body.append(attachments);
    }
    if (result.ticket.status === "closed") {
      notice(body, uiText("Ticket chiuso."));
      return;
    }
    const form = document.createElement("form"), label = document.createElement("label");
    label.textContent = uiText("Risposta in inglese");
    const input = document.createElement("textarea");
    input.name = "reply";
    input.required = true;
    input.maxLength = 8e3;
    input.rows = 5;
    input.lang = "en";
    label.append(input);
    const submit = localizedButton("Invia risposta", () => {
    }, "primary");
    submit.type = "submit";
    form.append(label, submit);
    body.append(form);
    form.onsubmit = async (e) => {
      e.preventDefault();
      submit.disabled = true;
      try {
        await rpc("ticketReply", { id, body: input.value, version: result.ticket.version });
        await supportTicketDialog(id);
      } catch (error) {
        status.textContent = error.message;
        submit.disabled = false;
      }
    };
  } catch (error) {
    status.textContent = error.message;
  }
}
