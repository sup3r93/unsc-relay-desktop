"use strict";
class StartupMacroValidationError extends Error {
}
function renderStartupMacroEditor(parent, profile) {
  const section = document.createElement("section");
  section.className = "startup-macro-editor";
  parent.append(section);
  const enabledLabel = document.createElement("label");
  enabledLabel.className = "check-label";
  const enabled = document.createElement("input");
  enabled.type = "checkbox";
  enabled.name = "startupMacroEnabled";
  enabled.checked = !!profile.startupMacro?.enabled;
  enabledLabel.append(enabled, "Esegui una macro all’apertura della sessione");
  section.append(enabledLabel);
  notice(section, uiText("La macro è una copia dello snippet nel profilo. Le successive modifiche alla libreria non cambiano comandi già approvati. Non inserire password o token: testo e variabili sono salvati in chiaro."));
  const original = profile.startupMacro ? structuredClone(profile.startupMacro) : null;
  let snapshot = original;
  const choices = [["", "Nessuna macro"], ...state.data.snippets.map((s) => [s.id, s.name + " / " + s.folder])];
  if (original && !choices.some(([id]) => id === original.snippetId)) choices.push([original.snippetId, original.name + " / copia salvata nel profilo"]);
  const select = field(section, uiText("Snippet da usare come macro"), "startupSnippetId", original?.snippetId || "", "text", choices);
  const refresh = localizedButton("Aggiorna copia dalla libreria", () => load(true));
  section.append(refresh);
  const confirmLabel = document.createElement("label");
  confirmLabel.className = "check-label";
  const confirm = document.createElement("input");
  confirm.type = "checkbox";
  confirm.checked = original?.confirmEachLaunch !== false;
  confirmLabel.append(confirm, "Chiedi conferma a ogni apertura");
  section.append(confirmLabel);
  const variables = document.createElement("div");
  variables.className = "form-grid macro-variables";
  section.append(variables);
  const preview = document.createElement("pre");
  preview.className = "snippet-code";
  interfaceAttribute(preview, 'aria-label', "Anteprima macro di avvio");
  section.append(preview);
  const status = document.createElement("p");
  status.className = "notice";
  status.setAttribute("role", "status");
  section.append(status);
  let inputs = /* @__PURE__ */ new Map();
  function render() {
    variables.replaceChildren();
    inputs = /* @__PURE__ */ new Map();
    if (snapshot) {
      for (const name of [...new Set([...snapshot.text.matchAll(/\{\{([a-zA-Z0-9_]+)\}\}/g)].map((m) => m[1]))]) {
        const input = field(variables, name, "startupMacroVar-" + name, snapshot.variables?.[name] || "");
        input.maxLength = 2048;
        input.oninput = update;
        inputs.set(name, input);
      }
    }
    refresh.disabled = !state.data.snippets.some((s) => s.id === select.value);
    update();
  }
  function update() {
    preview.textContent = snapshot ? snapshot.text.replace(/\{\{([a-zA-Z0-9_]+)\}\}/g, (_, name) => inputs.get(name)?.value || "{{" + name + "}}") : "Seleziona uno snippet per vedere il comando.";
    status.textContent = enabled.checked ? "Esecuzione alla nuova apertura, anche da workspace. Esclusi SFTP e riconnessioni automatiche." : "Macro disattivata: nessun comando aggiuntivo verrà eseguito.";
    for (const input of inputs.values()) input.required = enabled.checked;
  }
  function load(force = false) {
    const item = state.data.snippets.find((s) => s.id === select.value);
    if (!force && original?.snippetId === select.value) {
      snapshot = structuredClone(original);
    } else snapshot = item ? { snippetId: item.id, name: item.name, text: item.text, variables: Object.fromEntries([...inputs.entries()].map(([key, input]) => [key, input.value])) } : null;
    render();
  }
  select.onchange = () => load();
  enabled.onchange = update;
  render();
  notice(section, uiText("Le variabili sono sostituzioni testuali: controlla nell’anteprima spazi e virgolette del comando."));
  notice(section, uiText("SSH: il testo viene inviato al terminale autenticato dopo gli hook di connessione. Shell locali: viene eseguito dopo i comandi iniziali. OpenSSH incluso: comando remoto dopo l’autenticazione e poi shell interattiva POSIX; sostituisce un eventuale RemoteCommand del config. Non disponibile per eseguibili esterni."));
  return () => {
    if (!select.value || !snapshot) {
      if (enabled.checked) throw new StartupMacroValidationError("Seleziona uno snippet prima di abilitare la macro.");
      return null;
    }
    const values = Object.fromEntries([...inputs].map(([name, input]) => [name, input.value]));
    if (enabled.checked && Object.values(values).some((v) => !v.trim())) throw new StartupMacroValidationError("Compila tutte le variabili della macro.");
    return { ...snapshot, variables: values, enabled: enabled.checked, confirmEachLaunch: confirm.checked };
  };
}
