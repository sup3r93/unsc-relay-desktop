function archiveRequestModal(title, options) {
  const previous = document.activeElement, dialog = document.createElement("dialog"), heading = document.createElement("h2"), body = document.createElement("section"), status = document.createElement("p"), close = document.createElement("button");
  const id = "request-" + crypto.randomUUID();
  dialog.className = "archive-dialog account-personal-dialog archive-request-dialog";
  dialog.style.cssText = "box-sizing:border-box;width:min(44rem,calc(100vw - 2rem));max-height:85dvh;overflow:auto;background:#0b1714;color:#e6e9df;padding:min(1.25rem,4vw);overflow-wrap:anywhere;border:1px solid #bdd282";
  heading.id = id;
  heading.style.fontSize = "1.35rem";
  heading.textContent = options.text(title);
  dialog.setAttribute("aria-labelledby", id);
  status.setAttribute("role", "status");
  close.type = "button";
  close.textContent = options.text("Chiudi");
  close.onclick = () => dialog.close();
  const style = document.createElement("style");
  style.textContent = ".archive-request-dialog label{display:grid;gap:.5rem;margin-block:.75rem;min-width:0}.archive-request-dialog input,.archive-request-dialog select{box-sizing:border-box;width:100%;min-width:0;font:inherit;background:#071014;color:#e6e9df;padding:.65rem;border:1px solid #829876}.archive-request-dialog button{min-height:2.75rem;margin:.4rem;max-width:100%;white-space:normal;overflow-wrap:anywhere}.archive-request-dialog :focus-visible{outline:3px solid #d2e79f;outline-offset:3px}.archive-request-dialog article{padding-block:1rem;border-bottom:1px solid #56674e}";
  dialog.append(style, heading, body, status, close);
  const abort = new AbortController(), observer = new MutationObserver(() => {
    if (!options.valid()) dialog.close();
  });
  observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["open"] });
  window.addEventListener("relay-vault-locked", () => dialog.close(), { signal: abort.signal });
  window.addEventListener("relay-before-account-switch", () => dialog.close(), { signal: abort.signal });
  dialog.addEventListener("close", () => {
    abort.abort();
    observer.disconnect();
    body.replaceChildren();
    dialog.remove();
    if (previous instanceof HTMLElement && previous.isConnected) previous.focus();
  }, { once: true });
  let busy = false;
  async function run(task) {
    if (busy || !options.valid()) return;
    busy = true;
    for (const button of body.querySelectorAll("button")) button.disabled = true;
    try {
      await task();
    } catch (error) {
      if (dialog.isConnected) status.textContent = options.text(error instanceof Error ? error.message : "Operazione non riuscita. Riprova.");
    } finally {
      busy = false;
      if (dialog.isConnected) for (const button of body.querySelectorAll("button")) button.disabled = false;
    }
  }
  document.body.append(dialog);
  dialog.showModal();
  close.focus();
  return { dialog, body, status, run };
}
function requestArchiveAccessDialog(options) {
  const modal = archiveRequestModal("Richiedi accesso a un archivio", options), help = document.createElement("p"), form = document.createElement("form"), label = document.createElement("label"), id = document.createElement("input"), submit = document.createElement("button");
  modal.dialog.dataset.archiveRequestCreate = "";
  help.textContent = options.text("Chiedi al titolare il codice dell\u2019archivio. La richiesta non rivela se l\u2019archivio esiste e non concede accesso automaticamente.");
  label.textContent = options.text("Codice archivio");
  id.required = true;
  id.autocomplete = "off";
  id.maxLength = 36;
  id.pattern = "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}";
  label.append(id);
  submit.type = "submit";
  submit.textContent = options.text("Invia richiesta");
  form.append(label, submit);
  form.onsubmit = (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    void modal.run(async () => {
      await options.submit(id.value.trim());
      if (!modal.dialog.isConnected || !options.valid()) return;
      modal.status.textContent = options.text("Richiesta acquisita. Se l\u2019archivio \xE8 disponibile, il titolare potr\xE0 valutarla. Non viene confermata l\u2019esistenza dell\u2019archivio.");
      form.hidden = true;
    });
  };
  modal.body.append(help, form);
  return modal.dialog;
}
function reviewArchiveAccessRequestsDialog(options) {
  const modal = archiveRequestModal("Richieste di accesso", options), name = document.createElement("p"), codeLabel = document.createElement("label"), code = document.createElement("input"), help = document.createElement("p"), list = document.createElement("section"), refresh = document.createElement("button");
  modal.dialog.dataset.archiveRequestReview = "";
  name.textContent = options.name;
  codeLabel.textContent = options.text("Codice archivio da condividere");
  code.readOnly = true;
  code.value = options.id;
  codeLabel.append(code);
  help.textContent = options.text("Solo il titolare pu\xF2 approvare. Le richieste scadono dopo sette giorni; scegli i permessi prima di confermare. Il destinatario dovr\xE0 avere un dispositivo autorizzato.");
  refresh.type = "button";
  refresh.textContent = options.text("Aggiorna richieste");
  modal.body.append(name, codeLabel, help, list, refresh);
  async function load() {
    const rows = await options.load();
    if (!modal.dialog.isConnected || !options.valid()) return;
    if (!Array.isArray(rows) || rows.length > 50) throw Error(options.text("Richiesta non valida."));
    list.replaceChildren();
    if (!rows.length) {
      const empty = document.createElement("p");
      empty.textContent = options.text("Nessuna richiesta in attesa.");
      list.append(empty);
    }
    for (const request of rows) {
      if (typeof request.email !== "string" || request.email.length > 254 || typeof request.id !== "string" || !/^[0-9a-f-]{36}$/i.test(request.id) || !Number.isFinite(Date.parse(request.expiresAt))) throw Error(options.text("Richiesta non valida."));
      const row = document.createElement("article"), email = document.createElement("h3"), expiry = document.createElement("p"), roleLabel = document.createElement("label"), role = document.createElement("select"), durationLabel = document.createElement("label"), duration = document.createElement("select"), approve = document.createElement("button"), decline = document.createElement("button");
      email.style.fontSize = "1.1rem";
      email.textContent = request.email;
      expiry.textContent = options.text("Scadenza della richiesta") + ": " + new Date(request.expiresAt).toLocaleString(document.documentElement.lang);
      roleLabel.textContent = options.text("Permessi");
      for (const [value, key] of [["read", "Sola lettura"], ["write", "Lettura e scrittura"]]) {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = options.text(key);
        option.disabled = options.personal && value === "write";
        role.append(option);
      }
      role.id = "request-role-" + crypto.randomUUID();
      roleLabel.htmlFor = role.id;
      durationLabel.textContent = options.text("Scadenza dell\u2019accesso");
      for (const [value, key] of [["none", "Senza scadenza"], ["7", "Tra 7 giorni"], ["30", "Tra 30 giorni"]]) {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = options.text(key);
        duration.append(option);
      }
      duration.id = "request-expiry-" + crypto.randomUUID();
      durationLabel.htmlFor = duration.id;
      approve.type = decline.type = "button";
      approve.textContent = options.text("Approva richiesta");
      decline.textContent = options.text("Rifiuta richiesta");
      approve.onclick = () => void modal.run(async () => {
        const result = await options.approve(request, role.value, duration.value === "none" ? null : new Date(Date.now() + Number(duration.value) * 864e5).toISOString());
        if (result === false) return;
        if (!modal.dialog.isConnected) return;
        await load();
        modal.status.textContent = options.text(typeof result === "string" ? result : "Richiesta approvata.");
      });
      decline.onclick = () => void modal.run(async () => {
        if (!await options.decline(request) || !modal.dialog.isConnected) return;
        await load();
        modal.status.textContent = options.text("Richiesta rifiutata.");
      });
      row.append(email, expiry, roleLabel, role, durationLabel, duration, approve, decline);
      list.append(row);
    }
  }
  refresh.onclick = () => void modal.run(load);
  void modal.run(load);
  return modal.dialog;
}
