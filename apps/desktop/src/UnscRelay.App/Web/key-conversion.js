"use strict";
async function keyConversionDialog() {
  const body = showModal(uiText("Converti chiavi PPK / OpenSSH"), "PUTTYGEN / CONVERSIONE LOCALE", keysDialog);
  modal.classList.add("conversion-modal");
  const status = await call("conversionStatus");
  notice(body, uiText("PuTTYgen ") + status.toolVersion + " ufficiale è incluso nel programma. La conversione avviene nella sua finestra: nessuna chiave viene inviata online. Relay verifica il risultato confrontando l’impronta prima di importarlo.");
  if (!status.active) {
    const form2 = document.createElement("form");
    field(form2, uiText("Formato di destinazione"), "conversionTarget", "openssh", "text", [["openssh", "OpenSSH · formato moderno"], ["ppk", "PuTTY PPK · versione 3"]]);
    notice(form2, uiText("Scegli un file originale sul PC. Durante la conversione Relay lo mantiene aperto in sola lettura per impedirne la sostituzione. Nessuna esportazione delle chiavi private già custodite nell’archivio."));
    const start = document.createElement("button");
    start.className = "primary";
    start.textContent = uiText("Seleziona originale e apri PuTTYgen");
    form2.append(start);
    form2.onsubmit = async (e) => {
      e.preventDefault();
      start.disabled = true;
      try {
        const result = await call("conversionStart", { target: new FormData(form2).get("conversionTarget") });
        if (result) keyConversionDialog();
      } finally {
        start.disabled = false;
      }
    };
    body.append(form2);
    return;
  }
  const card = document.createElement("section");
  card.className = "conversion-summary";
  const title = document.createElement("h3");
  title.textContent = uiText("Originale: ") + status.source.fileName;
  const detail = document.createElement("p");
  detail.textContent = status.source.format + " · " + status.source.algorithm + " · " + status.source.bits + " bit";
  const fingerprint = document.createElement("code");
  fingerprint.textContent = status.source.fingerprint;
  card.append(title, detail, fingerprint);
  body.append(card);
  const steps = document.createElement("ol");
  for (const text of ["In PuTTYgen inserisci la passphrase dell’originale, se richiesta.", status.target === "ppk" ? "Premi Save private key e salva un nuovo file .ppk, mantenendo PPK versione 3." : "Dal menu Conversions scegli Export OpenSSH key (force new file format) e salva un nuovo file.", "Torna qui e seleziona il risultato per verificarne l’impronta."]) {
    const item = document.createElement("li");
    item.textContent = text;
    steps.append(item);
  }
  body.append(steps);
  notice(body, uiText("Puoi scegliere o cambiare la passphrase in PuTTYgen prima del salvataggio. Conserva entrambi i file finché non hai verificato il risultato. La conversione scade dopo 30 minuti. PuTTYgen resta aperto finché non lo chiudi."));
  const form = document.createElement("form");
  const name = field(form, uiText("Nome nell’archivio (per l’importazione)"), "conversionName", status.source.fileName.replace(/\.[^.]+$/, ""));
  name.maxLength = 100;
  const comment = field(form, uiText("Commento facoltativo"), "conversionComment", "");
  comment.maxLength = 1e3;
  const actions = document.createElement("div");
  actions.className = "form-actions";
  const verify = async (importing) => {
    const result = await call("conversionFinish", { id: status.id, import: importing, name: name.value, comment: comment.value });
    if (!result) return;
    if (result.snapshot) update(result.snapshot);
    const resultBody = showModal(uiText("Conversione verificata"), "STESSA CHIAVE / NUOVO FORMATO", keysDialog);
    notice(resultBody, result.fileName + " · " + result.format);
    const fp = document.createElement("code");
    fp.textContent = result.fingerprint;
    resultBody.append(fp);
    notice(resultBody, result.importedId ? "Chiave importata nell’archivio cifrato con il nome scelto. Gli originali non sono stati eliminati." : "Impronta identica all’originale. Il file convertito resta nella cartella che hai scelto in PuTTYgen.");
    resultBody.append(localizedButton("Chiavi salvate", keysDialog, "primary"));
  };
  const importButton = localizedButton("Verifica e importa nell’archivio", () => verify(true), "primary");
  importButton.disabled = !state.vault.unlocked;
  actions.append(localizedButton("Verifica soltanto", () => verify(false)), importButton);
  form.append(actions);
  body.append(form);
  if (!state.vault.unlocked) notice(body, uiText("Per importare, sblocca prima l’archivio da Chiavi salvate; la conversione in corso rimane disponibile."));
  body.append(localizedButton("Riapri PuTTYgen sull’originale", () => call("conversionOpen", { id: status.id })), localizedButton("Annulla conversione", async () => {
    await call("conversionCancel");
    keyConversionDialog();
  }, "danger"));
}
