"use strict";
function serverInfoDialog(pane) {
  const body = showModal(uiText("Informazioni del server"), pane.profile.name, null, true), help = document.createElement("p"), status = document.createElement("p"), details = document.createElement("dl");
  help.textContent = uiText("Rilevamento su richiesta tramite SFTP: cartella, permessi e spazio quando supportato. Non esegue comandi; sistema operativo e carico non vengono rilevati. Lo spazio non include necessariamente le quote personali.");
  status.setAttribute("role", "status");
  details.className = "server-information";
  const read = localizedButton("Rileva informazioni", async () => {
    read.disabled = true;
    status.textContent = uiText("Caricamento…");
    try {
      let row = function(key, value2) {
        const term = document.createElement("dt"), description = document.createElement("dd");
        term.textContent = uiText(key);
        description.textContent = value2;
        details.append(term, description);
      };
      const value = await rpc("remoteServerInfo", { sessionId: pane.id, path: pane.remotePath });
      if (!details.isConnected) return;
      details.replaceChildren();
      row("Cartella remota", value.directory);
      row("Permessi", value.permissions);
      row("UID / GID", value.owner + " / " + value.group);
      row("Spazio totale", capacityText(value.totalBytes));
      row("Spazio disponibile", capacityText(value.availableBytes));
      row("Sola lettura", value.readOnly == null ? uiText("Non disponibile") : uiText(value.readOnly ? "Sì" : "No"));
      status.textContent = uiText("Ultimo aggiornamento") + ": " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(value.checkedAt));
    } catch (error) {
      if (details.isConnected) status.textContent = error.message;
    } finally {
      read.disabled = false;
    }
  }, "primary");
  body.append(help, read, status, details);
}
function capacityText(bytes) {
  if (bytes == null || !Number.isFinite(bytes) || bytes < 0) return uiText("Non disponibile");
  const units = ["B", "KiB", "MiB", "GiB", "TiB"];
  let index = 0;
  while (bytes >= 1024 && index < units.length - 1) {
    bytes /= 1024;
    index++;
  }
  return bytes.toLocaleString(document.documentElement.lang, { maximumFractionDigits: 2 }) + " " + units[index];
}
async function transferPreflightDialog(pane, side) {
  const files = selectedFiles(pane, side);
  if (!files.length) return toast(uiText("Seleziona uno o più file o cartelle."), true);
  const body = showModal(uiText("Verifica preliminare del trasferimento"), pane.profile.name, null, true), marker = document.createElement("div"), status = document.createElement("p");
  status.setAttribute("role", "status");
  status.textContent = uiText("Caricamento…");
  body.append(marker, status);
  try {
    const plan = await rpc("filePlan", { sessionId: pane.id, direction: side === "local" ? "upload" : "download", paths: files.map((file) => file.path), localDirectory: pane.localPath, remoteDirectory: pane.remotePath });
    if (!marker.isConnected) return;
    const report = document.createElement("dl");
    report.className = "server-information";
    for (const [key, value] of [["Elementi pianificati", plan.items.length], ["Spazio da preparare", capacityText(plan.preflight?.requiredBytes)], ["Spazio disponibile", capacityText(plan.preflight?.availableBytes)], ["Destinazioni esistenti", plan.preflight?.existingDestinations ?? uiText("Non disponibile")], ["Collegamenti saltati", plan.skippedLinks || 0]]) {
      const term = document.createElement("dt"), description = document.createElement("dd");
      term.textContent = uiText(key);
      description.textContent = String(value);
      report.append(term, description);
    }
    body.append(report);
    status.textContent = uiText("Verifica completata senza avviare trasferimenti. Lo spazio e le destinazioni possono cambiare prima dell’avvio.");
  } catch (error) {
    if (marker.isConnected) status.textContent = error.message;
  }
}
