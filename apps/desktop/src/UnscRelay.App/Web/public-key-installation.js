"use strict";
async function publicKeyInstallationDialog(pane) {
  if (!["ssh", "sftp"].includes(pane.profile.protocol)) return toast(uiText("La procedura richiede una sessione SSH/SFTP del motore integrato."), true);
  const body = showModal(uiText("Installa chiave pubblica"), pane.profile.name, null, true), marker = document.createElement("span"), help = document.createElement("p"), form = document.createElement("form"), keyLabel = document.createElement("label"), keys = document.createElement("select"), phraseLabel = document.createElement("label"), phrase = document.createElement("input"), actions = document.createElement("div"), status = document.createElement("p"), details = document.createElement("dl"), wire = document.createElement("pre");
  let plan = null, busy = false;
  help.textContent = uiText("La procedura usa il percorso standard .ssh/authorized_keys della sessione SFTP. Conserva le righe esistenti e le loro restrizioni. Il profilo resta invariato: prima di cambiare metodo di accesso verifica il risultato. Su configurazioni personalizzate serve una verifica manuale.");
  keyLabel.textContent = uiText("Chiave software da installare");
  keyLabel.append(keys);
  phraseLabel.textContent = uiText("Passphrase della chiave, se presente");
  phrase.type = "password";
  phrase.autocomplete = "off";
  phrase.maxLength = 8192;
  phraseLabel.append(phrase);
  status.setAttribute("role", "status");
  details.className = "server-information";
  wire.className = "user-content";
  wire.style.whiteSpace = "pre-wrap";
  wire.style.overflowWrap = "anywhere";
  form.className = "portal-form";
  actions.className = "form-actions";
  function cancelPlan() {
    if (plan) void rpc("keyInstallCancel", { id: plan.id }).catch(() => {
    });
    plan = null;
    apply.disabled = true;
    details.replaceChildren();
    wire.textContent = "";
  }
  const preview = localizedButton("Verifica configurazione SSH", async () => {
    if (busy) return;
    busy = true;
    cancelPlan();
    status.setAttribute("role", "status");
    preview.disabled = true;
    try {
      const value = await rpc("keyInstallPreview", { sessionId: pane.id, keyId: keys.value });
      if (!live()) {
        void rpc("keyInstallCancel", { id: value.id }).catch(() => {
        });
        return;
      }
      plan = value;
      for (const [label, value2] of [["File remoto", plan.preview.path], ["Impronta", plan.preview.fingerprint], ["Cartella esistente", uiText(plan.preview.directoryExists ? "Sì" : "No")], ["File esistente", uiText(plan.preview.fileExists ? "Sì" : "No")], ["Permessi cartella", plan.preview.directoryPermissions], ["Permessi file", plan.preview.filePermissions]]) {
        const dt = document.createElement("dt"), dd = document.createElement("dd");
        dt.textContent = uiText(label);
        dd.textContent = value2;
        details.append(dt, dd);
      }
      wire.textContent = plan.preview.publicKey;
      status.textContent = uiText(plan.preview.alreadyPresent ? "La chiave è già presente: le restrizioni esistenti non vengono rimosse." : "Verrà aggiunta soltanto questa chiave pubblica. Nessuna riga esistente viene rimossa.");
      apply.textContent = uiText(plan.preview.alreadyPresent ? "Verifica nuovo accesso" : "Installa e verifica accesso");
      apply.disabled = false;
    } catch (error) {
      if (live()) status.textContent = error.message;
    } finally {
      busy = false;
      preview.disabled = false;
    }
  }), apply = localizedButton("Installa e verifica accesso", async () => {
    if (busy || !plan) return;
    busy = true;
    preview.disabled = apply.disabled = keys.disabled = true;
    const passphrase = phrase.value;
    phrase.value = "";
    status.setAttribute("role", "status");
    status.textContent = uiText("Installazione e verifica in corso…");
    try {
      const value = await rpc("keyInstallApply", { id: plan.id, passphrase });
      if (!live()) return;
      plan = null;
      status.textContent = value.message;
      status.setAttribute("role", value.verified ? "status" : "alert");
    } catch (error) {
      if (live()) {
        status.textContent = error.message + " " + uiText("L’installazione potrebbe aver creato una cartella o un file parziale. I vecchi accessi non vengono rimossi.");
        status.setAttribute("role", "alert");
      }
    } finally {
      phrase.value = "";
      busy = false;
      preview.disabled = keys.disabled = false;
      apply.disabled = true;
      if (plan) void rpc("keyInstallCancel", { id: plan.id }).catch(() => {
      });
      plan = null;
    }
  }, "primary");
  apply.disabled = preview.disabled = true;
  keys.onchange = cancelPlan;
  form.onsubmit = (e) => e.preventDefault();
  actions.append(preview, apply);
  form.append(keyLabel, phraseLabel, actions);
  marker.hidden = true;
  body.append(marker, help, form, status, details, wire);
  function live() {
    return marker.isConnected && modal.open;
  }
  function dispose() {
    phrase.value = "";
    cancelPlan();
    wire.textContent = "";
    details.replaceChildren();
    watcher.disconnect();
    modal.removeEventListener("close", dispose);
  }
  const watcher = new MutationObserver(() => {
    if (!marker.isConnected) dispose();
  });
  watcher.observe(body, { childList: true });
  modal.addEventListener("close", dispose, { once: true });
  try {
    const values = await rpc("keyInstallKeys");
    if (!live()) return;
    for (const key of values) keys.append(new Option(key.name + " · " + key.fingerprint, key.id));
    preview.disabled = !values.length;
    if (!values.length) status.textContent = uiText("Genera o importa prima una chiave software nel Client Key Manager.");
  } catch (error) {
    if (live()) status.textContent = error.message;
  }
}
