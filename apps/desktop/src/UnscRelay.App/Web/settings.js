"use strict";
function renderSettingsDialog(initial = "general") {
  const body = showModal(uiText("Impostazioni"), "UNSC RELAY / BUILD " + state.version);
  modal.classList.add("settings-modal");
  interfaceLabel($("modal-title"), "Impostazioni");
  const layout = document.createElement("div");
  layout.className = "settings-layout";
  const nav = document.createElement("div");
  nav.className = "settings-nav";
  nav.setAttribute("role", "tablist");
  interfaceAttribute(nav, 'aria-label', "Categorie impostazioni");
  nav.setAttribute("aria-orientation", "vertical");
  const content = document.createElement("div");
  content.className = "settings-content";
  layout.append(nav, content);
  body.append(layout);
  const tabs = [];
  let preview = false, disposed = false;
  const cleanup = () => {
    if (disposed) return;
    disposed = true;
    if (preview) startAppMusic();
    window.refreshSettingsAccess = null;
    window.disposeSettingsPreview = null;
    modal.removeEventListener("close", cleanup);
  };
  window.disposeSettingsPreview = cleanup;
  modal.addEventListener("close", cleanup);
  function section(id, title, description) {
    const tab = localizedButton(title, () => select(id));
    tab.id = "settings-tab-" + id;
    tab.setAttribute("role", "tab");
    tab.setAttribute("aria-controls", "settings-panel-" + id);
    const panel = document.createElement("section");
    panel.id = "settings-panel-" + id;
    panel.className = "settings-panel";
    panel.setAttribute("role", "tabpanel");
    panel.setAttribute("aria-labelledby", tab.id);
    const h = document.createElement("h3");
    interfaceUiLabel(h, title);
    panel.append(h);
    notice(panel, description);
    nav.append(tab);
    content.append(panel);
    tabs.push({ id, tab, panel });
    return panel;
  }
  function select(id) {
    for (const t of tabs) {
      const active = t.id === id;
      t.tab.setAttribute("aria-selected", String(active));
      t.tab.tabIndex = active ? 0 : -1;
      t.panel.hidden = !active;
    }
  }
  nav.onkeydown = (e) => {
    const i = tabs.findIndex((t) => t.tab === document.activeElement);
    if (i < 0) return;
    let n = i;
    if (["ArrowDown", "ArrowRight"].includes(e.key)) n = (i + 1) % tabs.length;
    else if (["ArrowUp", "ArrowLeft"].includes(e.key)) n = (i + tabs.length - 1) % tabs.length;
    else if (e.key === "Home") n = 0;
    else if (e.key === "End") n = tabs.length - 1;
    else return;
    e.preventDefault();
    select(tabs[n].id);
    tabs[n].tab.focus();
  };
  function card(parent, title) {
    const c = document.createElement("section");
    c.className = "settings-card";
    const h = document.createElement("h4");
    interfaceUiLabel(h, title);
    c.append(h);
    parent.append(c);
    return c;
  }
  function check(parent, label, value) {
    const l = document.createElement("label");
    l.className = "check-label";
    const input = document.createElement("input");
    input.type = "checkbox";
    input.checked = !!value;
    const caption = interfaceUiLabel(document.createElement("span"), label);
    input.id = "relay-check-" + crypto.randomUUID();
    l.htmlFor = input.id;
    l.append(input, caption);
    parent.append(l);
    return input;
  }
  function actions(parent, ...buttons) {
    const row = document.createElement("div");
    row.className = "settings-actions";
    row.append(...buttons);
    parent.append(row);
  }
  function action(label, fn, primary = false) {
    const b = localizedButton(interfaceKnownKey(label) ?? label, async () => {
      b.disabled = true;
      try {
        await fn();
      } catch {
      } finally {
        b.disabled = false;
      }
    }, primary ? "primary" : "");
    return b;
  }
  const account = section("account", "Account e sincronizzazione", "Archivio cifrato personale su OpenBao, con copia offline sul PC.");
  mountAccountSettings(account);
  const protection = card(account, uiText("Sicurezza e assistenza"));
  notice(protection, uiText("Il blocco di Windows blocca anche Relay e l’archivio. Le sessioni restano aperte; per riprendere serve l’account online con 2FA oppure Windows Hello abilitato manualmente."));
  actions(protection, action(uiText("Diagnostica senza dati personali"), diagnosticDialog), action(uiText("Profilo, sessioni e sicurezza online"), () => call("accountSite")));
  const general = section("general", "Generali", "Profili e dati del programma. Le preferenze di sicurezza sono nella scheda Accesso.");
  createLanguageSelector(general);
  const lifecycle = card(general, uiText("Chiusura e aggiornamenti"));
  const trayCheck = check(lifecycle, uiText("Alla chiusura mantieni Relay nell’area di notifica"), state.data.settings.closeToTray);
  notice(lifecycle, uiText("La X nasconde la finestra e mantiene attive le sessioni. Per chiudere completamente usa Esci da Relay nel menu dell’icona. Il blocco automatico e il blocco di Windows restano attivi."));
  const startupUpdate = check(lifecycle, uiText("Controlla aggiornamenti all’avvio"), state.data.settings.checkUpdatesOnStartup !== false);
  const dailyUpdate = check(lifecycle, uiText("Controlla aggiornamenti ogni giorno mentre Relay è aperto"), state.data.settings.checkUpdatesDaily !== false);
  notice(lifecycle, uiText("Il controllo non scarica né installa automaticamente. Gli aggiornamenti critici firmati possono richiedere l’installazione prima di riprendere l’uso."));
  actions(lifecycle, action(uiText("Salva chiusura e aggiornamenti"), async () => {
    update(await call("lifecycleSettings", { closeToTray: trayCheck.checked, checkUpdatesOnStartup: startupUpdate.checked, checkUpdatesDaily: dailyUpdate.checked }));
    toast(uiText("Preferenze salvate."));
  }));
  const resources = card(general, uiText("Rete e risorse"));
  const metered = check(resources, uiText("Usa modalità connessione a consumo"), !!state.data.settings.meteredConnection), saving = check(resources, uiText("Riduci animazioni e lampeggiamento dei terminali"), !!state.data.settings.resourceSaving);
  notice(resources, uiText("La modalità a consumo è manuale: sospende la sincronizzazione automatica e il caricamento automatico dei salvataggi esterni. Le operazioni avviate esplicitamente restano disponibili; quelle già in corso non vengono interrotte."));
  notice(resources, uiText("Le verifiche di sicurezza e il controllo delle versioni restano attivi. Nessun aggiornamento viene scaricato automaticamente."));
  notice(resources, uiText("Il risparmio risorse raggruppa l’output delle schede inattive e lo mostra quando torni alla scheda. Le sessioni e le registrazioni continuano; l’ordine del testo viene conservato."));
  actions(resources, action(uiText("Salva rete e risorse"), async () => {
    update(await call("resourceSettings", { meteredConnection: metered.checked, resourceSaving: saving.checked }));
    toast(uiText("Preferenze salvate."));
  }));
  const preferenceSync = card(general, uiText("Preferenze sincronizzate"));
  const syncLanguage = check(preferenceSync, uiText("Sincronizza la lingua su questo dispositivo"), !!state.data.settings.syncLanguage), syncMusic = check(preferenceSync, uiText("Sincronizza la musica su questo dispositivo"), !!state.data.settings.syncMusic);
  notice(preferenceSync, uiText("Sincronizza le voci selezionate nell’archivio cifrato. Sicurezza, editor, aggiornamenti e rete restano locali. Disattivare una voce conserva l’ultima copia online."));
  actions(preferenceSync, action(uiText("Salva scelta di sincronizzazione"), async () => {
    update(await call("preferenceSyncSettings", { language: syncLanguage.checked, music: syncMusic.checked }));
    toast(uiText("Preferenze salvate."));
  }));
  const dataCard = card(general, uiText("Dati locali"));
  notice(dataCard, uiText("Profili e archivio rimangono nei dati personali anche dopo gli aggiornamenti. L’esportazione dei profili non include le chiavi private."));
  actions(dataCard, action(uiText("Esporta profili"), () => call("exportProfiles")), action(uiText("Importa profili"), async () => {
    update(await call("importProfiles"));
    toast(uiText("Profili importati."));
  }), action(uiText("Apri dati locali"), () => call("openData")));
  notice(general, uiText("Versione installata: ") + state.version);
  const access = section("access", "Accesso", "Account online con 2FA e Windows Hello offline, solo dopo abilitazione manuale. Password dei server e passphrase SSH restano separate.");
  const app = card(access, uiText("Apertura dell’applicazione"));
  const appCheck = check(app, uiText("Richiedi autenticazione all’apertura"), true);
  appCheck.disabled = true;
  notice(app, uiText("Sequenza: Intro → Autenticazione → Accesso autorizzato. La modifica richiede una nuova verifica della tua identità."));
  actions(app, action(uiText("Salva accesso e verifica identità"), async () => {
    update(await call("accessSettings", { requireAppAuthentication: appCheck.checked }));
    toast(uiText("Accesso aggiornato."));
    refreshArchive();
  }, true));
  const archive = card(access, uiText("Archivio chiavi"));
  async function vaultAction(name) {
    const previous = window.vaultPromptActive;
    window.vaultPromptActive = true;
    try {
      update(await call(name));
      refreshArchive();
    } finally {
      window.vaultPromptActive = previous;
    }
  }
  function refreshArchive() {
    archive.replaceChildren();
    const h = document.createElement("h4");
    h.textContent = uiText("Sblocco dell’archivio su questo PC");
    archive.append(h);
    notice(archive, state.vault.unlocked ? "Archivio sbloccato con la tua identità." : "Archivio bloccato. Accedi con il tuo account per aprirlo.");
    notice(archive, state.vault.windowsHello ? "Windows Hello abilitato manualmente per questo account Relay e questo PC." : "Windows Hello disattivato. Senza Hello è necessaria una connessione per accedere con il tuo account e la 2FA.");
    notice(archive, uiText("Account Windows condiviso? Chi può usare il suo PIN o i suoi metodi Hello potrebbe aprire anche Relay. Abilita Hello solo se questo account Windows è personale."));
    if (state.vault.unlocked) actions(archive, action(state.vault.windowsHello ? "Disabilita Windows Hello su questo PC" : "Abilita Windows Hello su questo PC", () => vaultAction(state.vault.windowsHello ? "helloDisable" : "helloEnable")), action(uiText("Blocca archivio"), () => vaultAction("vaultLock")));
    else {
      const buttons = [action(uiText("Accedi con il tuo account"), () => vaultAction("vaultUnlock"), true)];
      if (state.vault.windowsHello) buttons.push(action(uiText("Sblocca con Windows Hello"), () => vaultAction("helloUnlock")));
      actions(archive, ...buttons);
    }
  }
  window.refreshSettingsAccess = refreshArchive;
  refreshArchive();
  const lock = card(access, uiText("Blocco automatico dell’archivio"));
  const lockForm = document.createElement("form");
  field(lockForm, uiText("Blocca archivio dopo minuti"), "autoLockMinutes", state.data.settings.autoLockMinutes, "number");
  const lockInput = lockForm.querySelector("input");
  lockInput.min = 1;
  lockInput.max = 120;
  lockInput.required = true;
  const lockSave = document.createElement("button");
  lockSave.type = "submit";
  lockSave.className = "primary";
  lockSave.textContent = uiText("Salva blocco automatico");
  lockForm.append(lockSave);
  lockForm.onsubmit = async (e) => {
    e.preventDefault();
    lockSave.disabled = true;
    try {
      update(await call("settings", { autoLockMinutes: Number(lockInput.value) }));
      toast(uiText("Blocco automatico salvato."));
    } catch {
    } finally {
      lockSave.disabled = false;
    }
  };
  lock.append(lockForm);
  const chat = section("chatgpt", "ChatGPT", "API locale per client MCP come ChatGPT e Codex. Ogni richiesta richiede il tuo consenso.");
  const api = card(chat, uiText("Autorizzazioni"));
  const apiCheck = check(api, uiText("Abilita API locale per ChatGPT / Codex"), state.data.settings.assistantApiEnabled);
  notice(api, uiText("Ogni richiesta mostra l’azione e i dati condivisi e richiede password o Windows Hello. Nessun permesso permanente e nessuna porta di rete. Disattivando l’API vengono rifiutate anche le richieste in attesa."));
  notice(api, uiText("Il nome ChatGPT non prova l’identità del chiamante. Il client può inviare al servizio AI le risposte che autorizzi. Password, chiavi private e token dell’app non sono esposti."));
  actions(api, action(uiText("Salva API e verifica identità"), async () => {
    update(await call("accessSettings", { assistantApiEnabled: apiCheck.checked }));
    toast(uiText("API aggiornata."));
    refreshArchive();
  }, true), action(uiText("Copia configurazione MCP"), async () => {
    const config = await call("assistantConfig");
    await call("clipboardWrite", { text: JSON.stringify(config, null, 2) });
    toast(uiText("Configurazione MCP copiata."));
  }));
  notice(chat, uiText("Componente incluso: Librerie/ChatGPT/UNSC Relay MCP.exe."));
  const editor = section("editor", "Editor", "Scegli il programma per modificare i file remoti sul tuo PC.");
  const editorCard = card(editor, uiText("Editor esterno predefinito"));
  const editorPath = document.createElement("input");
  editorPath.readOnly = true;
  interfaceAttribute(editorPath, 'aria-label', "Programma editor esterno");
  const editorStatus = document.createElement("p");
  editorStatus.className = "notice";
  function refreshEditor() {
    editorPath.value = state.data.settings.externalEditor || "Blocco note di Windows";
    editorStatus.textContent = state.data.settings.externalEditor ? state.data.settings.editorPreference === "auto" ? "Notepad++ rilevato e assegnato automaticamente." : "Editor scelto manualmente e salvato." : state.data.settings.editorPreference === "notepad" ? "Blocco note selezionato manualmente." : "Notepad++ non rilevato. Per ora viene usato Blocco note; puoi scegliere un programma EXE o ripetere la ricerca.";
  }
  editorCard.append(editorPath, editorStatus);
  refreshEditor();
  actions(editorCard, action(uiText("Scegli programma EXE"), async () => {
    update(await call("editorChooseProgram"));
    refreshEditor();
  }, true), action(uiText("Ripristina Blocco note"), async () => {
    update(await call("editorResetProgram"));
    refreshEditor();
  }));
  notice(editorCard, uiText("Il file remoto viene aperto in una copia locale temporanea. Controllo dei conflitti e caricamento dopo il salvataggio mantengono le opzioni già impostate nell’editor remoto."));
  const recommendation = document.createElement("section");
  recommendation.className = "editor-recommendation";
  interfaceAttribute(recommendation, 'aria-label', "Editor consigliato Notepad++");
  const logo = document.createElement("img");
  logo.src = "brand/notepad-plus-plus.svg";
  logo.alt = "Logo Notepad++";
  logo.width = 118;
  logo.height = 100;
  const recommendedCopy = document.createElement("div");
  const recommendedKicker = document.createElement("small");
  recommendedKicker.textContent = uiText("EDITOR CONSIGLIATO");
  const recommendedTitle = document.createElement("h4");
  recommendedTitle.textContent = uiText("Notepad++");
  const recommendedText = document.createElement("p");
  recommendedText.textContent = uiText("Consigliato per modificare script e file di configurazione remoti. Se installato, Relay lo rileva automaticamente quando non hai già scelto un altro editor.");
  const website = document.createElement("a");
  website.href = "https://notepad-plus-plus.org/";
  website.textContent = uiText("notepad-plus-plus.org ↗");
  website.onclick = (e) => {
    e.preventDefault();
    call("openNotepadWebsite").catch(() => {
    });
  };
  recommendedCopy.append(recommendedKicker, recommendedTitle, recommendedText, website);
  recommendation.append(logo, recommendedCopy);
  editor.append(recommendation);
  actions(recommendedCopy, action(uiText("Rileva e usa Notepad++"), async () => {
    update(await call("editorDetectProgram"));
    refreshEditor();
    toast(state.data.settings.externalEditor ? "Notepad++ impostato come editor." : "Notepad++ non trovato. Scegli l’EXE oppure installalo dal sito ufficiale.", !state.data.settings.externalEditor);
  }));
  const audio = section("audio", "Audio", "Tre volumi indipendenti: scegli il livello di ogni fase. Le sequenze restano non saltabili anche a volume zero.");
  function volumeControl(parent, id, label, value) {
    const l = document.createElement("label");
    l.className = "settings-volume";
    l.htmlFor = id;
    l.textContent = label;
    const input = document.createElement("input");
    input.type = "range";
    input.id = id;
    input.min = 0;
    input.max = 100;
    input.step = 1;
    input.value = Math.round(value * 100);
    const output = document.createElement("output");
    output.htmlFor = id;
    const row = document.createElement("div");
    row.className = "settings-slider";
    row.append(input, output);
    parent.append(l, row);
    const refresh = () => {
      output.value = input.value + "%";
      input.setAttribute("aria-valuetext", input.value + " percento");
      input.style.setProperty("--volume-level", input.value + "%");
    };
    input.addEventListener("input", refresh);
    refresh();
    return input;
  }
  const levels = document.createElement("div");
  levels.className = "audio-sequence-levels";
  audio.append(levels);
  const intro = card(levels, uiText("Intro e accesso"));
  notice(intro, uiText("Un solo livello per la sequenza iniziale, il login e la sua musica di attesa. Si applica dalla prossima apertura."));
  const introRange = volumeControl(intro, "settings-intro-volume", "Volume intro e accesso", sequenceVolume("startup"));
  const outro = card(levels, uiText("Outro"));
  notice(outro, uiText("Il suono della sequenza di chiusura. Si applica alla prossima uscita; gli aggiornamenti continuano a chiudersi senza outro."));
  const outroRange = volumeControl(outro, "settings-outro-volume", "Volume outro", sequenceVolume("shutdown"));
  const music = card(audio, uiText("Musica nell’app"));
  const enabled = check(music, uiText("Riproduci musica quando l’app è aperta"), state.data.settings.appMusicEnabled);
  const range = volumeControl(music, "settings-music-volume", "Volume della musica", appMusicVolume());
  const status = document.createElement("p");
  status.className = "settings-audio-status";
  status.setAttribute("role", "status");
  music.append(status);
  function updateAudioLabel() {
    status.textContent = enabled.checked ? Number(range.value) === 0 ? "Musica attiva, volume azzerato." : "Musica attiva durante l’utilizzo dell’app." : "Musica dell’app disattivata.";
  }
  function previewAudio() {
    preview = true;
    updateAudioLabel();
    if (!enabled.checked) {
      appMusic?.pause();
      return;
    }
    if (!appMusic) appMusic = backgroundTrack();
    appMusic.volume = Number(range.value) / 100;
    appMusic.play().catch(() => toast(uiText("Riproduzione non disponibile."), true));
  }
  enabled.onchange = previewAudio;
  range.oninput = previewAudio;
  updateAudioLabel();
  notice(music, uiText("Solo la musica dell’app si ascolta in anteprima. Salva audio conserva i tre volumi; chiudendo senza salvare ripristini le preferenze precedenti."));
  async function saveAudio(on, onlyMusic = false) {
    const values = { enabled: on, volume: Number(range.value) / 100 };
    if (!onlyMusic) Object.assign(values, { startupVolume: Number(introRange.value) / 100, shutdownVolume: Number(outroRange.value) / 100 });
    update(await call("audioSettings", values));
    enabled.checked = on;
    preview = false;
    startAppMusic();
    updateAudioLabel();
    toast(uiText("Preferenze audio salvate."));
  }
  actions(music, action(uiText("Salva audio"), () => saveAudio(enabled.checked), true), action(uiText("Disattiva musica nell’app"), () => saveAudio(false, true)));
  notice(audio, uiText("Il tasto Musica nella barra superiore controlla soltanto la musica dell’app. Non modifica i livelli di intro e outro e non elimina le tracce."));
  const disclaimer = card(audio, uiText("Musica e diritti"));
  notice(disclaimer, uiText("Musica ed effetti audio tratti da Halo: tutti i diritti appartengono ai rispettivi titolari. UNSC Relay è un progetto personale e non ufficiale, non affiliato né approvato da Microsoft o Halo Studios. La presenza delle tracce nell’app non concede diritti di riutilizzo o distribuzione."));
  const features = section("features", "Funzionalità", "Esplora le funzioni per area. Lo stato indica ciò che è disponibile nella versione installata.");
  const groups = [
  [
    "connections",
    "Connessioni",
    [
      1,
      3,
      9,
      13,
      14,
      15,
      23,
      24,
      29,
      58,
      59
    ]
  ],
  [
    "files",
    "File e trasferimenti",
    [
      16,
      17,
      18,
      27,
      47,
      55,
      56,
      57
    ]
  ],
  [
    "security",
    "Sicurezza",
    [
      2,
      10,
      11,
      12,
      19,
      20,
      21,
      22,
      26,
      36,
      38,
      39,
      41,
      48,
      51
    ]
  ],
  [
    "accounts",
    "Account e archivi",
    [
      31,
      32,
      33,
      34,
      35,
      37,
      40,
      42,
      43,
      44,
      50,
      52,
      53
    ]
  ],
  [
    "automation",
    "Automazioni",
    [
      4,
      5,
      6,
      25,
      28,
      54
    ]
  ],
  [
    "application",
    "Applicazione",
    [
      0,
      7,
      8,
      30,
      45,
      46,
      49
    ]
  ]
];
  const featureNav = document.createElement("div");
  featureNav.className = "feature-tabs";
  featureNav.setAttribute("role", "tablist");
  interfaceAttribute(featureNav, 'aria-label', "Aree delle funzionalità");
  notice(features, uiText("Le funzioni online richiedono un servizio aggiornato e i relativi permessi."));
  features.append(featureNav);
  const featureTabs = [];
  function chooseFeature(id, focus = false) {
    for (const item of featureTabs) {
      const active = item.id === id;
      item.tab.setAttribute("aria-selected", String(active));
      item.tab.tabIndex = active ? 0 : -1;
      item.panel.hidden = !active;
      if (active && focus) item.tab.focus();
    }
  }
  for (const [id, title, indices] of groups) {
    const tab = localizedButton(title, () => chooseFeature(id));
    tab.id = "feature-tab-" + id;
    tab.setAttribute("role", "tab");
    tab.setAttribute("aria-controls", "feature-panel-" + id);
    const panel = document.createElement("section");
    panel.id = "feature-panel-" + id;
    panel.className = "feature-panel";
    panel.setAttribute("role", "tabpanel");
    panel.setAttribute("aria-labelledby", tab.id);
    const heading = document.createElement("h4");
    interfaceLabel(heading, title);
    const summary = document.createElement("p");
    summary.className = "muted";
    const countLabel = interfaceLabel(document.createElement("span"), "Funzioni in questa area");
    summary.append(countLabel, ": " + indices.length.toLocaleString(document.documentElement.lang));
    panel.append(heading, summary);
    for (const index of indices) {
      const [name, ready] = capabilities[index];
      const row = document.createElement("div");
      row.className = "capability feature-capability" + (ready ? "" : " pending");
      const label = document.createElement("span"), status2 = document.createElement("strong");
      interfaceLabel(label, name);
      interfaceLabel(status2, ready ? "Disponibile" : "Non disponibile in questa versione");
      status2.className = "feature-status";
      row.append(label, status2);
      panel.append(row);
    }
    featureNav.append(tab);
    features.append(panel);
    featureTabs.push({ id, tab, panel });
  }
  featureNav.onkeydown = (e) => {
    const index = featureTabs.findIndex((t) => t.tab === document.activeElement);
    if (index < 0) return;
    let next = index;
    if (e.key === "ArrowRight") next = (index + 1) % featureTabs.length;
    else if (e.key === "ArrowLeft") next = (index + featureTabs.length - 1) % featureTabs.length;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = featureTabs.length - 1;
    else return;
    e.preventDefault();
    e.stopPropagation();
    chooseFeature(featureTabs[next].id, true);
  };
  chooseFeature(groups[0][0]);
  select(tabs.some((t) => t.id === initial) ? initial : "general");
}
async function diagnosticDialog() {
  const report = await call("diagnosticPreview");
  const body = showModal(uiText("Diagnostica"), "ANTEPRIMA / SENZA DATI PERSONALI", null, true);
  notice(body, uiText("Controlla il rapporto prima di esportarlo. Se serve assistenza, puoi allegare il file al ticket dal sito: l’invio richiede una conferma separata."));
  const preview = document.createElement("textarea");
  preview.readOnly = true;
  preview.rows = 18;
  preview.value = JSON.stringify(report, null, 2);
  interfaceAttribute(preview, 'aria-label', "Anteprima rapporto diagnostico");
  body.append(preview, localizedButton("Esporta rapporto", () => call("diagnosticExport"), "primary"));
}
