"use strict";
async function mountAccountMethods(root) {
  const heading = document.createElement("h3");
  heading.textContent = uiText("Metodi di sicurezza registrati");
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  root.replaceChildren(heading, status);
  status.textContent = uiText("Caricamento…");
  try {
    const values = await rpc("accountMethods");
    if (!root.isConnected) return;
    status.textContent = uiText("Per salvare o revocare un metodo verrà richiesta una nuova verifica a due fattori.");
    let busy = false;
    for (const [index, m] of values.entries()) {
      const form = document.createElement("form");
      form.className = "account-method";
      const title = document.createElement("strong");
      title.textContent = { otp: "App OTP", webauthn: "Chiave FIDO2", "webauthn-passwordless": uiText("Passkey") }[m.type] || "Metodo di sicurezza";
      const date = document.createElement("small");
      date.textContent = m.created ? new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "medium" }).format(new Date(m.created)) : "Data di registrazione non disponibile";
      const label = document.createElement("label");
      label.textContent = uiText("Nome del metodo o dispositivo");
      const name = document.createElement("input");
      name.name = "name";
      name.id = "native-factor-" + index;
      name.value = m.name || "";
      name.required = true;
      name.maxLength = 80;
      name.autocomplete = "off";
      label.append(name);
      const controls = document.createElement("div");
      controls.className = "form-actions";
      const save = localizedButton("Salva nome", () => {
      });
      save.type = "submit";
      const remove = localizedButton("Revoca metodo", async () => {
        if (busy) return;
        if (!await rpc("confirmAction", { message: "Revocare " + (m.name || title.textContent) + "? Non sarà più possibile usarlo per accedere." })) return;
        await mutate("accountMethodRemove");
      }, "danger");
      const protectedLast = m.type === "otp" && values.filter((v) => v.type === "otp").length === 1;
      remove.disabled = protectedLast;
      if (protectedLast) {
        const note = document.createElement("small");
        note.textContent = uiText("Aggiungi e verifica un altro metodo OTP prima di revocare l’ultimo.");
        form.append(note);
      }
      async function mutate(action) {
        if (busy) return;
        busy = true;
        for (const button of root.querySelectorAll("button")) button.disabled = true;
        status.textContent = uiText("Conferma la tua identità nella finestra protetta…");
        try {
          await rpc(action, { id: m.id, name: name.value });
          await mountAccountMethods(root);
        } catch (error) {
          status.textContent = error.message;
          for (const button of root.querySelectorAll("button")) button.disabled = false;
          for (const control of root.querySelectorAll("[data-protected-factor]")) control.disabled = true;
        } finally {
          busy = false;
        }
      }
      if (protectedLast) remove.dataset.protectedFactor = "true";
      form.onsubmit = (e) => {
        e.preventDefault();
        void mutate("accountMethodRename");
      };
      controls.append(save, remove);
      form.prepend(title, date, label, controls);
      root.append(form);
    }
    if (!values.length) status.textContent = uiText("Nessun metodo registrato disponibile.");
  } catch (error) {
    status.textContent = error.message;
  }
}
async function personalAccountEditor(kind, onSaved) {
  const previous = document.activeElement;
  let profile = {};
  if (kind === "profile") {
    try {
      profile = await rpc("accountProfile");
    } catch (error) {
      toast(error.message, true);
      return;
    }
  }
  const dialog = document.createElement("dialog");
  dialog.className = "support-create-dialog account-personal-dialog";
  dialog.setAttribute("aria-labelledby", "personal-account-heading");
  const heading = document.createElement("h2");
  heading.id = "personal-account-heading";
  heading.textContent = kind === "profile" ? uiText("Modifica dati personali") : uiText("Cambia password dell’account");
  const help = document.createElement("p");
  help.id = "personal-account-help";
  help.textContent = kind === "profile" ? uiText("La verifica dell’identità viene richiesta quando salvi. Cambiare email chiude le sessioni online e richiede la verifica del nuovo indirizzo.") : "Questa è la password del tuo account UNSC. La password di cifratura dell’archivio non cambia. Dopo il salvataggio le sessioni online vengono chiuse.";
  dialog.setAttribute("aria-describedby", help.id);
  dialog.append(heading, help);
  const form = document.createElement("form");
  let dirty = false, busy = false, expired = false;
  const first = kind === "profile" ? field(form, uiText("Nome"), "firstName", profile.firstName || "") : null, last = kind === "profile" ? field(form, uiText("Cognome"), "lastName", profile.lastName || "") : null, email = kind === "profile" ? field(form, uiText("Email"), "email", profile.email || "", "email") : null;
  if (first) {
    first.maxLength = last.maxLength = 100;
    first.autocomplete = "given-name";
    last.autocomplete = "family-name";
    email.required = true;
    email.maxLength = 254;
    email.autocomplete = "email";
  }
  const password = kind === "password" ? field(form, uiText("Nuova password dell’account"), "password", "", "password") : null, repeat = kind === "password" ? field(form, uiText("Ripeti la nuova password"), "repeatPassword", "", "password") : null;
  if (password) {
    for (const input of [password, repeat]) {
      input.required = true;
      input.minLength = 12;
      input.maxLength = 1024;
      input.autocomplete = "new-password";
    }
    repeat.oninput = () => repeat.setCustomValidity("");
    const reveal = localizedButton("Mostra password", () => {
      const show = password.type === "password";
      password.type = repeat.type = show ? "text" : "password";
      reveal.textContent = show ? uiText("Nascondi password") : uiText("Mostra password");
      reveal.setAttribute("aria-pressed", String(show));
    });
    reveal.setAttribute("aria-pressed", "false");
    form.append(reveal);
  }
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  const actions = document.createElement("div");
  actions.className = "form-actions";
  const cancel = localizedButton("Annulla", () => tryClose()), save = localizedButton("Salva e conferma identità", () => {
  }, "primary");
  save.type = "submit";
  actions.append(cancel, save);
  form.append(actions, status);
  dialog.append(form);
  document.body.append(dialog);
  dialog.showModal();
  (first || password).focus();
  async function tryClose() {
    if (busy) return;
    if (dirty && !await rpc("confirmAction", { message: "Scartare le modifiche non salvate?" })) return;
    dialog.close();
  }
  dialog.addEventListener("cancel", (e) => {
    e.preventDefault();
    void tryClose();
  });
  dialog.addEventListener("close", () => {
    for (const input of [password, repeat]) if (input) input.value = "";
    dialog.remove();
    if (previous?.isConnected) previous.focus();
  }, { once: true });
  form.addEventListener("input", () => {
    dirty = true;
  });
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (busy) return;
    if (password && password.value !== repeat.value) {
      repeat.setCustomValidity(uiText("Le password non coincidono."));
      repeat.reportValidity();
      return;
    }
    busy = true;
    dialog.setAttribute("aria-busy", "true");
    for (const input of form.querySelectorAll("input,button")) input.disabled = true;
    status.textContent = uiText("Conferma l’identità nella finestra protetta…");
    try {
      const result = await rpc(kind === "profile" ? "accountProfileSave" : "accountPasswordSave", kind === "profile" ? { firstName: first.value.trim(), lastName: last.value.trim(), email: email.value.trim().toLowerCase() } : { password: password.value });
      dirty = false;
      if (password) password.value = repeat.value = "";
      if (result.reauthenticate) {
        expired = true;
        status.textContent = kind === "password" ? uiText("Password aggiornata. Accedi nuovamente con la nuova password.") : result.verificationSent ? uiText("Email aggiornata. Controlla la nuova casella e accedi di nuovo.") : uiText("Email aggiornata. L’invio della verifica non è riuscito: accedi di nuovo per richiederla.");
        const login = localizedButton("Accedi di nuovo", async () => {
          login.disabled = true;
          try {
            update(await call("accountLogin"));
            dialog.close();
            await accountProfileDialog();
          } catch (error) {
            status.textContent = error.message;
            login.disabled = false;
          }
        }, "primary");
        login.dataset.reauthenticate = "true";
        actions.append(login);
        save.hidden = true;
      } else {
        await onSaved?.();
        status.textContent = uiText("Dati personali aggiornati.");
      }
    } catch (error) {
      status.textContent = error.message + (kind === "password" ? " Se la connessione si è interrotta durante il salvataggio, verifica se la nuova password è già attiva prima di riprovare." : "");
    } finally {
      busy = false;
      dialog.setAttribute("aria-busy", "false");
      for (const input of form.querySelectorAll("input,button")) input.disabled = expired && input !== cancel && !input.dataset.reauthenticate;
    }
  };
}
