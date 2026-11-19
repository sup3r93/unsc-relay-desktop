function storageExplorerDialog(options) {
  const text = options.text, previous = document.activeElement, dialog = document.createElement("dialog"), heading = document.createElement("h2"), help = document.createElement("p"), tabs = document.createElement("nav"), list = document.createElement("section"), status = document.createElement("p"), previousPage = document.createElement("button"), nextPage = document.createElement("button"), refresh = document.createElement("button"), close = document.createElement("button"), abort = new AbortController();
  let category = "archives", page = 0, busy = false, archives = null;
  let confirmation = null;
  const id = "space-" + crypto.randomUUID();
  heading.id = id;
  heading.textContent = text("Esplora utilizzo dello spazio");
  dialog.dataset.storageExplorer = "";
  dialog.className = "account-personal-dialog storage-explorer";
  dialog.setAttribute("aria-labelledby", id);
  help.id = id + "-help";
  help.textContent = text("L’elenco usa metadati. La misurazione di un archivio legge il pacchetto cifrato sul server e restituisce solo dimensione e revisione; non decifra né scarica i contenuti sul dispositivo. La cronologia e le copie esportate non sono incluse.");
  dialog.setAttribute("aria-describedby", help.id);
  status.setAttribute("role", "status");
  tabs.setAttribute("role", "tablist");
  tabs.setAttribute("aria-label", text("Categorie di spazio"));
  list.id = id + "-panel";
  list.setAttribute("role", "tabpanel");
  list.tabIndex = 0;
  const navigation = document.createElement("div");
  navigation.className = "space-navigation";
  previousPage.type = nextPage.type = refresh.type = close.type = "button";
  previousPage.textContent = text("Precedente");
  nextPage.textContent = text("Successiva");
  refresh.textContent = text("Aggiorna elenco");
  close.textContent = text("Chiudi");
  close.onclick = () => dialog.close();
  navigation.append(previousPage, nextPage, refresh);
  dialog.append(heading, help, tabs, status, list, navigation, close);
  const style = document.createElement("style");
  style.textContent = ".storage-explorer{box-sizing:border-box;width:min(56rem,calc(100vw - 2rem));max-height:85dvh;overflow:auto;padding:min(1.25rem,4vw);background:#0b1714;color:#e6e9df;border:1px solid #bdd282;overflow-wrap:anywhere}.storage-explorer h2{font-size:1.35rem;line-height:1.3}.storage-explorer h3{font-size:1.1rem;margin:.3rem 0}.storage-explorer button{min-height:2.75rem;max-width:100%;margin:.3rem;white-space:normal;overflow-wrap:anywhere}.storage-explorer [role=tablist],.space-navigation{display:flex;flex-wrap:wrap;gap:.5rem}.storage-explorer [role=tab][aria-selected=true]{background:#bdd282;color:#101b13}.storage-explorer article{padding:1rem 0;border-bottom:1px solid #56674e}.storage-explorer :focus-visible{outline:3px solid #d2e79f;outline-offset:3px}";
  dialog.append(style);
  function size(bytes) {
    if (!Number.isSafeInteger(bytes) || bytes < 0) throw Error(text("Dimensione non valida."));
    return bytes.toLocaleString(document.documentElement.lang) + " B";
  }
  function alive() {
    return dialog.isConnected && options.valid();
  }
  function row(name) {
    const card = document.createElement("article"), title = document.createElement("h3");
    title.className = "user-content";
    title.textContent = name;
    card.append(title);
    list.append(card);
    return card;
  }
  async function run(work) {
    if (busy || !alive()) return;
    busy = true;
    dialog.setAttribute("aria-busy", "true");
    for (const button of dialog.querySelectorAll("button")) if (button !== close && button.getAttribute("role") !== "tab") button.disabled = true;
    try {
      await work();
    } catch (error) {
      if (alive()) status.textContent = text(error instanceof Error ? error.message : "Operazione non riuscita. Riprova.");
    } finally {
      busy = false;
      if (alive()) {
        dialog.removeAttribute("aria-busy");
        for (const button of dialog.querySelectorAll("button")) button.disabled = false;
        previousPage.disabled = page === 0;
        nextPage.disabled = !nextPage.dataset.more;
      }
    }
  }
  function confirmRemoval(attachment) {
    return new Promise((resolve) => {
      const prompt = document.createElement("dialog"), title = document.createElement("h2"), warning = document.createElement("p"), details = document.createElement("dl"), cancel = document.createElement("button"), remove = document.createElement("button");
      let accepted = false;
      confirmation = prompt;
      prompt.className = "storage-explorer";
      prompt.dataset.storageConfirmation = "";
      title.id = id + "-delete-title";
      title.textContent = text("Eliminare questo allegato?");
      warning.id = id + "-delete-warning";
      warning.textContent = text("L’allegato sarà subito inaccessibile. Il servizio distruggerà tutte le sue versioni e libererà la quota solo dopo la verifica. Le copie già scaricate restano disponibili; i backup seguono la conservazione configurata.");
      prompt.setAttribute("aria-labelledby", title.id);
      prompt.setAttribute("aria-describedby", warning.id);
      for (const [label, value] of [["Nome del file", attachment.name], ["Dimensione", size(attachment.bytes)], ["Ticket", attachment.ticketId]]) {
        const term = document.createElement("dt"), valueElement = document.createElement("dd");
        term.textContent = text(label);
        valueElement.textContent = value;
        details.append(term, valueElement);
      }
      cancel.type = remove.type = "button";
      cancel.textContent = text("Annulla");
      remove.textContent = text("Elimina definitivamente");
      remove.className = "danger";
      cancel.onclick = () => prompt.close();
      remove.onclick = () => {
        accepted = true;
        prompt.close();
      };
      prompt.append(title, warning, details, cancel, remove);
      prompt.addEventListener("close", () => {
        confirmation = null;
        prompt.remove();
        close.focus();
        resolve(accepted && alive());
      }, { once: true });
      document.body.append(prompt);
      prompt.showModal();
      cancel.focus();
    });
  }
  async function load() {
    status.textContent = text("Caricamento…");
    if (category === "archives") {
      if (!archives) archives = await options.archives();
      if (!alive()) return;
      if (!Array.isArray(archives) || archives.length > 1e3) throw Error(text("Riepilogo non valido."));
      list.replaceChildren();
      const selected = archives.slice(page * 20, (page + 1) * 20);
      nextPage.dataset.more = archives.length > (page + 1) * 20 ? "yes" : "";
      for (const archive of selected) {
        const card = row(archive.name), access = document.createElement("p"), measured = document.createElement("p"), measure = document.createElement("button"), open = document.createElement("button");
        access.textContent = text(archive.access === "owner" ? "Titolare" : archive.access === "write" ? "Lettura e scrittura" : "Sola lettura");
        measured.textContent = text("Dimensione non ancora misurata.");
        measure.type = open.type = "button";
        measure.textContent = text("Misura pacchetto cifrato");
        measure.onclick = () => void run(async () => {
          const result = await options.measure(archive.id);
          if (!alive() || !card.isConnected) return;
          if (!Number.isSafeInteger(result.revision) || result.revision < 1) throw Error(text("Riepilogo non valido."));
          measured.textContent = size(result.encryptedBytes) + " · " + text("Revisione") + " " + result.revision + " · " + new Date(result.measuredAt).toLocaleString(document.documentElement.lang);
          status.textContent = text("Misurazione completata.");
        });
        open.textContent = text("Gestisci archivio");
        open.onclick = () => {
          dialog.close();
          options.openArchive(archive.id);
        };
        card.append(access, measured, measure, open);
      }
    } else {
      const value = await options.attachments(page);
      if (!alive()) return;
      if (!value || !Array.isArray(value.items) || value.items.length > 20 || value.page !== page) throw Error(text("Riepilogo non valido."));
      list.replaceChildren();
      nextPage.dataset.more = value.hasMore ? "yes" : "";
      for (const attachment of value.items) {
        const card = row(attachment.name), detail = document.createElement("p"), open = document.createElement("button");
        detail.textContent = size(attachment.bytes) + " · " + text(attachment.state === "pending" ? "Caricamento in attesa" : attachment.state === "deleting" ? "Eliminazione in attesa" : "Disponibile") + " · " + new Date(attachment.createdAt).toLocaleString(document.documentElement.lang);
        open.type = "button";
        open.textContent = text("Apri ticket");
        open.onclick = () => {
          dialog.close();
          options.openTicket(attachment.ticketId);
        };
        card.append(detail, open);
        if (attachment.canDelete === true && attachment.state === "ready" && options.remove) {
          const remove = document.createElement("button");
          remove.type = "button";
          remove.className = "danger";
          remove.textContent = text("Elimina allegato");
          remove.onclick = () => void run(async () => {
            if (!await confirmRemoval(attachment) || !alive()) return;
            const result = await options.remove(attachment.id);
            if (!alive()) return;
            if (result?.state !== "deleting") throw Error(text("Riepilogo non valido."));
            await load();
            if (alive()) status.textContent = text("Eliminazione richiesta. La quota sarà liberata dopo la distruzione verificata.");
          });
          card.append(remove);
        }
      }
    }
    if (!list.childElementCount) {
      const empty = document.createElement("p");
      empty.textContent = text("Nessun elemento in questa pagina.");
      list.append(empty);
    }
    status.textContent = text("Pagina") + " " + (page + 1);
    previousPage.disabled = page === 0;
    nextPage.disabled = !nextPage.dataset.more;
  }
  function select(value) {
    if (busy) return;
    category = value;
    page = 0;
    for (const tab of tabs.querySelectorAll("button")) {
      const active = tab.dataset.category === value;
      tab.setAttribute("aria-selected", String(active));
      tab.tabIndex = active ? 0 : -1;
      if (active) list.setAttribute("aria-labelledby", tab.id);
    }
    void run(load);
  }
  for (const [value, label] of [["archives", "Archivi"], ["attachments", "Allegati"]]) {
    const button = document.createElement("button");
    button.id = id + "-" + value;
    button.type = "button";
    button.dataset.category = value;
    button.textContent = text(label);
    button.setAttribute("role", "tab");
    button.setAttribute("aria-controls", list.id);
    button.setAttribute("aria-selected", String(value === category));
    button.tabIndex = value === category ? 0 : -1;
    button.onclick = () => select(value);
    tabs.append(button);
  }
  list.setAttribute("aria-labelledby", id + "-archives");
  tabs.onkeydown = (event) => {
    if (busy || !["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    const value = event.key === "Home" ? "archives" : event.key === "End" ? "attachments" : category === "archives" ? "attachments" : "archives";
    select(value);
    tabs.querySelector("[data-category=" + value + "]")?.focus();
  };
  previousPage.onclick = () => {
    if (page > 0 && !busy) {
      page--;
      void run(load);
    }
  };
  nextPage.onclick = () => {
    if (nextPage.dataset.more && !busy) {
      page++;
      void run(load);
    }
  };
  refresh.onclick = () => {
    archives = null;
    void run(load);
  };
  const observer = new MutationObserver(() => {
    if (!options.valid()) dialog.close();
  });
  observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["open"] });
  window.addEventListener("relay-vault-locked", () => dialog.close(), { signal: abort.signal });
  window.addEventListener("relay-before-account-switch", () => dialog.close(), { signal: abort.signal });
  dialog.addEventListener("close", () => {
    confirmation?.close();
    abort.abort();
    observer.disconnect();
    archives = null;
    list.replaceChildren();
    dialog.remove();
    if (previous instanceof HTMLElement && previous.isConnected) previous.focus();
  }, { once: true });
  document.body.append(dialog);
  dialog.showModal();
  close.focus();
  void run(load);
  return dialog;
}
