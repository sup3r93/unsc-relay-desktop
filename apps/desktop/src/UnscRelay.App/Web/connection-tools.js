"use strict";
function saveConnectionTemplateDialog(profile) {
  const body = showModal(uiText("Salva modello di connessione"), uiText("MODELLI / IMPOSTAZIONI RIUTILIZZABILI"));
  notice(body, uiText("Il modello conserva tipo di sessione, porta, timeout, ambiente e opzioni di connessione. Indirizzi, utenti, password, chiavi, percorsi, note e automazioni non vengono copiati."));
  const form = document.createElement("form"), name = field(form, uiText("Nome del modello"), "templateName", profile.name);
  name.maxLength = 100;
  name.required = true;
  const save = document.createElement("button");
  save.type = "submit";
  save.className = "primary";
  save.textContent = uiText("Salva modello");
  form.append(save);
  form.onsubmit = async (e) => {
    e.preventDefault();
    save.disabled = true;
    try {
      update(await call("connectionTemplateSave", { profileId: profile.id, name: name.value }));
      modal.close();
      toast(uiText("Modello salvato."));
    } finally {
      save.disabled = false;
    }
  };
  body.append(form);
  name.focus();
}
function connectionTemplatesDialog() {
  const body = showModal(uiText("Modelli di connessione"), uiText("MODELLI / NUOVA CONNESSIONE"));
  notice(body, uiText("Ogni modello crea una nuova connessione da rivedere. Scegli le impostazioni da riusare; indirizzo e credenziali si inseriscono nel form, senza riuso automatico."));
  const list = document.createElement("div");
  body.append(list);
  const render = () => {
    list.replaceChildren();
    for (const template of state.data.connectionTemplates || []) {
      const row = document.createElement("section");
      row.className = "settings-card connection-template-card";
      const title = document.createElement("h3");
      title.className = "user-content";
      title.textContent = template.name;
      row.append(title);
      const network = advancedCheck(row, uiText("Riusa porta, timeout e opzioni SSH"), true), appearance = advancedCheck(row, uiText("Riusa l’ambiente della connessione"), true);
      row.append(localizedButton("Crea da modello", async () => {
        const profile = await call("connectionTemplateUse", { id: template.id, network: network.checked, appearance: appearance.checked });
        if (row.isConnected && modal.open) profileDialog(profile);
      }), localizedButton("Elimina modello", async () => {
        if (!await call("confirmAction", { text: uiText("Eliminare questo modello? Le connessioni esistenti restano disponibili.") })) return;
        update(await call("connectionTemplateDelete", { id: template.id }));
        render();
      }, "danger"));
      list.append(row);
    }
    if (!list.children.length) notice(list, uiText("Nessun modello salvato. Apri il menu di una connessione e scegli Salva modello di connessione."));
  };
  render();
}
async function connectionGuideDialog(profile) {
  const body = showModal(uiText("Verifica guidata della connessione"), uiText("VERIFICA / SENZA CREDENZIALI"));
  body.tabIndex = 0;
  const heading = document.createElement("h3");
  heading.className = "user-content";
  heading.textContent = profile.name;
  body.append(heading);
  notice(body, uiText("Vengono controllati soltanto DNS e porta TCP. Non vengono inviate credenziali o eseguiti comandi. Una porta aperta non conferma l’identità del server."));
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  status.textContent = uiText("Verifica in corso…");
  body.append(status);
  try {
    const result = await call("connectionProbe", { profileId: profile.id });
    if (!heading.isConnected || !modal.open) return;
    status.textContent = uiText("Verifica completata") + " · " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "medium" }).format(new Date(result.checkedAt));
    const names = { dns: "Indirizzo e DNS", tcp: "Porta TCP", authentication: "Autenticazione", "host-identity": "Identità del server", route: "Percorso di connessione", timeout: "Tempo massimo di verifica" }, states = { passed: "Riuscita", failed: "Non riuscita", "not-tested": "Da verificare nella connessione", skipped: "Non eseguita" };
    for (const step of result.steps) {
      const row = document.createElement("section");
      row.className = "settings-card";
      const h = document.createElement("h4");
      h.textContent = uiText(names[step.stage] || step.stage) + " · " + uiText(states[step.status] || step.status);
      const detail = document.createElement("p");
      detail.className = "user-content";
      detail.textContent = step.stage === "dns" && step.status === "passed" ? step.detail : uiText(step.detail);
      row.append(h, detail);
      body.append(row);
    }
  } catch (error) {
    if (heading.isConnected && modal.open) status.textContent = error.message;
  }
}
