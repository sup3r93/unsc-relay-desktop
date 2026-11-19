"use strict";
async function synchronizationCenterDialog() {
  let page = 0, sequence = 0;
  const body = showModal(uiText("Centro sincronizzazione"), uiText("ACCOUNT / STATO DELLE COPIE"), null, true);
  const marker = document.createElement("div");
  marker.className = "sync-overview";
  body.append(marker);
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  const toolbar = document.createElement("div");
  toolbar.className = "form-actions";
  const list = document.createElement("div");
  body.append(toolbar, status, list);
  const text = (key) => uiText(key), date = (value) => value ? new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "medium" }).format(new Date(value)) : text("Non registrato");
  const previous = button(text("Precedente"), () => {
    page--;
    return load();
  }), next = button(text("Successiva"), () => {
    page++;
    return load();
  });
  const refresh = button(text("Aggiorna stato"), () => load()), sync = button(text("Sincronizza adesso"), async () => {
    sync.disabled = true;
    try {
      update(await call("accountSync"));
    } finally {
      await load();
    }
  }), review = button(text("Risolvi conflitti"), () => renderSettingsDialog("account"));
  toolbar.append(refresh, sync, review, previous, next, button(text("Archivi collegati"), () => archivesDialog()));
  async function load() {
    const request = ++sequence;
    refresh.disabled = true;
    sync.disabled = true;
    previous.disabled = next.disabled = true;
    status.textContent = text("Caricamento dello stato…");
    try {
      const value = await call("accountSyncOverview", { page });
      if (request !== sequence || !marker.isConnected || !modal.open) return;
      list.replaceChildren();
      previous.disabled = page === 0;
      next.disabled = !value.hasMore;
      const account = value.account || {}, card = document.createElement("section");
      card.className = "settings-card";
      const heading = document.createElement("h3");
      heading.textContent = text("Archivio personale sincronizzato");
      card.append(heading);
      const facts = document.createElement("dl");
      facts.className = "account-profile-details";
      for (const [label, content] of [[text("Ultima sincronizzazione riuscita"), date(account.lastSuccessfulSync)], [text("Ultima verifica online"), date(account.lastVerified)], [text("Revisione verificata"), account.revision > 0 ? String(account.revision) : text("Non registrato")], [text("Modifiche locali"), value.pendingLocalChanges === true ? text("In attesa di sincronizzazione") : value.pendingLocalChanges === false ? text("Nessuna modifica locale rilevata") : text("Da verificare dopo lo sblocco")]]) {
        const row = document.createElement("div"), term = document.createElement("dt"), detail = document.createElement("dd");
        term.textContent = label;
        detail.textContent = content;
        row.append(term, detail);
        facts.append(row);
      }
      card.append(facts);
      const message = document.createElement("p");
      message.className = "user-content";
      message.textContent = account.message || "";
      card.append(message);
      if (account.meteredConnection) {
        const paused = document.createElement("p");
        paused.textContent = text("Connessione a consumo: sincronizzazione automatica in pausa. Usa Sincronizza adesso per procedere esplicitamente.");
        card.append(paused);
      }
      if (account.meteredExternalPending > 0) {
        const external = document.createElement("p");
        external.textContent = text("Salvataggi esterni non caricati") + ": " + account.meteredExternalPending;
        card.append(external);
      }
      list.append(card);
      review.hidden = !account.conflict;
      sync.disabled = !account.ready || account.busy || account.conflict;
      status.textContent = text(account.conflict ? "Conflitto: scegli esplicitamente quale copia usare." : account.busy ? "Sincronizzazione in corso…" : "Stato aggiornato.");
      const note = document.createElement("p");
      note.textContent = text("Gli altri archivi mostrano l’ultima copia osservata su questo PC. Aprili online per verificare eventuali modifiche o revoche successive.");
      list.append(note);
      for (const archive of value.archives || []) {
        const row = document.createElement("section");
        row.className = "settings-card";
        const h = document.createElement("h3");
        h.className = "user-content";
        h.textContent = archive.name;
        const detail = document.createElement("p");
        detail.textContent = text(archive.invalid ? "Riepilogo locale non leggibile: verifica l’archivio prima di usarlo." : archive.pending === true ? "Modifiche locali in attesa di pubblicazione" : archive.revision ? "Copia locale disponibile" : "Stato della copia locale da verificare") + " · " + text("Copia osservata il") + ": " + date(archive.observedAt) + (archive.revision ? " · " + text("Revisione") + " " + archive.revision : "");
        row.append(h, detail);
        list.append(row);
      }
    } catch (error) {
      if (request === sequence && marker.isConnected && modal.open) status.textContent = error.message;
    } finally {
      if (request === sequence) refresh.disabled = false;
    }
  }
  await load();
}
