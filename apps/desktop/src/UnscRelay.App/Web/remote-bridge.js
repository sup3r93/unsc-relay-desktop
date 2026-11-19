"use strict";
async function remoteBridgeDialog(pane, selected) {
  const files = selected ? [selected] : selectedFiles(pane, "remote");
  if (files.length !== 1 || files[0].directory || files[0].link) return toast(uiText("Seleziona un solo file normale da copiare."), true);
  const file = files[0], body = showModal(uiText("Copia su altro server"), pane.profile.name, null, true), marker = document.createElement("span"), help = document.createElement("p"), form = document.createElement("form"), source = document.createElement("p"), targetLabel = document.createElement("label"), target = document.createElement("select"), pathLabel = document.createElement("label"), path = document.createElement("input"), replaceLabel = document.createElement("label"), replace = document.createElement("input"), actions = document.createElement("div"), status = document.createElement("p"), report = document.createElement("dl");
  let targets = [], plan = null, busy = false;
  help.textContent = uiText("Copia un file tra due sessioni SFTP già aperte. I dati passano nella memoria del PC, senza una copia completa sul disco locale. Il server di destinazione conserva un file parziale fino alla verifica. La sorgente non viene eliminata.");
  source.textContent = file.path;
  source.className = "user-content";
  targetLabel.textContent = uiText("Sessione di destinazione");
  targetLabel.append(target);
  pathLabel.textContent = uiText("Percorso assoluto di destinazione");
  path.type = "text";
  path.required = true;
  path.maxLength = 4e3;
  pathLabel.append(path);
  replace.type = "checkbox";
  replaceLabel.append(replace, document.createTextNode(uiText("Consenti la sostituzione del file esistente dopo la verifica")));
  replaceLabel.className = "toggle-row";
  status.setAttribute("role", "status");
  report.className = "server-information";
  actions.className = "form-actions";
  const preview = localizedButton("Verifica copia", async () => {
    if (busy || !form.reportValidity()) return;
    busy = true;
    preview.disabled = enqueue.disabled = true;
    if (plan) void rpc("fileBridgeCancel", { id: plan.id }).catch(() => {
    });
    plan = null;
    try {
      const result = await rpc("fileBridgePlan", { sessionId: pane.id, targetSessionId: target.value, sourcePath: file.path, targetPath: path.value });
      if (!live()) {
        void rpc("fileBridgeCancel", { id: result.id }).catch(() => {
        });
        return;
      }
      plan = result;
      report.replaceChildren();
      for (const [key, value] of [["File da copiare", capacityText(result.preview.size)], ["Destinazione esistente", uiText(result.preview.destinationExists ? "Sì" : "No")], ["Spazio disponibile", capacityText(result.preview.availableBytes)], ["Spazio temporaneo sul PC", capacityText(result.preview.temporaryDiskBytes)], ["Buffer di copia", capacityText(result.preview.memoryBufferBytes)]]) {
        const dt = document.createElement("dt"), dd = document.createElement("dd");
        dt.textContent = uiText(key);
        dd.textContent = value;
        report.append(dt, dd);
      }
      status.textContent = uiText("Anteprima valida per cinque minuti. Puoi mettere in pausa e riprendere dalla coda; dopo una disconnessione riapri entrambe le sessioni.");
      enqueue.disabled = result.preview.destinationExists && !replace.checked;
    } catch (error) {
      if (live()) status.textContent = error.message;
    } finally {
      busy = false;
      preview.disabled = false;
    }
  }), enqueue = localizedButton("Accoda copia", async () => {
    if (busy || !plan) return;
    busy = true;
    preview.disabled = enqueue.disabled = true;
    try {
      await rpc("fileBridgeEnqueue", { id: plan.id, replace: replace.checked });
      if (live()) {
        modal.close();
        toast(uiText("Copia aggiunta alla coda."));
      }
    } catch (error) {
      if (live()) status.textContent = error.message;
    } finally {
      busy = false;
      preview.disabled = false;
      enqueue.disabled = !plan || plan.preview.destinationExists && !replace.checked;
    }
  }, "primary");
  enqueue.disabled = true;
  preview.disabled = true;
  function invalidate() {
    if (plan) void rpc("fileBridgeCancel", { id: plan.id }).catch(() => {
    });
    plan = null;
    enqueue.disabled = true;
    report.replaceChildren();
    status.textContent = "";
  }
  target.onchange = () => {
    const item = targets.find((item2) => item2.id === target.value);
    if (item) path.value = item.directory.replace(/\/$/, "") + "/" + file.path.split("/").at(-1);
    invalidate();
  };
  path.oninput = invalidate;
  replace.onchange = () => enqueue.disabled = !plan || plan.preview.destinationExists && !replace.checked;
  form.onsubmit = (e) => e.preventDefault();
  actions.append(preview, enqueue);
  form.className = "portal-form";
  form.append(targetLabel, pathLabel, replaceLabel, actions);
  marker.hidden = true;
  body.append(marker, help, source, form, status, report);
  function live() {
    return marker.isConnected && modal.open;
  }
  function dispose() {
    if (plan) void rpc("fileBridgeCancel", { id: plan.id }).catch(() => {
    });
    plan = null;
    watcher.disconnect();
    modal.removeEventListener("close", dispose);
  }
  const watcher = new MutationObserver(() => {
    if (!marker.isConnected) dispose();
  });
  watcher.observe(body, { childList: true });
  modal.addEventListener("close", dispose, { once: true });
  try {
    targets = (await rpc("fileBridgeTargets")).filter((item) => item.id !== pane.id);
    if (!live()) return;
    for (const item of targets) target.append(new Option(item.name, item.id));
    if (!targets.length) {
      status.textContent = uiText("Apri prima una seconda sessione SFTP.");
      return;
    }
    target.onchange();
    preview.disabled = false;
  } catch (error) {
    if (live()) status.textContent = error.message;
  }
}
