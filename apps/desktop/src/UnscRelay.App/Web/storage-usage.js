function storageUsageCard(host, text, load, formatDate, headingLevel = "h2", explore) {
  const card = document.createElement("section"), heading = document.createElement(headingLevel), help = document.createElement("p"), status = document.createElement("p"), details = document.createElement("dl"), read = document.createElement("button");
  card.className = "panel settings-card storage-usage";
  heading.textContent = text("Utilizzo dello spazio");
  help.textContent = text("Riepilogo dei metadati dell\u2019account. Non scarica i contenuti degli archivi. Gli allegati in attesa di caricamento occupano gi\xE0 la quota prenotata.");
  status.setAttribute("role", "status");
  details.className = "server-information";
  read.type = "button";
  read.textContent = text("Aggiorna utilizzo online");
  card.append(heading, help, read, status, details);
  host.append(card);
  if (explore) {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = text("Esplora archivi e allegati");
    button.onclick = () => explore();
    card.append(button);
  }
  function size(value) {
    if (value === -1) return text("Illimitato");
    if (!Number.isFinite(value) || value < 0) return text("Non disponibile");
    const units = ["B", "KiB", "MiB", "GiB", "TiB"];
    let index = 0;
    while (value >= 1024 && index < units.length - 1) {
      value /= 1024;
      index++;
    }
    return value.toLocaleString(document.documentElement.lang, { maximumFractionDigits: 2 }) + " " + units[index];
  }
  read.onclick = async () => {
    read.disabled = true;
    status.textContent = text("Caricamento\u2026");
    try {
      let row2 = function(key, value2) {
        const term = document.createElement("dt"), description = document.createElement("dd");
        term.textContent = text(key);
        description.textContent = value2;
        details.append(term, description);
      };
      var row = row2;
      const value = await load();
      if (!card.isConnected) return;
      const usage = value.usage;
      details.replaceChildren();
      row2("Archivi di propriet\xE0", String(usage.ownedArchives));
      row2("Archivi condivisi con te", String(usage.sharedArchives));
      row2("Limite configurato per archivio", size(usage.archiveLimit));
      row2("Allegati utilizzati e prenotati", size(usage.attachmentUsed));
      row2("Di cui prenotati", size(usage.attachmentReserved));
      row2("Limite totale allegati", size(usage.attachmentLimit));
      if (usage.attachmentLimit >= 0) {
        const progress = document.createElement("progress");
        progress.max = Math.max(1, usage.attachmentLimit);
        progress.value = Math.min(progress.max, Math.max(0, usage.attachmentUsed));
        progress.setAttribute("aria-label", text("Utilizzo della quota allegati"));
        const progressTerm = document.createElement("dt"), progressValue = document.createElement("dd");
        progressTerm.textContent = text("Utilizzo della quota allegati");
        progressValue.append(progress);
        details.append(progressTerm, progressValue);
        if (usage.attachmentUsed > usage.attachmentLimit) {
          const exceeded = document.createElement("p");
          exceeded.textContent = text("Quota allegati superata.");
          progressValue.append(exceeded);
        }
      }
      status.textContent = text("Ultimo aggiornamento") + ": " + formatDate(value.checkedAt);
    } catch (error) {
      if (card.isConnected) status.textContent = error instanceof Error ? error.message : String(error);
    } finally {
      read.disabled = false;
    }
  };
}
