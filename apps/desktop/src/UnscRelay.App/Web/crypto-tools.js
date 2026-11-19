"use strict";
function agentDialog() {
  const body = showModal(uiText("Token, smart card e agent"), "CHIAVI ESTERNE / AVANZATE", keysDialog);
  notice(body, uiText("Qui trovi solo le chiavi fornite da un token o da un agent. Per vedere e modificare i file importati nell’archivio, torna a Chiavi salvate."));
  const form = document.createElement("form");
  const kind = field(form, uiText("Agent da usare"), "kind", "openssh-agent", "text", [["openssh-agent", "OpenSSH / agent compatibile"], ["pageant", "PuTTY Pageant"]]);
  const read = document.createElement("button");
  read.textContent = uiText("Leggi identità disponibili");
  read.className = "primary";
  const actions = document.createElement("div");
  actions.className = "toolbar";
  actions.append(read, localizedButton("Carica modulo PKCS#11 del produttore", () => call("agentLoadSmartcard", { kind: kind.value })));
  form.append(actions);
  body.append(form);
  const list = document.createElement("div");
  body.append(list);
  form.onsubmit = async (e) => {
    e.preventDefault();
    read.disabled = true;
    list.replaceChildren();
    try {
      const keys = await call("agentList", { kind: kind.value });
      if (!keys.length) notice(list, uiText("Nessuna identità disponibile. Controlla token e agent."));
      for (const key of keys) {
        const row = document.createElement("div");
        row.className = "list-row";
        const detail = document.createElement("div");
        const title = document.createElement("strong");
        title.textContent = key.comment || key.algorithm;
        const description = document.createElement("small");
        description.textContent = key.algorithm + " / " + (key.hardware ? "FIDO hardware" : "Firma tramite agent") + (key.certificate ? " / certificato SSH" : "");
        const fingerprint = document.createElement("small");
        fingerprint.textContent = key.fingerprint;
        detail.append(title, description, fingerprint);
        row.append(detail, localizedButton("Copia impronta", () => call("agentCopyFingerprint", { fingerprint: key.fingerprint })));
        list.append(row);
      }
    } catch {
    } finally {
      read.disabled = false;
    }
  };
  body.append(localizedButton("Genera chiave hardware / ibrida", hardwareGenerationDialog));
  const help = document.createElement("details");
  const summary = document.createElement("summary");
  summary.textContent = uiText("Compatibilità e configurazione");
  help.append(summary);
  notice(help, uiText("Avvia prima l’agent e rendi disponibile la chiave. Eventuali richieste di PIN o tocco del token sono gestite dall’agent. Per PKCS#11 scegli esclusivamente il modulo originale del produttore; il PIN non viene salvato."));
  notice(help, uiText("La generazione -sk e disponibile tramite il client OpenSSH selezionato, con PIN e tocco nella scheda interattiva. Per il profilo SSH scegli lo stesso agent e, preferibilmente, l’impronta da usare. UNSC Relay non è certificato FIPS o CNSA."));
  body.append(help);
}
function certificateDialog(key) {
  const body = showModal(uiText("Certificato SSH / ") + key.name, "CA LOCALE / DURATA LIMITATA", keysDialog);
  if (key.certificate) {
    const cert = key.certificate;
    const revoked = isCertificateRevoked(cert);
    notice(body, revoked ? "CERTIFICATO REVOCATO: i nuovi accessi da questo archivio sono bloccati. La chiave privata e conservata." : "Certificato associato e disponibile per i controlli di validita.");
    notice(body, "ID: " + cert.keyId + " · Principal: " + cert.principals.join(", "));
    notice(body, uiText("Valido da ") + new Date(cert.validFrom).toLocaleString(document.documentElement.lang) + " fino a " + (cert.validUntil ? new Date(cert.validUntil).toLocaleString(document.documentElement.lang) : "senza scadenza"));
    notice(body, "CA: " + cert.authorityFingerprint);
    const tools = document.createElement("div");
    tools.className = "toolbar";
    tools.append(localizedButton("Esporta -cert.pub", () => call("certificateExport", { keyId: key.id })), localizedButton("Rimuovi associazione", async () => {
      update(await call("certificateRemove", { keyId: key.id }));
      keysDialog();
    }, "danger"));
    const revoke = localizedButton("Revoca certificato", () => revokeCertificateDialog(key), "danger");
    revoke.disabled = revoked;
    tools.append(revoke, localizedButton("Verifica in KRL esterna", async () => {
      const result = await call("certificateKrlQuery", { keyId: key.id });
      if (result && !result.cancelled) {
        notice(body, (result.revoked ? "REVOCATO nella lista " : "Non presente nella lista ") + result.fileName + ". La verifica esterna non modifica l’archivio o i server.");
      }
    }));
    body.append(tools);
  }
  body.append(localizedButton("CA esterna / HSM / rinnovo automatico", () => externalCertificateDialog(key)));
  body.append(localizedButton("Importa certificato esistente", async () => {
    update(await call("certificateImport", { keyId: key.id }));
    keysDialog();
  }));
  const caKeys = state.vault.keys.filter((k) => k.id !== key.id);
  if (!caKeys.length) {
    notice(body, uiText("Per emettere un certificato occorre una seconda chiave nel deposito da usare come CA. Puoi importare o generare una chiave separata."));
    return;
  }
  const form = document.createElement("form");
  form.className = "form-grid";
  const matchingCa = caKeys.find((k) => k.fingerprint === key.certificate?.authorityFingerprint);
  field(form, uiText("Chiave CA separata"), "caId", matchingCa?.id || caKeys[0].id, "text", caKeys.map((k) => [k.id, k.name]));
  field(form, uiText("Principal / username remoto"), "principal", state.data.profiles.find((p) => p.keyId === key.id)?.username || "");
  field(form, uiText("Durata"), "hours", "1", "text", [["1", "1 ora"], ["2", "2 ore"], ["4", "4 ore"], ["8", "8 ore"]]);
  const sign = document.createElement("button");
  sign.textContent = key.certificate ? "Rinnova con la CA selezionata" : "Emetti certificato";
  sign.className = "primary";
  form.append(sign);
  form.onsubmit = async (e) => {
    e.preventDefault();
    sign.disabled = true;
    const data = Object.fromEntries(new FormData(form));
    try {
      update(await call("certificateIssue", { keyId: key.id, caId: data.caId, principal: data.principal, hours: Number(data.hours) }));
      keysDialog();
    } catch {
      sign.disabled = false;
    }
  };
  body.append(form);
  notice(body, uiText("Il server deve già fidarsi della chiave pubblica della CA. L’app non configura il server e non distribuisce la chiave privata della CA. I certificati scaduti o destinati a un altro username vengono bloccati prima dell’accesso. Nessun rinnovo silenzioso o fallback alla chiave ordinaria."));
}
function cryptoCapabilitiesDialog() {
  const body = showModal(uiText("Algoritmi e disponibilità"), "CAPACITÀ REALI / NESSUNA CERTIFICAZIONE IMPLICITA", keysDialog);
  const entries = [["Ed25519", "Generazione, importazione e autenticazione nel motore integrato."], ["RSA 3072 / 4096 / 8192", "Generazione e autenticazione con firme RSA SHA-2. Il prefisso del file pubblico può restare ssh-rsa; la firma SHA-1 non viene offerta."], ["ECDSA P-256 / P-384 / P-521", "Generazione, importazione e autenticazione. L’uso di curve NIST non rende il programma certificato FIPS."], ["Ed25519-SK / ECDSA-SK", "Identità hardware tramite agent compatibile; richiedono token e supporto del produttore. Nessun dispositivo usato nelle verifiche di questa build."], ["Smart card / HSM", "Modulo PKCS#11 tramite agent, con PIN non salvato. Richiede hardware e driver compatibili."], ["Certificati SSH", "Importazione, emissione/rinnovo locale da 1 a 8 ore, revoca persistente nell’archivio, esportazione e verifica KRL OpenSSH. La KRL va distribuita esplicitamente ai server; agent esterni restano soggetti alle revoche configurate sui server."], ["Scambio chiavi post-quantistico", "Opzione per profilo: ammette soltanto ML-KEM/X25519 o sntrup/X25519 offerti da SSH.NET. Fallisce se il server non è compatibile."], ["Firma ssh-mldsa44-ed25519", "Disponibile nel client OpenSSH incluso: generazione, firma e sessione nativa con server compatibile. Non supportata dal percorso SSH.NET/deposito ordinario. Lo scambio chiavi post-quantistico e una funzione distinta."], ["ML-DSA-87 / CNSA 2.0", "Non implementato come algoritmo di autenticazione SSH in questa build; nessuna dichiarazione di conformità militare, CNSA o FIPS."]];
  for (const [title, text] of entries) {
    const heading = document.createElement("h3");
    heading.textContent = title;
    body.append(heading);
    notice(body, text);
  }
}
function isCertificateRevoked(certificate) {
  const blob = (t) => String(t || "").trim().split(/\s+/)[1];
  return (state.vault.revocations || []).some((r) => certificate.revocationIdentity && r.certificate.revocationIdentity ? r.certificate.revocationIdentity === certificate.revocationIdentity : blob(r.certificate.text) === blob(certificate.text));
}
function revokeCertificateDialog(key) {
  const body = showModal(uiText("Revoca certificato / ") + key.name, "CERTIFICATO SSH / REVOCA PERSISTENTE", () => certificateDialog(key));
  notice(body, uiText("Revoca questo certificato, mantenendo la chiave privata. I nuovi accessi con questo certificato dall’archivio Relay saranno bloccati; le sessioni gia aperte continuano. Un nuovo certificato potra essere emesso separatamente."));
  notice(body, uiText("Per impedire l’uso anche da altri client e agent, esporta la lista KRL e configurala sui server. Nessun file remoto viene modificato automaticamente. Le revoche restano nel backup cifrato anche rimuovendo il certificato o la chiave."));
  const form = document.createElement("form");
  const reason = field(form, uiText("Motivo della revoca"), "reason", "");
  reason.required = true;
  reason.maxLength = 500;
  const actions = document.createElement("div");
  actions.className = "form-actions";
  actions.append(localizedButton("Annulla", () => certificateDialog(key)));
  const submit = document.createElement("button");
  submit.className = "danger";
  submit.textContent = uiText("Conferma revoca");
  actions.append(submit);
  form.append(actions);
  body.append(form);
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (!form.reportValidity()) return;
    submit.disabled = true;
    try {
      update(await call("certificateRevoke", { keyId: key.id, reason: reason.value }));
      const refreshed = state.vault.keys.find((k) => k.id === key.id);
      if (refreshed) certificateDialog(refreshed);
      else keysDialog();
    } catch {
    } finally {
      submit.disabled = false;
    }
  };
}
function revocationsDialog() {
  const body = showModal(uiText("Certificati revocati"), "REGISTRO CIFRATO / OPENSSH KRL", keysDialog, true);
  notice(body, uiText("Le revoche bloccano i nuovi accessi con i certificati conservati in questo archivio. Non interrompono sessioni gia aperte, non revocano la chiave privata sottostante e non modificano i server."));
  notice(body, uiText("Esporta la KRL per distribuirla e configurarla sui server OpenSSH. Include tutte le revoche di questo archivio, anche per chiavi eliminate. Non sostituisce eventuali altre liste di revoca del server. Ripristinare un vecchio backup non contiene le revoche successive."));
  const entries = state.vault.revocations || [];
  const tools = document.createElement("div");
  tools.className = "toolbar";
  const exportButton = localizedButton("Esporta KRL OpenSSH", async () => {
    exportButton.disabled = true;
    try {
      const result = await call("certificateKrlExport");
      if (result?.exported) notice(body, uiText("KRL esportata: ") + result.fileName + " · " + result.count + " revoche. Nessuna modifica ai server.");
    } finally {
      exportButton.disabled = !entries.length;
    }
  }, "primary");
  exportButton.disabled = !entries.length;
  tools.append(exportButton);
  body.append(tools);
  if (!entries.length) {
    notice(body, uiText("Nessun certificato revocato. Seleziona una chiave con certificato e usa Certificato > Revoca certificato."));
    return;
  }
  const list = document.createElement("div");
  list.className = "revocations-list";
  for (const entry of entries) {
    const card = document.createElement("section");
    card.className = "revocation-card";
    const title = document.createElement("h3");
    title.textContent = entry.name;
    card.append(title);
    notice(card, uiText("Revocato il ") + new Date(entry.revokedAt).toLocaleString(document.documentElement.lang) + " · " + entry.reason);
    notice(card, uiText("ID certificato: ") + entry.certificate.keyId + " · Principal: " + entry.certificate.principals.join(", "));
    notice(card, "CA: " + entry.certificate.authorityFingerprint);
    list.append(card);
  }
  body.append(list);
}
