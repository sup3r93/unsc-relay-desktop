"use strict";
function mountDesktopTicketLinks(parent, id, title) {
  const section = document.createElement("section"), heading = document.createElement("h3"), rows = document.createElement("div"), status = document.createElement("p");
  section.className = "ticket-links settings-card";
  heading.textContent = uiText("Ticket collegati");
  status.setAttribute("role", "status");
  section.append(heading, rows, status, localizedButton("Collega un ticket", choose));
  parent.append(section);
  async function load() {
    try {
      const links = await rpc("ticketLinks", { id });
      if (!section.isConnected) return;
      rows.replaceChildren();
      for (const link of links) {
        const row = document.createElement("article"), label = document.createElement("p");
        label.textContent = uiText(link.kind === "duplicate" ? link.outgoing ? "Duplicato di" : "Duplicato collegato" : "Ticket correlato") + " · " + link.title;
        row.append(label, localizedButton("Apri ticket collegato", () => supportTicketDialog(link.ticketId)), localizedButton("Rimuovi collegamento", async () => {
          if (!await rpc("confirmAction", { text: uiText("Rimuovere solo il collegamento? I ticket restano invariati.") })) return;
          await rpc("ticketLinkDelete", { id, link: link.id });
          await load();
        }));
        rows.append(row);
      }
      status.textContent = links.length ? "" : uiText("Nessun ticket collegato.");
    } catch (error) {
      if (section.isConnected) status.textContent = error.message;
    }
  }
  void load();
  function choose() {
    const dialog = document.createElement("dialog"), body = document.createElement("div"), h = document.createElement("h2"), status2 = document.createElement("p"), form = document.createElement("form"), searchLabel = document.createElement("label"), search = document.createElement("input"), selection = document.createElement("select"), selectionLabel = document.createElement("label"), kind = document.createElement("select"), kindLabel = document.createElement("label");
    dialog.className = "manager-modal";
    dialog.dataset.ticketLink = "true";
    h.id = "ticket-link-" + crypto.randomUUID();
    h.textContent = uiText("Collega un ticket");
    dialog.setAttribute("aria-labelledby", h.id);
    body.className = "attachment-preview-body";
    status2.setAttribute("role", "status");
    search.type = "search";
    search.maxLength = 160;
    searchLabel.textContent = uiText("Cerca ticket");
    searchLabel.append(search);
    selection.size = 6;
    selectionLabel.textContent = uiText("Ticket da collegare");
    selectionLabel.append(selection);
    kindLabel.textContent = uiText("Tipo di collegamento");
    for (const [value, key] of [["related", "Ticket correlato"], ["duplicate", "Duplicato di"]]) kind.append(new Option(uiText(key), value));
    kindLabel.append(kind);
    const submit = localizedButton("Cerca", () => {
    });
    submit.type = "submit";
    form.append(searchLabel, submit);
    const apply = localizedButton("Conferma collegamento", async () => {
      const selected = candidates.find((ticket) => ticket.id === selection.value);
      if (!selected) return;
      if (!await rpc("confirmAction", { text: uiText("Collegare i ticket selezionati?") + "\n" + title + "\n" + selected.title + "\n" + uiText(kind.value === "duplicate" ? "Duplicato di" : "Ticket correlato") })) return;
      apply.disabled = true;
      try {
        await rpc("ticketLinkCreate", { id, relatedId: selected.id, kind: kind.value });
        dialog.close();
        await load();
      } catch (error) {
        status2.textContent = error.message;
        apply.disabled = false;
      }
    }, "primary");
    apply.disabled = true;
    selection.onchange = () => apply.disabled = !selection.value;
    const close = localizedButton("Chiudi", () => dialog.close());
    body.append(form, selectionLabel, kindLabel, status2, apply);
    const header = document.createElement("header");
    header.className = "modal-header";
    header.append(h, close);
    dialog.append(header, body);
    document.body.append(dialog);
    dialog.showModal();
    search.focus();
    let candidates = [], sequence = 0;
    const closeParent = () => dialog.close();
    modal.addEventListener("close", closeParent, { once: true });
    dialog.addEventListener("close", () => {
      modal.removeEventListener("close", closeParent);
      dialog.remove();
    }, { once: true });
    async function find() {
      const request = ++sequence;
      submit.disabled = true;
      apply.disabled = true;
      try {
        const result = await rpc("ticketFind", { search: search.value });
        if (!dialog.isConnected || request !== sequence) return;
        candidates = result.filter((ticket) => ticket.id !== id).slice(0, 50);
        selection.replaceChildren(new Option(uiText("Scegli un ticket"), ""));
        for (const ticket of candidates) selection.append(new Option(ticket.title + " · #" + ticket.id.slice(0, 8), ticket.id));
        status2.textContent = uiText("Mostrati fino a 50 risultati. Restringi la ricerca se manca un ticket.");
      } catch (error) {
        if (dialog.isConnected) status2.textContent = error.message;
      } finally {
        submit.disabled = false;
      }
    }
    form.onsubmit = (event) => {
      event.preventDefault();
      void find();
    };
    void find();
  }
}
