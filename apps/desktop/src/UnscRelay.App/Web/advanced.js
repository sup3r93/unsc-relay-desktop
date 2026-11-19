"use strict";
function advancedCheck(parent, text, checked = false) {
  const label = document.createElement("label");
  label.className = "check-label";
  const box = document.createElement("input");
  box.type = "checkbox";
  box.checked = checked;
  label.append(box, text);
  parent.append(label);
  return box;
}
function renderRdpOptions(parent, current) {
  const details = document.createElement("details");
  details.className = "algorithm-criteria";
  const summary = document.createElement("summary");
  summary.textContent = uiText("Opzioni RDP avanzate / Gateway e dispositivi");
  details.append(summary);
  const grid = document.createElement("div");
  grid.className = "form-grid";
  details.append(grid);
  parent.append(details);
  const gateway = field(grid, uiText("RD Gateway / nome DNS"), "rdpGateway", current.gateway || "");
  const user = field(grid, uiText("Utente Gateway (vuoto: richiesta Windows)"), "rdpGatewayUser", current.gatewayUsername || "");
  const domain = field(grid, uiText("Dominio Gateway"), "rdpGatewayDomain", current.gatewayDomain || "");
  const audio = field(grid, uiText("Audio remoto"), "rdpAudio", String(current.audioMode ?? 2), "text", [["2", "Disattivato"], ["0", "Riproduci sul PC"], ["1", "Riproduci sul server"]]);
  const multi = advancedCheck(details, "Usa tutti i monitor (RDP incorporato, avvio a schermo intero)", current.multiMonitor);
  const drives = advancedCheck(details, "Condividi unita locali con il server", current.redirectDrives);
  const printers = advancedCheck(details, "Condividi stampanti", current.redirectPrinters);
  const cards = advancedCheck(details, "Condividi smart card", current.redirectSmartCards);
  const ports = advancedCheck(details, "Condividi porte COM", current.redirectPorts);
  notice(details, uiText("RD Gateway e jump host SSH sono alternativi. Il Gateway viene sempre usato quando configurato, senza ripiego diretto. Il multimonitor usa il controllo Microsoft incorporato nella sessione Relay, con certificati e credenziali gestiti come nelle altre schede RDP. Dopo il login passa a schermo intero; Ctrl+Alt+Interr permette di tornare alla scheda. Nessuna password viene scritta in file RDP."));
  return () => ({ gateway: gateway.value.trim(), gatewayUsername: user.value, gatewayDomain: domain.value, multiMonitor: multi.checked, redirectDrives: drives.checked, redirectPrinters: printers.checked, redirectSmartCards: cards.checked, redirectPorts: ports.checked, audioMode: Number(audio.value) });
}
function nativeProfileDialog(existing, selection = {}, draft = {}) {
  const p = existing || { id: crypto.randomUUID().replaceAll("-", ""), protocol: "native", name: "", folder: "Connessioni", host: "", port: 22, username: "", authentication: "auto", keyId: "", passwordId: "", jumpHost: "", hooks: [], tunnels: [], ...draft };
  const n = p.native || { kind: selection.kind || "ssh" };
  const body = showModal(existing ? "Modifica client nativo" : "Nuova sessione / client nativo", "CLIENT ORIGINALI / SCHEDE E WORKSPACE", null, true);
  modal.classList.add("native-profile-modal");
  const form = document.createElement("form");
  form.className = "form-grid";
  body.append(form);
  const name = field(form, uiText("Nome"), "name", p.name);
  name.required = true;
  const folder = field(form, uiText("Cartella"), "folder", p.folder, "text", folderChoices(p.folder));
  const kind = field(form, uiText("Client"), "kind", n.kind, "text", [["ssh", "OpenSSH / config completo"], ["sftp", "SFTP OpenSSH / console"], ["mosh", "Mosh"], ["telnet", "Telnet"], ["serial", "Seriale / PuTTY Plink"], ["ftp", "FTP / client incluso"], ["s3", "S3 / Rclone"], ["rsh", "Rsh"], ["xdmcp", "XDMCP / X server"], ["bash", "Bash inclusa"], ["zsh", "Zsh inclusa"], ["browser", "Browser / scheda isolata"], ["file", "File locali"]]);
  const target = field(form, uiText("Host, alias, URL o cartella"), "target", n.target || "");
  const exe = field(form, uiText("Eseguibile (vuoto: componente incluso)"), "executable", n.executable || "");
  form.append(localizedButton("Scegli eseguibile", async () => {
    const path = await call("nativeSelectFile", { title: "Seleziona il client originale (.exe)" });
    if (path) exe.value = path;
  }));
  const authentication = field(form, uiText("Autenticazione"), "nativeAuthentication", p.authentication || "auto", "text", [["auto", "File config / identità esterna"], ["key", "Chiave dell’archivio"], ["password", "Password"]]);
  const vaultKey = field(form, uiText("Chiave dell’archivio"), "nativeKeyId", p.keyId || "", "text", [["", "Seleziona una chiave"], ...(state.vault.keys || []).map((k) => [k.id, k.name + " / " + k.algorithm + (k.certificate ? " / certificato SSH" : "")])]);
  const keyPhrase = field(form, uiText("Passphrase della chiave (vuota se assente)"), "keyPassphrase", "", "password");
  const password = field(form, uiText("Password"), "password", "", "password");
  const remember = advancedCheck(form, "Salva password nell’archivio cifrato", !!p.passwordId);
  const shared = advancedCheck(form, "Usa Host Key Manager condiviso", n.sharedHostKeys !== false);
  const authRefresh = () => {
    vaultKey.parentElement.hidden = keyPhrase.parentElement.hidden = authentication.value !== "key";
    password.parentElement.hidden = remember.parentElement.hidden = authentication.value !== "password";
  };
  authentication.onchange = () => {
    authRefresh();
    identity.disabled = authentication.value === "key";
  };
  authRefresh();
  const nativePort = field(form, uiText("Porta SSH (0: usa config o 22)"), "nativePort", n.port || 0, "number");
  nativePort.min = 0;
  nativePort.max = 65535;
  const config = field(form, uiText("File config OpenSSH (opzionale)"), "config", n.configFile || "");
  form.append(localizedButton("Scegli config OpenSSH", async () => {
    const path = await call("nativeSelectFile", { title: "Seleziona il config OpenSSH" });
    if (path) config.value = path;
  }));
  const identity = field(form, uiText("Identita OpenSSH / hardware (opzionale)"), "identity", n.identityFile || "");
  form.append(localizedButton("Scegli identita", async () => {
    const path = await call("nativeSelectFile", { title: "Seleziona identita OpenSSH (il contenuto resta al client)" });
    if (path) identity.value = path;
  }));
  identity.disabled = authentication.value === "key";
  const signature = field(form, uiText("Firma richiesta / nessun fallback"), "signature", n.signature || "default", "text", [["default", "Algoritmi del client / config"], ["ssh-mldsa44-ed25519", "ML-DSA-44 + Ed25519 (client compatibile necessario)"], ["ssh-ed448", "Ed448 (solo se offerto dal client)"]]);
  const serial = field(form, uiText("Porta seriale"), "serialPort", n.serialPort || "COM1");
  const baud = field(form, uiText("Baud"), "baud", n.baud || 115200, "number");
  const args = field(form, uiText("Argomenti del client / elenco JSON"), "args", n.argumentsJson || "[]", "textarea");
  args.parentElement.classList.add("wide");
  const cwd = field(form, uiText("Cartella iniziale del client (opzionale)"), "cwd", p.startupDirectory || "");
  const pq = advancedCheck(body, "Richiedi scambio chiavi post-quantistico (SSH/SFTP)", !!p.requirePostQuantumKex);
  const remoteRoot = field(form, uiText("Cartella iniziale del browser SFTP"), "nativeRemoteDirectory", p.remoteDirectory || ".");
  const subsystem = field(form, uiText("Comando server SFTP personalizzato (opzionale)"), "nativeSubsystem", p.subsystem || "");
  const macroSection = document.createElement("section");
  macroSection.className = "wide native-macro-section";
  body.append(macroSection);
  const macroHeading = document.createElement("h3");
  macroHeading.textContent = uiText("Macro di avvio");
  macroSection.append(macroHeading);
  const startupMacro = renderStartupMacroEditor(macroSection, p);
  const hookSection = document.createElement("section");
  hookSection.className = "wide native-hook-section";
  body.append(hookSection);
  const hookTitle = document.createElement("h3");
  hookTitle.textContent = uiText("Automazione SSH");
  hookSection.append(hookTitle);
  notice(hookSection, uiText("Hook locali e remoti per OpenSSH incluso: dopo connessione parte solo ad autenticazione riuscita. Nessun hook durante gli aggiornamenti; un accesso fallito non avvia gli hook di disconnessione. Gli hook remoti usano un canale della stessa connessione autenticata, senza scrivere nel terminale. Non inserire segreti nei comandi."));
  const nativeHooks = renderHookEditor(hookSection, p.hooks || []);
  const availability = document.createElement("p");
  availability.className = "notice";
  body.append(availability);
  const help = document.createElement("div");
  help.className = "notice";
  body.append(help);
  const infos = { browser: "Pagina web nella scheda, isolata dal bridge e dall’archivio di Relay. Solo URL HTTP/HTTPS; nessuna password salvata nel profilo.", file: "Esplorazione di cartelle locali nella scheda. I file non vengono eseguiti automaticamente.", ssh: "Terminale OpenSSH nella scheda. Include, Match, ProxyCommand, agent e certificati sono valutati dal client originale. Usa file fidati: possono eseguire comandi locali. Le chiavi host usano il gestore Relay quando la relativa opzione è attiva. Sono supportate anche le identità ibride importate nell’archivio e i certificati utente validi associati alle chiavi software. Con un certificato, username, scadenza e revoca vengono controllati; nessun ripiego alla sola chiave.", sftp: "Console SFTP OpenSSH in una scheda. Usa il config completo; i comandi put/get sono quelli del client. Il pulsante SFTP apre anche un browser grafico sullo stesso canale autenticato. Per coda avanzata, ripresa e editor remoto usa per ora il tipo SFTP integrato.", serial: "Usa PuTTY Plink incluso e la porta COM scelta. Nessun driver viene installato.", s3: 'Usa Rclone incluso. Esegui una sessione con ["config"] per configurare un endpoint S3, poi usa ["ls","nome:bucket"]. Configurazione separata nei dati personali Relay; usa la cifratura config di Rclone. Non inserire access key o token nel profilo Relay.', ftp: "Client FTP interattivo incluso. Inserisci host e autenticazione nella console. FTP non cifra il traffico.", mosh: "Mosh e incluso sul PC; deve essere presente anche sul server, con porte UDP raggiungibili.", telnet: "Usa il client Telnet incluso. Il traffico non e cifrato.", rsh: "Usa il client Rsh incluso. Il traffico non e cifrato.", xdmcp: 'Usa XWin incluso. Indica l’host nel campo destinazione; Relay aggiunge -query. Argomenti opzionali: [":1","-once"]. L’X server usa una finestra dedicata. La sessione remota deve consentire XDMCP.', bash: "Bash inclusa, interattiva nella scheda.", zsh: "Zsh inclusa, interattiva nella scheda." };
  function refresh() {
    hookSection.hidden = !["ssh", "sftp"].includes(kind.value);
    remoteRoot.parentElement.hidden = subsystem.parentElement.hidden = !["ssh", "sftp"].includes(kind.value);
    macroSection.hidden = kind.value !== "ssh";
    const ssh = ["ssh", "sftp"].includes(kind.value);
    authentication.parentElement.hidden = shared.parentElement.hidden = !ssh;
    if (!ssh) authentication.value = "auto";
    authRefresh();
    if (!ssh) {
      vaultKey.parentElement.hidden = keyPhrase.parentElement.hidden = password.parentElement.hidden = remember.parentElement.hidden = true;
    }
    for (const input of [config, identity, signature, nativePort]) input.parentElement.hidden = !ssh;
    serial.parentElement.hidden = baud.parentElement.hidden = kind.value !== "serial";
    args.parentElement.hidden = ssh || ["serial", "browser", "file"].includes(kind.value);
    target.parentElement.hidden = ["serial", "bash", "zsh"].includes(kind.value);
    target.required = ["ssh", "sftp", "mosh", "telnet", "rsh"].includes(kind.value);
    pq.parentElement.hidden = !ssh;
    help.textContent = infos[kind.value];
    availability.textContent = uiText("Il componente incluso viene usato automaticamente. Puoi scegliere una copia alternativa se necessario.");
  }
  kind.onchange = refresh;
  refresh();
  body.append(localizedButton("Verifica client e algoritmi SSH", async () => {
    const result = await call("nativeTools", { ssh: kind.value === "ssh" ? exe.value : "" });
    availability.textContent = result.tools.map((t) => t.kind + ": " + (t.available ? "disponibile" : "non rilevato")).join(" · ") + "\nFirme del client SSH: " + result.keys.join(", ");
  }));
  const actions = document.createElement("div");
  actions.className = "form-actions";
  const save = localizedButton("Salva sessione", async () => {
    if (!form.reportValidity()) return;
    save.disabled = true;
    try {
      update(await call("saveProfile", { ...p, name: name.value, folder: folder.value, protocol: "native", startupMacro: kind.value === "ssh" ? startupMacro() : null, authentication: authentication.value, keyId: authentication.value === "key" ? vaultKey.value : "", password: password.value, keyPassphrase: keyPhrase.value, rememberPassword: authentication.value === "password" && remember.checked, host: "", username: "", jumpHost: "", hooks: ["ssh", "sftp"].includes(kind.value) ? nativeHooks() : [], tunnels: [], startupDirectory: cwd.value, remoteDirectory: remoteRoot.value || ".", subsystem: subsystem.value, requirePostQuantumKex: pq.checked && ["ssh", "sftp"].includes(kind.value), native: { port: Number(nativePort.value), sharedHostKeys: shared.checked, kind: kind.value, target: target.parentElement.hidden ? "" : target.value, executable: exe.value, configFile: config.value, identityFile: identity.value, signature: signature.value, serialPort: serial.value, baud: Number(baud.value), argumentsJson: args.parentElement.hidden ? "[]" : args.value } }));
      modal.close();
      toast(uiText("Sessione nativa salvata."));
    } finally {
      save.disabled = false;
    }
  }, "primary");
  actions.append(localizedButton("Annulla", () => modal.close()), save);
  if (existing) actions.prepend(localizedButton("Elimina profilo", async () => {
    update(await call("deleteProfile", { profileId: p.id }));
    modal.close();
  }, "danger"));
  body.append(actions);
  const scrolling = document.createElement("div");
  scrolling.className = "native-form-scroll";
  for (const child of [...body.children]) if (child !== actions) scrolling.append(child);
  body.prepend(scrolling);
}
function hardwareGenerationDialog() {
  const body = showModal(uiText("Generazione hardware e OpenSSH"), "CLIENT NATIVO / NESSUNA CHIAVE SIMULATA", keysDialog, true);
  const form = document.createElement("form");
  body.append(form);
  const name = field(form, uiText("Commento della chiave"), "hardwareName", "UNSC Relay");
  const algorithm = document.createElement("fieldset");
  algorithm.className = "algorithm-selection";
  const legend = document.createElement("legend");
  legend.textContent = uiText("Algoritmo / hardware e firma ibrida");
  algorithm.append(legend);
  const grid = document.createElement("div");
  grid.className = "algorithm-grid";
  algorithm.append(grid);
  form.append(algorithm);
  for (const choice of [
    { id: "ed25519-sk", name: "Ed25519-SK / FIDO2", text: "Firma Ed25519 custodita nel token. Circa 128 bit di sicurezza classica; protegge dalla copia del file privato. Richiede un token e un provider compatibili.", hardware: true },
    { id: "ecdsa-sk", name: "ECDSA-SK / FIDO2", text: "Firma NIST P-256 sul token. Circa 128 bit di sicurezza classica. PIN, tocco e verifica utente dipendono dal dispositivo e dalla configurazione.", hardware: true },
    { id: "mldsa44-ed25519", name: "ML-DSA-44 + Ed25519", text: "Firma ibrida classica e post-quantistica. Richiede supporto del server; la chiave privata viene salvata su disco e non è una credenziale hardware.", hardware: false }
  ]) {
    const card = document.createElement("label");
    card.className = "algorithm-card";
    const top = document.createElement("span");
    top.className = "algorithm-card-top";
    const radio = document.createElement("input");
    radio.type = "radio";
    radio.name = "hardwareAlgorithm";
    radio.value = choice.id;
    radio.checked = choice.id === "ed25519-sk";
    radio.required = true;
    radio.setAttribute("aria-label", choice.name);
    const title = document.createElement("strong");
    title.textContent = choice.name;
    top.append(radio, title);
    const copy = document.createElement("span");
    copy.className = "algorithm-copy";
    copy.textContent = choice.text;
    card.append(top, copy);
    grid.append(card);
    radio.onchange = refreshHardware;
  }
  function refreshHardware() {
    const hardware = form.elements.hardwareAlgorithm.value !== "mldsa44-ed25519";
    resident.disabled = !hardware;
    if (!hardware) resident.checked = false;
  }
  const exe = field(form, uiText("ssh-keygen.exe (vuoto: OpenSSH incluso)"), "hardwareExe", "");
  form.append(localizedButton("Scegli ssh-keygen", async () => {
    const path = await call("nativeSelectFile", { title: "Seleziona ssh-keygen del tuo client OpenSSH" });
    if (path) exe.value = path;
  }));
  const resident = advancedCheck(form, "Credenziale residente sul token (solo -sk)");
  notice(body, uiText("Si apre una scheda interattiva per PIN, tocco e passphrase. I file vengono salvati nel percorso scelto: per -sk contengono un riferimento al token; la chiave ibrida e invece una chiave privata su disco. Dopo la generazione puoi importare la chiave ibrida nell’archivio con Importa OpenSSH nativo / ibrido e usarla nelle sessioni OpenSSH native. La disponibilita dipende dal client, dal provider FIDO e dal dispositivo."));
  const run = document.createElement("button");
  run.className = "primary";
  run.textContent = uiText("Genera con OpenSSH");
  form.append(run);
  form.onsubmit = async (e) => {
    e.preventDefault();
    run.disabled = true;
    try {
      const profile = await call("nativeTask", { kind: "generate", executable: exe.value, algorithm: form.elements.hardwareAlgorithm.value, name: name.value, resident: resident.checked });
      if (profile) {
        modal.close();
        await connect(profile);
      }
    } finally {
      run.disabled = false;
    }
  };
  body.append(localizedButton("Carica una identita in OpenSSH agent", async () => {
    const profile = await call("nativeTask", { kind: "load" });
    if (profile) {
      modal.close();
      await connect(profile);
    }
  }));
}
async function externalCertificateDialog(key) {
  const providers = await call("externalCertificateStatus");
  const old = providers.find((c) => c.keyId === key.id) || {};
  const body = showModal(uiText("CA esterna / ") + key.name, "CERTIFICATI / PROVIDER E RINNOVO", () => certificateDialog(key), true);
  const form = document.createElement("form");
  form.className = "form-grid";
  body.append(form);
  const exe = field(form, uiText("Eseguibile del provider CA (vuoto: Step incluso)"), "providerExe", old.executable || "");
  form.append(localizedButton("Scegli provider", async () => {
    const path = await call("nativeSelectFile", { title: "Seleziona il client della CA (step, wrapper aziendale, ssh-keygen per HSM)" });
    if (path) exe.value = path;
  }));
  const fingerprint = field(form, uiText("Impronta SHA256 della CA fidata"), "authorityFingerprint", old.authorityFingerprint || "");
  const principal = field(form, uiText("Principal / username"), "principal", old.principal || state.data.profiles.find((p) => p.keyId === key.id)?.username || "");
  const hours = field(form, uiText("Durata massima (ore)"), "hours", old.hours || 1, "number");
  hours.min = 1;
  hours.max = 8;
  const args = field(form, uiText("Argomenti del provider / elenco JSON"), "providerArgs", old.argumentsJson || '["ssh","certificate","{principal}","{publicKey}","--sign","--principal","{principal}","--not-after","{hours}h","--no-agent"]', "textarea");
  args.parentElement.classList.add("wide");
  const help = document.createElement("p");
  help.className = "hint";
  help.textContent = uiText("Step incluso richiede la configurazione della tua CA: aggiungi --ca-url, --root e le opzioni del provisioner. Usa file protetti per i segreti, non inserirli negli argomenti. Il provider riceve solo la chiave pubblica.");
  body.append(help);
  const auto = advancedCheck(body, "Rinnova automaticamente vicino alla scadenza, con app aperta e archivio sbloccato", !!old.autoRenew);
  notice(body, uiText("Il provider riceve solo il file pubblico. Deve scrivere il certificato in {certificate}. Segnaposto: {publicKey}, {certificate}, {principal}, {hours}. Login al provider, politiche CA e accesso HSM restano gestiti dal suo client. Non inserire token o password negli argomenti."));
  notice(body, uiText('Per una CA in agent: ssh-keygen con ["-U","-s","C:\\percorso\\ca.pub","-I","relay","-n","{principal}","-V","+{hours}h","{publicKey}"]. Per HSM PKCS#11 aggiungi ["-D","C:\\percorso\\modulo.dll"] secondo il tuo provider. ssh-keygen produce identity-cert.pub accanto a identity.pub.'));
  notice(body, uiText("Sono verificati firma CA, impronta fidata, durata, principal e corrispondenza con la chiave. Nessun fallback su un certificato rifiutato. Rinnovo automatico non eseguito per certificati revocati; una chiave protetta da passphrase richiede il rinnovo manuale."));
  const save = localizedButton("Salva provider", async () => {
    update(await call("externalCertificateSave", { keyId: key.id, provider: { keyId: key.id, executable: exe.value, argumentsJson: args.value, authorityFingerprint: fingerprint.value.trim(), principal: principal.value, hours: Number(hours.value), autoRenew: auto.checked } }));
    toast(uiText("Provider salvato."));
  }, "primary");
  body.append(save, localizedButton("Richiedi / rinnova ora", async () => {
    update(await call("externalCertificateIssue", { keyId: key.id }));
    keysDialog();
  }), localizedButton("Rimuovi provider", async () => {
    update(await call("externalCertificateSave", { keyId: key.id, remove: true }));
    keysDialog();
  }, "danger"));
}
async function connectLocalFiles(profile) {
  const pane = await connectDesktopPane(profile, true);
  pane.desktop = false;
  pane.localFiles = true;
  pane.connected = true;
  pane.status.textContent = uiText("File locali");
  pane.surface.replaceChildren();
  const toolbar = document.createElement("form");
  toolbar.className = "toolbar";
  const path = document.createElement("input");
  interfaceAttribute(path, 'aria-label', "Cartella locale");
  path.style.flex = "1";
  const list = document.createElement("div");
  list.className = "workspace-file-list";
  pane.surface.style.overflow = "auto";
  const history = [];
  let parent = null, home = null;
  async function load(value, push = true) {
    const data = await call("localList", { path: value });
    if (push && path.value) history.push(path.value);
    path.value = data.path;
    parent = data.parent;
    home = data.home;
    list.replaceChildren();
    for (const file of data.entries) {
      const row = button((file.directory ? "▱ " : "▤ ") + file.name, async () => {
        if (file.directory && !file.link) await load(file.path);
        else {
          await call("clipboardWrite", { text: file.path });
          toast(uiText("Percorso copiato; nessun file eseguito."));
        }
      });
      row.className = "workspace-file-row";
      row.style.width = "100%";
      row.style.textAlign = "left";
      row.title = file.path;
      list.append(row);
    }
  }
  toolbar.append(localizedButton("← Indietro", () => {
    const value = history.pop();
    if (value) return load(value, false);
  }), localizedButton("↑ Superiore", () => {
    if (parent) return load(parent);
  }), localizedButton("⌂ Home", () => load(home || "")), path, localizedButton("Apri", () => load(path.value)), localizedButton("Scegli cartella", async () => {
    const value = await call("localChoose");
    if (value) await load(value.path);
  }));
  toolbar.onsubmit = (e) => {
    e.preventDefault();
    load(path.value);
  };
  pane.surface.append(toolbar, list);
  await load(profile.native.target || "");
  return pane;
}
const tunnelObserved = /* @__PURE__ */ new Map();
let tunnelMonitorBusy = false;
const tunnelMonitor = setInterval(async () => {
  if (tunnelMonitorBusy || !native || !state || state.appLocked || state.account?.requiresLink || document.hidden) return;
  tunnelMonitorBusy = true;
  try {
    for (const pane of panes.values()) {
      if (!pane.connected || pane.local || pane.fileWorkspace || !pane.profile.tunnels?.length) continue;
      const rows = await rpc("tunnelStatus", { sessionId: pane.id });
      for (const row of rows) {
        const key = pane.id + "/" + row.rule.id;
        const before = tunnelObserved.get(key);
        tunnelObserved.set(key, row.active);
        if (before === true && !row.active) toast(uiText("Tunnel interrotto: ") + pane.profile.name + " · porta " + row.rule.localPort, true);
      }
    }
  } catch {
  } finally {
    tunnelMonitorBusy = false;
  }
}, 1e4);
window.addEventListener("unload", () => clearInterval(tunnelMonitor), { once: true });
