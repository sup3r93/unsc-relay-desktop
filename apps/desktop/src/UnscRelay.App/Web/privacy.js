"use strict";
async function privacyDialog() {
  const body = showModal(uiText("Dati e privacy"), uiText("ACCOUNT SELEZIONATO / QUESTO DISPOSITIVO"), null, true), status = document.createElement("p"), groups = document.createElement("div"), help = document.createElement("p");
  status.setAttribute("role", "status");
  status.dataset.privacyStatus = "true";
  groups.className = "privacy-data-groups";
  help.textContent = uiText("Categorie e dimensioni dei dati, senza leggere i contenuti.");
  body.append(help, status, groups);
  const labels = { archive: "Archivi e copie di recupero", recordings: "Registrazioni", editor: "Copie degli editor", temporary: "File temporanei gestiti" };
  async function refresh() {
    status.textContent = uiText("Caricamento…");
    try {
      const data = await rpc("privacyInventory");
      if (!groups.isConnected) return;
      groups.replaceChildren();
      for (const group of data) {
        const card = document.createElement("section"), title = document.createElement("h3"), detail = document.createElement("p");
        card.className = "settings-card";
        title.textContent = uiText(labels[group.category] || group.category);
        detail.textContent = group.files + " " + uiText("file") + " · " + capacityText(group.bytes) + (group.partial ? " · " + uiText("Inventario parziale") : "") + (group.skippedLinks ? " · " + uiText("Collegamenti saltati") + ": " + group.skippedLinks : "");
        card.append(title, detail);
        if (group.category === "editor" || group.category === "temporary") {
          const clean = localizedButton("Rimuovi questi file", async () => {
            clean.disabled = true;
            try {
              const result = await rpc("privacyClean", { category: group.category });
              await refresh();
              if (status.isConnected) status.textContent = uiText(result.partial ? "Pulizia incompleta. Aggiorna l’inventario e chiudi i programmi che usano questi file." : result.removed ? "Pulizia completata." : "Pulizia annullata.");
            } catch (error) {
              if (status.isConnected) status.textContent = error.message;
            } finally {
              clean.disabled = false;
            }
          });
          clean.disabled = group.files === 0;
          card.append(clean);
        }
        groups.append(card);
      }
      status.textContent = "";
    } catch (error) {
      if (status.isConnected) status.textContent = error.message;
    }
  }
  const cache = document.createElement("section");
  cache.className = "settings-card";
  const cacheTitle = document.createElement("h3"), cacheHelp = document.createElement("p");
  cacheTitle.textContent = uiText("Cache dell’interfaccia");
  cacheHelp.textContent = uiText("Svuotare la cache conserva account, cookie, archivi e preferenze.");
  cache.append(cacheTitle, cacheHelp, localizedButton("Svuota cache dell’interfaccia", async () => {
    if (await rpc("privacyCacheClear")) status.textContent = uiText("Cache svuotata.");
  }));
  const reports = document.createElement("section");
  reports.className = "settings-card";
  const reportsTitle = document.createElement("h3"), reportsHelp = document.createElement("p");
  reportsTitle.textContent = uiText("Rapporti diagnostici ed esportazioni");
  reportsHelp.textContent = uiText("Rapporti ed esportazioni restano nella posizione scelta. Questa pulizia non li rimuove.");
  reports.append(reportsTitle, reportsHelp, localizedButton("Assistenza e segnalazioni", () => supportDialog()));
  const actions = document.createElement("div");
  actions.className = "form-actions";
  actions.append(localizedButton("Aggiorna inventario", refresh), localizedButton("Apri dati locali", () => call("openData")), localizedButton("Il tuo account", () => accountProfileDialog()));
  body.append(cache, reports, actions);
  storageUsageCard(body, uiText, () => rpc("accountStorageUsage"), (value) => new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(value)), "h3", () => {
    const owner = state.account?.owner;
    storageExplorerDialog({ text: uiText, valid: () => body.isConnected && modal.open && state.account?.owner === owner && !state.appLocked, archives: () => rpc("archiveList"), attachments: (page) => rpc("accountStorageAttachments", { page }), remove: (attachmentId) => rpc("accountStorageAttachmentDelete", { attachmentId }), measure: (archiveId) => rpc("archiveStorageUsage", { archiveId }), openArchive: (archiveId) => archivesDialog(archiveId), openTicket: (ticketId) => supportTicketDialog(ticketId) });
  });
  await refresh();
}
