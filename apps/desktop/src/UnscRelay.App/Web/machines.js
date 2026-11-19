"use strict";
function machineDialog(existing) {
  const body = showModal(existing ? "Modifica indirizzo" : "Nuovo indirizzo", "RUBRICA / NESSUNA CREDENZIALE");
  const form = document.createElement("form");
  const name = field(form, uiText("Nome macchina"), "machineName", existing?.name || "");
  name.required = true;
  name.maxLength = 100;
  const host = field(form, uiText("Host / indirizzo IP"), "machineHost", existing?.host || "");
  host.required = true;
  const port = field(form, uiText("Porta"), "machinePort", existing?.port || 22, "number");
  port.min = 1;
  port.max = 65535;
  port.required = true;
  const actions = document.createElement("div");
  actions.className = "form-actions";
  actions.append(localizedButton("Annulla", machinesDialog));
  const save = document.createElement("button");
  save.className = "primary";
  save.textContent = uiText("Salva indirizzo");
  actions.append(save);
  form.append(actions);
  form.onsubmit = async (e) => {
    e.preventDefault();
    save.disabled = true;
    try {
      update(await call("machineSave", { id: existing?.id || crypto.randomUUID().replaceAll("-", ""), name: name.value.trim(), host: host.value.trim(), port: Number(port.value) }));
      machinesDialog();
    } catch {
    } finally {
      save.disabled = false;
    }
  };
  body.append(form);
}
