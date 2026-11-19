"use strict";
function connectionNotesDialog(profile) {
  let expected = structuredClone({ notes: profile.notes || "", operations: profile.operations || { contact: "", maintenance: "", checklist: [] } });
  const body = showModal(uiText("Note e checklist"), profile.name, null, true), form = document.createElement("form"), help = document.createElement("p"), list = document.createElement("div"), status = document.createElement("p");
  help.textContent = uiText("Istruzioni e attività sono appunti: non eseguono comandi. Non inserire password o chiavi private. Le note fanno parte del profilo e delle sue copie sincronizzate.");
  body.append(help, form);
  status.setAttribute("role", "status");
  function area(title, value, limit) {
    const label = document.createElement("label"), input = document.createElement("textarea");
    label.className = "archive-field";
    input.id = "connection-note-" + crypto.randomUUID();
    label.htmlFor = input.id;
    label.textContent = uiText(title);
    input.value = value || "";
    input.maxLength = limit;
    input.rows = 4;
    form.append(label, input);
    return input;
  }
  const notes = area("Istruzioni operative", profile.notes, 32768), contact = area("Contatti di riferimento", profile.operations?.contact, 1e3), maintenance = area("Note di manutenzione", profile.operations?.maintenance, 8e3);
  const heading = document.createElement("h3");
  heading.textContent = uiText("Checklist operativa");
  form.append(heading, list);
  const add = localizedButton("Aggiungi attività", () => row({ id: crypto.randomUUID().replaceAll("-", ""), text: "", completed: false }, true));
  function row(task, focus = false) {
    const group = document.createElement("fieldset"), legend = document.createElement("legend"), label = document.createElement("label"), input = document.createElement("input"), done = document.createElement("input"), doneLabel = document.createElement("label");
    legend.textContent = uiText("Attività");
    group.dataset.taskId = task.id;
    label.textContent = uiText("Descrizione attività");
    input.type = "text";
    input.value = task.text;
    input.maxLength = 300;
    input.required = true;
    label.append(input);
    done.type = "checkbox";
    done.checked = !!task.completed;
    doneLabel.append(done, document.createTextNode(uiText("Completata")));
    const remove = localizedButton("Rimuovi attività", () => {
      group.remove();
      add.disabled = list.childElementCount >= 30;
      add.focus();
    });
    group.append(legend, label, doneLabel, remove);
    list.append(group);
    add.disabled = list.childElementCount >= 30;
    if (focus) input.focus();
  }
  for (const task of profile.operations?.checklist || []) row(task);
  let confirmingDiscard = false;
  const draft = () => ({ notes: notes.value, operations: { contact: contact.value, maintenance: maintenance.value, checklist: [...list.children].map((group) => ({ id: group.dataset.taskId, text: group.querySelector("input[type=text]").value.trim(), completed: group.querySelector("input[type=checkbox]").checked })) } });
  async function discard(event) {
    if (!form.isConnected || JSON.stringify(draft()) === JSON.stringify(expected)) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    if (confirmingDiscard) return;
    confirmingDiscard = true;
    try {
      if (await call("confirmAction", { text: uiText("Scartare le modifiche non salvate a note e checklist?") })) modal.close();
    } finally {
      confirmingDiscard = false;
    }
  }
  const close = $("modal-close");
  close.addEventListener("click", discard, true);
  modal.addEventListener("cancel", discard, true);
  modal.addEventListener("close", () => {
    close.removeEventListener("click", discard, true);
    modal.removeEventListener("cancel", discard, true);
  }, { once: true });
  const save = localizedButton("Salva note e checklist", () => {
  }, "primary");
  save.type = "submit";
  form.append(add, save, status);
  form.onsubmit = async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    const checklist = [...list.children].map((group) => ({ id: group.dataset.taskId, text: group.querySelector("input[type=text]").value.trim(), completed: group.querySelector("input[type=checkbox]").checked }));
    save.disabled = true;
    try {
      const proposed = { notes: notes.value, operations: { contact: contact.value, maintenance: maintenance.value, checklist } };
      update(await call("connectionNotes", { profileId: profile.id, ...proposed, expected }));
      expected = structuredClone(proposed);
      if (form.isConnected) status.textContent = uiText("Note e checklist salvate.");
    } catch (error) {
      if (form.isConnected) status.textContent = error.message;
    } finally {
      save.disabled = false;
    }
  };
}
