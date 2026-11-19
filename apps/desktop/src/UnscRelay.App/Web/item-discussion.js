function validDiscussion(value) {
  if (value === void 0 || value === null) return [];
  if (!Array.isArray(value) || value.length > 100) throw Error("Discussione non valida.");
  const result = value;
  if (new Set(result.map((item) => item?.id)).size !== result.length || result.some((item) => !item || typeof item.id !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(item.id) || typeof item.author !== "string" || !item.author.trim() || item.author.length > 100 || /[\x00-\x1f\x7f-\x9f]/.test(item.author) || typeof item.text !== "string" || !item.text.trim() || item.text.length > 4e3 || /[\x00-\x08\x0b\x0c\x0e-\x1f\x7f-\x9f]/.test(item.text) || typeof item.created !== "string" || item.created.length > 40 || !Number.isFinite(Date.parse(item.created)) || new Date(item.created).getUTCFullYear() < 2e3 || new Date(item.created).getUTCFullYear() > 2100)) throw Error("Commento non valido.");
  return result;
}
function appendItemComment(current, author, text, now = /* @__PURE__ */ new Date()) {
  const result = [...validDiscussion(current), { id: crypto.randomUUID(), author: author.trim(), text: text.trim(), created: now.toISOString() }];
  return validDiscussion(result);
}
function itemDiscussionDialog(options) {
  const text = options.text, previous = document.activeElement, dialog = document.createElement("dialog"), heading = document.createElement("h2"), warning = document.createElement("p"), list = document.createElement("ol"), status = document.createElement("p"), form = document.createElement("form"), authorLabel = document.createElement("label"), author = document.createElement("input"), messageLabel = document.createElement("label"), message = document.createElement("textarea"), send = document.createElement("button"), refresh = document.createElement("button"), close = document.createElement("button");
  let busy = false, canPost = false;
  dialog.className = "account-personal-dialog item-discussion";
  dialog.dataset.itemDiscussion = "";
  const id = "discussion-" + crypto.randomUUID();
  heading.id = id;
  heading.textContent = text("Discussione") + " \xB7 " + options.name;
  dialog.setAttribute("aria-labelledby", id);
  warning.id = id + "-help";
  warning.textContent = text("I commenti sono cifrati con l\u2019archivio e visibili ai membri autorizzati. Il nome dell\u2019autore \xE8 dichiarato, non verificato. Chi pu\xF2 scrivere pu\xF2 modificare la discussione: non \xE8 un registro di controllo.");
  dialog.setAttribute("aria-describedby", warning.id);
  status.setAttribute("role", "status");
  list.setAttribute("aria-label", text("Commenti"));
  authorLabel.textContent = text("Nome da mostrare (non verificato)");
  author.required = true;
  author.maxLength = 100;
  author.autocomplete = "off";
  authorLabel.append(author);
  messageLabel.textContent = text("Nuovo commento");
  message.required = true;
  message.maxLength = 4e3;
  message.rows = 4;
  messageLabel.append(message);
  send.type = "submit";
  send.textContent = text("Aggiungi commento");
  refresh.type = close.type = "button";
  refresh.textContent = text("Aggiorna discussione");
  close.textContent = text("Chiudi");
  close.onclick = () => dialog.close();
  form.append(authorLabel, messageLabel, send);
  dialog.append(heading, warning, list, status, form, refresh, close);
  const style = document.createElement("style");
  style.textContent = ".item-discussion{box-sizing:border-box;overflow-wrap:anywhere;width:min(44rem,calc(100vw - 2rem));max-height:85dvh;overflow:auto;background:#0b1714;color:#e6e9df;border:1px solid #bdd282;padding:min(1.25rem,4vw);border-radius:.6rem}.item-discussion h2{font-size:1.35rem;line-height:1.3;overflow-wrap:anywhere}.item-discussion button{max-width:100%;white-space:normal;overflow-wrap:anywhere}.item-discussion label{display:grid;gap:.5rem;margin-block:.75rem}.item-discussion input,.item-discussion textarea{box-sizing:border-box;width:100%;font:inherit;background:#071014;color:#e6e9df;border:1px solid #829876;padding:.65rem}.item-discussion li{padding:.75rem 0;border-bottom:1px solid #56674e;overflow-wrap:anywhere}.item-discussion li p{white-space:pre-wrap}.item-discussion button{min-height:2.75rem;margin:.4rem;padding:.5rem .75rem}.item-discussion :focus-visible{outline:3px solid #d2e79f;outline-offset:3px}";
  dialog.append(style);
  async function load() {
    if (options.valid && !options.valid()) throw Error(text("Archivio bloccato."));
    const value = await options.read();
    if (!dialog.isConnected || options.valid && !options.valid()) return;
    const comments = validDiscussion(value.comments);
    list.replaceChildren();
    for (const comment of comments) {
      const row = document.createElement("li"), identity = document.createElement("strong"), time = document.createElement("time"), body = document.createElement("p");
      identity.textContent = comment.author;
      time.dateTime = comment.created;
      time.textContent = " \xB7 " + new Date(comment.created).toLocaleString(document.documentElement.lang);
      body.textContent = comment.text;
      row.append(identity, time, body);
      list.append(row);
    }
    form.hidden = value.readOnly;
    canPost = !value.readOnly && comments.length < 100;
    send.disabled = !canPost;
    status.textContent = text(value.pending ? "Modifiche locali non pubblicate. Gli altri membri vedono ancora la copia online." : value.readOnly ? "Sola lettura" : "I commenti diventano disponibili agli altri dopo il salvataggio dell\u2019archivio.");
    if (!comments.length) {
      const empty = document.createElement("li");
      empty.textContent = text("Nessun commento.");
      list.append(empty);
    }
  }
  async function run(work) {
    if (busy || options.valid && !options.valid()) return;
    busy = true;
    send.disabled = refresh.disabled = true;
    try {
      await work();
    } catch (error) {
      if (dialog.isConnected) status.textContent = error instanceof Error ? text(error.message) : text("Operazione non riuscita. Riprova.");
    } finally {
      busy = false;
      if (dialog.isConnected) {
        refresh.disabled = false;
        send.disabled = !canPost;
      }
    }
  }
  refresh.onclick = () => void run(load);
  form.onsubmit = (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    void run(async () => {
      await options.post(author.value, message.value);
      if (!dialog.isConnected) return;
      message.value = "";
      await load();
      message.focus();
    });
  };
  const abort = new AbortController(), observer = new MutationObserver(() => {
    if (options.valid && !options.valid()) dialog.close();
  });
  const conceal = () => dialog.close();
  window.addEventListener("relay-vault-locked", conceal, { signal: abort.signal });
  window.addEventListener("relay-before-account-switch", conceal, { signal: abort.signal });
  observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["open"] });
  dialog.addEventListener("close", () => {
    abort.abort();
    observer.disconnect();
    message.value = author.value = "";
    list.replaceChildren();
    dialog.remove();
    if (previous instanceof HTMLElement && previous.isConnected) previous.focus();
    else options.restoreFocus?.();
  }, { once: true });
  document.body.append(dialog);
  dialog.showModal();
  close.focus();
  void run(load);
  return dialog;
}
