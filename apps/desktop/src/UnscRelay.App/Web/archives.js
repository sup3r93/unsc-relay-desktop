"use strict";
async function archivesDialog(initialArchiveId = null) {
  const body = showModal(uiText("Archivi collegati"), "ACCOUNT / CONDIVISIONE CIFRATA", null, true);
  modal.classList.add("archives-modal");
  const intro = document.createElement("p");
  intro.className = "notice";
  intro.textContent = uiText("Gli archivi restano separati. Il titolare gestisce gli accessi; gli altri utenti possono avere sola lettura o lettura e scrittura. Le chiavi del dispositivo restano nel deposito cifrato locale.");
  body.append(intro);
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  status.setAttribute("aria-live", "polite");
  const toolbar = document.createElement("div");
  toolbar.className = "settings-actions";
  const content = document.createElement("div");
  body.append(toolbar, status, content);
  let selected = null, busy = false;
  const run = (label, fn, primary = false) => button(label, async () => {
    if (busy) return;
    busy = true;
    status.textContent = uiText("Operazione in corso…");
    try {
      await fn();
    } catch (e) {
      status.textContent = e.message || "Operazione non riuscita.";
    } finally {
      busy = false;
    }
  }, primary ? "primary" : "");
  const api = (action, values = {}) => call(action, selected ? { archiveId: selected.id, localOnly: action === "archivePublish" ? false : !!selected.offline, ...values } : values);
  function input(parent, label, value = "", type = "text") {
    const l = document.createElement("label");
    l.className = "archive-field";
    l.textContent = label;
    const i = document.createElement("input");
    i.type = type;
    i.value = value;
    l.append(i);
    parent.append(l);
    return i;
  }
  async function list(localOnly = false) {
    const archives = await call(localOnly ? "archiveLocalList" : "archiveList");
    content.replaceChildren();
    toolbar.replaceChildren(run(uiText("Richiedi accesso a un archivio"), () => {
      const owner = state.account.owner;
      requestArchiveAccessDialog({ text: uiText, valid: () => content.isConnected && modal.open && state.account?.owner === owner && !state.appLocked, submit: async (archiveId) => {
        await rpc("archiveRequestAccess", { archiveId });
      } });
    }), run(uiText("Prepara questo dispositivo"), async () => {
      await call("archivePrepare");
      status.textContent = uiText("Dispositivo preparato. Per gli archivi condivisi chiedi al titolare di autorizzarlo.");
    }), run(uiText("Nuovo archivio"), create, true), run(uiText("Copie locali offline"), () => list(true)), run(uiText("Aggiorna elenco"), () => list(false)));
    status.textContent = uiText("Scegli un archivio. La console principale conserva i dati del suo archivio personale.");
    for (const archive of archives) {
      const card = document.createElement("section");
      card.className = "settings-card";
      const heading = document.createElement("h3");
      heading.textContent = archive.name;
      const note = document.createElement("p");
      note.textContent = (archive.access === "owner" ? "Titolare" : archive.access === "read" ? "Sola lettura" : "Lettura e scrittura") + (archive.ownerName ? " · " + archive.ownerName : "") + (archive.offline ? " · Copia locale offline" : "");
      if (archive.accessReviewDue) {
        const reminder = document.createElement("p");
        reminder.textContent = uiText("Revisione accessi necessaria");
        card.append(reminder);
      }
      card.append(heading, note, run(uiText("Apri archivio"), async () => {
        selected = archive;
        await open();
      }), run(uiText("Accessi e proprietà"), async () => {
        selected = archive;
        await members();
      }), run(uiText("Verifica integrità archivio"), () => archiveIntegrityDialog(archive)));
      if (archive.access === "owner" && !archive.offline) card.append(run(uiText("Richieste di accesso"), () => {
        const owner = state.account.owner;
        reviewArchiveAccessRequestsDialog({ id: archive.id, name: archive.name, personal: !!archive.personal, text: uiText, valid: () => content.isConnected && modal.open && state.account?.owner === owner && !state.appLocked, load: () => rpc("archiveAccessRequests", { archiveId: archive.id }), approve: async (request, access, expiresAt) => {
          const result = await rpc("archiveRequestApprove", { archiveId: archive.id, requestId: request.id, access, expiresAt });
          return result.cancelled ? false : result.ready ? true : "Accesso aggiunto. Autorizza i dispositivi dal pannello degli accessi.";
        }, decline: async (request) => !(await rpc("archiveRequestDismiss", { archiveId: archive.id, requestId: request.id })).cancelled });
      }));
      if (archive.access === "owner" && !archive.offline) card.append(run(uiText("Esporta tutti i contenuti in chiaro"), () => plainArchiveExportDialog(archive)));
      content.append(card);
    }
    return archives;
  }
  async function open() {
    const value = await api("archiveRead");
    content.replaceChildren();
    toolbar.replaceChildren(run(uiText("Tutti gli archivi"), () => list(false)), run(uiText("Aggiorna archivio"), open), run(uiText("Scarica copia cifrata"), async () => {
      await api("archiveExport");
      status.textContent = uiText("Copia cifrata conservata.");
    }), run(uiText("Autorizza altri miei dispositivi"), async () => {
      await api("archiveAuthorize", { account: state.account.owner });
      status.textContent = uiText("Dispositivi autorizzati.");
    }));
    status.textContent = value.archive.name + " · revisione " + value.revision + " · " + (value.archive.access === "read" ? "Sola lettura" : "Modifiche consentite");
    if (value.archive.access === "owner" && !value.offline && !value.pending) toolbar.append(run(uiText("Esporta tutti i contenuti in chiaro"), () => plainArchiveExportDialog(value.archive)));
    if (value.pending) toolbar.append(run(uiText("Invia modifiche offline"), async () => {
      await api("archivePublish");
      await open();
    }, true));
    if (value.offline || value.pending) status.textContent += " · " + (value.offline ? "Copia locale offline. " : "") + (value.pending ? "Modifiche da inviare al server." : "");
    for (const record of value.records.filter((r) => ["ssh-key", "ssh-native-key", "password"].includes(r.kind))) {
      const card = document.createElement("section");
      card.className = "settings-card";
      const heading = document.createElement("h3");
      heading.textContent = record.name;
      card.append(heading);
      const name = input(card, "Nome", record.name), notes = input(card, "Note", record.comment || "");
      name.maxLength = 100;
      notes.maxLength = 1e3;
      name.readOnly = notes.readOnly = value.archive.access === "read";
      const actions = document.createElement("div");
      actions.className = "settings-actions";
      actions.append(run(record.kind === "password" ? "Copia password" : "Copia contenuto privato", async () => {
        await api("archiveCopySecret", { recordId: record.id });
        status.textContent = uiText("Copiato negli appunti. Evita di incollarlo in applicazioni non fidate.");
      }));
      actions.append(run(uiText("Discussione"), () => {
        const archiveId = value.archive.id, owner = state.account.owner;
        let revision = value.revision;
        itemDiscussionDialog({ name: record.name, valid: () => content.isConnected && modal.open && selected?.id === archiveId && state.account?.owner === owner && !!state.vault?.unlocked && !state.appLocked, text: uiText, read: async () => {
          const current = await rpc("archiveRead", { archiveId, localOnly: !!value.offline });
          const item = current.records.find((item2) => item2.id === record.id);
          if (!item) throw new Error(uiText("Elemento non più disponibile."));
          revision = current.revision;
          return { comments: validDiscussion(item.discussion), pending: !!current.pending, readOnly: current.archive.access === "read" };
        }, post: async (author, text) => {
          await rpc("archiveCommentAdd", { archiveId, recordId: record.id, revision, author, text, localOnly: !!value.offline });
        } });
      }));
      if (value.archive.access !== "read") actions.append(run(uiText("Salva nome e note"), async () => {
        await api("archiveUpdateRecord", { revision: value.revision, recordId: record.id, name: name.value, comment: notes.value });
        await open();
      }, true));
      if (value.archive.access !== "read") actions.append(run(uiText("Elimina elemento"), async () => {
        await api("archiveDeleteRecord", { revision: value.revision, recordId: record.id });
        await open();
      }));
      card.append(actions);
      content.append(card);
    }
    if (value.archive.access !== "read") {
      const form = document.createElement("form");
      form.className = "settings-card";
      const heading = document.createElement("h3");
      heading.textContent = uiText("Aggiungi password");
      form.append(heading);
      const name = input(form, "Nome della nuova password"), secret = input(form, "Nuova password", "", "password");
      name.maxLength = 100;
      secret.maxLength = 8192;
      secret.autocomplete = "new-password";
      form.append(run(uiText("Cifra e salva password"), async () => {
        const password = secret.value;
        secret.value = "";
        await api("archiveAddPassword", { revision: value.revision, name: name.value, password });
        await open();
      }, true));
      form.onsubmit = (e) => e.preventDefault();
      content.append(form);
    }
    if (!value.records.length) {
      const empty = document.createElement("p");
      empty.textContent = uiText("Nessuna chiave o password in questo archivio.");
      content.append(empty);
    }
    if (value.profiles.length) {
      const heading = document.createElement("h3");
      heading.textContent = uiText("Connessioni conservate");
      content.append(heading);
      for (const profile of value.profiles) {
        const p = document.createElement("p");
        p.textContent = profile.name + " · " + profile.protocol + " · " + profile.host + (profile.port ? ":" + profile.port : "");
        content.append(p);
      }
    }
  }
  function expiryControl(parent, initial) {
    const label = document.createElement("label"), select = document.createElement("select"), custom = document.createElement("input");
    label.className = "archive-field";
    label.textContent = uiText("Scadenza dell’accesso");
    select.id = "archive-expiry-" + crypto.randomUUID();
    label.htmlFor = select.id;
    for (const [value, text] of [["none", "Senza scadenza"], ["7", "Tra 7 giorni"], ["30", "Tra 30 giorni"], ["custom", "Data e ora personalizzate"]]) {
      const option = document.createElement("option");
      option.value = value;
      option.textContent = uiText(text);
      select.append(option);
    }
    parent.append(label, select);
    custom.type = "datetime-local";
    interfaceAttribute(custom, 'aria-label', "Data e ora locali");
    if (initial) {
      const date = new Date(initial);
      custom.value = new Date(date.getTime() - date.getTimezoneOffset() * 6e4).toISOString().slice(0, 16);
      select.value = "custom";
    }
    select.onchange = () => custom.hidden = select.value !== "custom";
    select.onchange();
    parent.append(custom);
    const read = () => {
      if (select.value === "none") return null;
      const date = select.value === "custom" ? new Date(custom.value) : new Date(Date.now() + Number(select.value) * 864e5);
      if (!Number.isFinite(date.getTime()) || date.getTime() < Date.now() + 6e4) throw Error(uiText("Scegli una scadenza futura di almeno un minuto."));
      return date.toISOString();
    };
    return Object.assign(read, { preset(days) {
      if (!["none", "7", "30"].includes(days)) throw Error("Invalid preset");
      select.value = days;
      select.onchange();
    } });
  }
  async function history(page = 0) {
    const rows = await api("archiveActivity", { page });
    content.replaceChildren();
    toolbar.replaceChildren(run(uiText("Torna agli accessi"), members));
    const heading = document.createElement("h3");
    heading.textContent = uiText("Cronologia degli accessi");
    content.append(heading);
    const names = { "created": "Archivio creato", "renamed": "Archivio rinominato", "member.changed": "Permessi aggiornati", "member.removed": "Accesso revocato", "sharing.paused": "Condivisione sospesa.", "sharing.resumed": "Condivisione riattivata.", "ownership.transferred": "Proprietà trasferita" }, list2 = document.createElement("ol");
    for (const row of rows) {
      const item = document.createElement("li");
      item.textContent = new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(row.createdAt)) + " · " + uiText(names[row.action] || row.action) + (row.access ? " · " + uiText(row.access === "read" ? "Sola lettura" : "Lettura e scrittura") : "");
      list2.append(item);
    }
    content.append(list2);
    if (!rows.length) status.textContent = uiText("Nessuna operazione registrata.");
    if (page > 0) toolbar.append(run(uiText("Precedente"), () => history(page - 1)));
    if (rows.length === 50) toolbar.append(run(uiText("Successiva"), () => history(page + 1)));
  }
  async function sharing(suspended) {
    const review = await api("archiveSharingReview", { suspended });
    content.replaceChildren();
    toolbar.replaceChildren(run(uiText("Torna agli accessi"), members));
    const heading = document.createElement("h3");
    heading.textContent = uiText(suspended ? "Sospendi condivisione" : "Riattiva condivisione");
    content.append(heading);
    const help = document.createElement("p");
    help.textContent = uiText(suspended ? "Gli altri utenti perderanno l’accesso online. Il titolare conserva l’accesso; le copie già scaricate non vengono cancellate." : "Verranno riattivati soltanto gli accessi non scaduti elencati qui. Controlla i permessi prima di continuare.");
    content.append(help);
    for (const member of review.members) {
      const p = document.createElement("p");
      p.className = "user-content";
      p.textContent = (member.email || member.account) + " · " + uiText(member.account === selected.ownerAccount ? "Titolare" : member.access === "read" ? "Sola lettura" : "Lettura e scrittura") + " · " + (member.expiresAt ? new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(member.expiresAt)) : uiText("Senza scadenza"));
      content.append(p);
    }
    content.append(run(uiText("Conferma con nuova verifica 2FA"), async () => {
      await api("archiveSharingSet", { suspended, intent: review.intent });
      selected.sharingSuspended = suspended;
      await members();
      status.textContent = uiText(suspended ? "Condivisione sospesa." : "Condivisione riattivata.");
    }));
  }
  async function members() {
    content.replaceChildren();
    toolbar.replaceChildren(run(uiText("Tutti gli archivi"), () => list(false)), run(uiText("Apri archivio"), open));
    if (selected.access !== "owner") {
      status.textContent = uiText("Titolare: ") + (selected.ownerName || "Account del titolare") + ". Solo il titolare gestisce gli accessi.";
      return;
    }
    status.textContent = uiText("La revoca impedisce i successivi accessi online, ma non cancella le copie già scaricate.");
    toolbar.append(run(uiText(selected.sharingSuspended ? "Riattiva condivisione" : "Sospendi condivisione"), () => sharing(!selected.sharingSuspended)));
    toolbar.append(run(uiText("Revisione periodica degli accessi"), () => archiveAccessReviewDialog(selected)));
    toolbar.append(run(uiText("Cronologia degli accessi"), () => history()), run(uiText("Scarica riepilogo permessi"), async () => {
      if (await api("archivePermissionsReport")) status.textContent = uiText("Riepilogo salvato.");
    }), run(uiText("Scarica PDF"), async () => {
      if (await api("archivePermissionsReport", { format: "pdf" })) status.textContent = uiText("Riepilogo salvato.");
    }));
    const rename = document.createElement("section");
    rename.className = "settings-card";
    const newName = input(rename, "Nome archivio", selected.name);
    newName.maxLength = 100;
    rename.append(run(uiText("Salva nome archivio"), async () => {
      await api("archiveRename", { name: newName.value });
      selected.name = newName.value;
      status.textContent = uiText("Nome aggiornato.");
    }));
    content.append(rename);
    for (const member of await api("archiveMembers")) {
      const row = document.createElement("section");
      row.className = "settings-card";
      const heading2 = document.createElement("h3");
      heading2.textContent = member.email || (member.account === selected.ownerAccount ? "Titolare" : "Utente collegato");
      row.append(heading2);
      if (member.account === selected.ownerAccount) {
        const note = document.createElement("p");
        note.textContent = uiText("Titolare unico");
        row.append(note);
      } else {
        const permissions = document.createElement("select");
        permissions.setAttribute("aria-label", uiText("Permessi di ") + (member.email || "utente collegato"));
        for (const [value, label] of [["read", "Sola lettura"], ["write", "Lettura e scrittura"]]) {
          const o = document.createElement("option");
          o.value = value;
          o.textContent = label;
          o.selected = value === member.access;
          o.disabled = selected.personal && value === "write";
          permissions.append(o);
        }
        const expires2 = expiryControl(row, member.expiresAt);
        row.append(permissions, run(uiText("Salva permessi"), async () => {
          if (!member.email) throw Error("Aggiungi nuovamente il destinatario tramite email.");
          const result = await api("archiveShare", { email: member.email, access: permissions.value, expiresAt: expires2() });
          status.textContent = result.message;
        }), run(uiText("Autorizza dispositivi"), async () => {
          await api("archiveAuthorize", { account: member.account });
          status.textContent = uiText("Dispositivi autorizzati.");
        }), run(uiText("Revoca accesso"), async () => {
          if (!await call("confirmAction", { text: "Revocare l’accesso di " + (member.email || "questo utente") + "?" })) return;
          await api("archiveRevoke", { account: member.account });
          await members();
        }));
      }
      content.append(row);
    }
    const share = document.createElement("section");
    share.className = "settings-card";
    const heading = document.createElement("h3");
    heading.textContent = uiText("Condividi con un utente");
    share.append(heading);
    const email = input(share, "Email del destinatario", "", "email");
    email.maxLength = 254;
    const write = input(share, "Permetti modifiche", "", "checkbox");
    if (selected.personal) {
      write.disabled = true;
      notice(share, uiText("L’archivio personale sincronizzato può essere condiviso solo in lettura. Per collaborare in scrittura crea un archivio separato."));
    }
    const expires = expiryControl(share);
    sharingPresetControl(share, !!selected.personal, (access, days) => {
      write.checked = access === "write" && !selected.personal;
      expires.preset(days);
    });
    share.append(run(uiText("Condividi archivio"), async () => {
      const result = await api("archiveShare", { email: email.value, access: write.checked ? "write" : "read", expiresAt: expires() });
      await members();
      status.textContent = result.message;
    }, true));
    content.append(share);
    const transfer = document.createElement("section");
    transfer.className = "settings-card";
    const transferHeading = document.createElement("h3");
    transferHeading.textContent = uiText("Trasferisci la proprietà");
    transfer.append(transferHeading);
    const target = input(transfer, "Email del nuovo titolare", "", "email"), keep = input(transfer, "Conserva il mio accesso in lettura e scrittura", "", "checkbox");
    target.maxLength = 254;
    transfer.append(run(uiText("Trasferisci con nuova verifica 2FA"), async () => {
      const completed = await api("archiveTransfer", { email: target.value, keepAccess: keep.checked });
      if (!completed) {
        status.textContent = uiText("Trasferimento annullato.");
        return;
      }
      await list();
      status.textContent = uiText("Trasferimento completato. Il titolare è uno solo.");
    }));
    content.append(transfer);
  }
  async function create() {
    content.replaceChildren();
    toolbar.replaceChildren(run(uiText("Tutti gli archivi"), () => list(false)));
    status.textContent = uiText("Il nuovo archivio si apre con il tuo account sui dispositivi autorizzati. Conserva il codice di recupero se perdi tutti i dispositivi.");
    const name = input(content, "Nome del nuovo archivio");
    name.maxLength = 100;
    const recovery2 = Array.from(crypto.getRandomValues(new Uint8Array(24)), (b) => b.toString(16).padStart(2, "0")).join("");
    const code = input(content, "Codice di recupero", recovery2);
    code.readOnly = true;
    content.append(run(uiText("Copia codice di recupero"), () => call("clipboardWrite", { text: recovery2 })));
    const saved = input(content, "Ho conservato il codice di recupero", "", "checkbox");
    content.append(run(uiText("Crea archivio cifrato"), async () => {
      if (!saved.checked || !name.value.trim()) throw Error("Indica il nome e conferma di aver conservato il codice.");
      selected = await call("archiveCreate", { name: name.value.trim(), recovery: recovery2 });
      await open();
    }, true));
  }
  const recover = document.createElement("details");
  const summary = document.createElement("summary");
  summary.textContent = uiText("Recupero di un archivio di cui sono titolare");
  recover.append(summary);
  const recovery = input(recover, "Codice di recupero o password precedente", "", "password");
  recovery.autocomplete = "off";
  recover.append(run(uiText("Collega questo dispositivo"), async () => {
    if (!selected) throw Error("Scegli prima un archivio.");
    const value = recovery.value;
    recovery.value = "";
    await api("archiveRecover", { recovery: value });
    await open();
  }));
  body.append(recover);
  const available = await list();
  if (initialArchiveId) {
    selected = available.find((archive) => archive.id === initialArchiveId) || null;
    if (!selected) {
      status.textContent = uiText("Archivio non disponibile per questo account.");
      return;
    }
    await open();
  }
}
async function plainArchiveExportDialog(archive) {
  let opened;
  try {
    opened = await rpc("archivePlainReview", { archiveId: archive.id });
  } catch (error) {
    toast(error.message, true);
    return;
  }
  const previous = document.activeElement, review = opened.review, token = opened.token;
  const dialog = document.createElement("dialog");
  dialog.className = "support-create-dialog account-personal-dialog";
  dialog.dataset.plainExport = token;
  dialog.setAttribute("aria-labelledby", "plain-export-heading");
  const heading = document.createElement("h2");
  heading.id = "plain-export-heading";
  heading.textContent = uiText("Esporta tutti i contenuti in chiaro");
  const summary = document.createElement("p");
  summary.textContent = review.name + " · revisione " + review.revision;
  const help = document.createElement("p");
  help.id = "plain-export-help";
  help.textContent = uiText("Il file ZIP contiene tutti i dati della revisione corrente: password leggibili, chiavi nel formato originale, connessioni, cartelle, snippet, workspace e chiavi host. Le passphrase dei file originali delle chiavi restano conservate. Sono escluse versioni storiche, fattori dell’account e ticket.");
  dialog.setAttribute("aria-describedby", help.id);
  dialog.append(heading, summary, help);
  const form = document.createElement("form");
  const label = document.createElement("label");
  label.className = "check";
  const consent = document.createElement("input");
  consent.type = "checkbox";
  consent.required = true;
  label.append(consent, "Conserverò questa copia in chiaro in una posizione protetta.");
  form.append(label);
  let password = null;
  if (review.format === 1) {
    const legacy = document.createElement("p");
    legacy.textContent = uiText("Il pacchetto precedente contiene un solo file. Il suo nome originale sarà registrato separatamente nel ZIP.");
    form.append(legacy);
    password = field(form, uiText("Password del pacchetto precedente"), "password", "", "password");
    password.required = true;
    password.maxLength = 4096;
    password.autocomplete = "current-password";
  }
  const code = field(form, uiText("Codice ricevuto via email"), "code");
  code.inputMode = "numeric";
  code.autocomplete = "one-time-code";
  code.pattern = "[0-9 -]{8,16}";
  code.maxLength = 16;
  code.closest("label").hidden = true;
  const destination = document.createElement("p");
  destination.className = "user-content";
  form.append(destination);
  const actions = document.createElement("div");
  actions.className = "form-actions";
  let busy = false, requested = false;
  const status = document.createElement("p");
  status.setAttribute("role", "status");
  const cancel = localizedButton("Annulla", () => {
    if (!busy) dialog.close();
  }), submit = localizedButton("Verifica 2FA e invia codice email", () => {
  }, "primary");
  submit.type = "submit";
  actions.append(cancel, submit);
  form.append(actions, status);
  dialog.append(form);
  document.body.append(dialog);
  dialog.showModal();
  consent.focus();
  dialog.addEventListener("cancel", (e) => {
    if (busy) e.preventDefault();
  });
  dialog.addEventListener("close", () => {
    code.value = "";
    if (password) password.value = "";
    dialog.remove();
    void rpc("archivePlainCancel", { token }).catch(() => {
    });
    if (previous?.isConnected) previous.focus();
  }, { once: true });
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (busy) return;
    busy = true;
    dialog.setAttribute("aria-busy", "true");
    for (const item of form.querySelectorAll("input,button")) item.disabled = true;
    status.textContent = requested ? "Verifica del codice e salvataggio sul dispositivo…" : "Conferma la tua identità nella finestra protetta…";
    try {
      if (!requested) {
        const value = await rpc("archivePlainBegin", { token, password: password?.value || "" });
        if (value.cancelled) {
          status.textContent = uiText("Esportazione annullata prima dell’invio del codice.");
          return;
        }
        requested = true;
        consent.disabled = true;
        destination.textContent = uiText("Codice inviato a ") + value.email + " · scadenza " + new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(value.expiresAt));
        code.required = true;
        code.closest("label").hidden = false;
        submit.textContent = uiText("Salva ZIP in chiaro");
        status.textContent = uiText("Inserisci il codice email. La posizione del file viene scelta nel salvataggio di Windows.");
      } else {
        const result = await rpc("archivePlainSave", { token, code: code.value, password: password?.value || "" });
        if (result.cancelled) {
          status.textContent = uiText("Salvataggio annullato. Puoi scegliere un’altra posizione.");
          return;
        }
        if (result.saved) {
          status.textContent = uiText("Pacchetto ZIP salvato. Puoi salvarlo nuovamente fino alla chiusura o alla scadenza.");
          code.value = "";
          code.required = false;
          code.closest("label").hidden = true;
          if (password) {
            password.value = "";
            password.required = false;
          }
          submit.textContent = uiText("Salva nuovamente il pacchetto");
          cancel.textContent = uiText("Chiudi");
        }
      }
    } catch (error) {
      status.textContent = error.message;
    } finally {
      busy = false;
      dialog.setAttribute("aria-busy", "false");
      for (const item of form.querySelectorAll("input,button")) item.disabled = item === consent && requested;
      if (requested && !code.closest("label").hidden) code.focus();
      else submit.focus();
    }
  };
}
function sharingPresetControl(parent, readOnly, apply) {
  const box = document.createElement("section"), label = document.createElement("label"), select = document.createElement("select"), button2 = document.createElement("button"), help = document.createElement("p");
  select.id = "sharing-preset-" + crypto.randomUUID();
  label.htmlFor = select.id;
  label.textContent = uiText("Modello di permessi");
  const presets = [["read", "7", "Sola lettura per 7 giorni"], ["read", "30", "Sola lettura per 30 giorni"], ["read", "none", "Sola lettura senza scadenza"], ["write", "7", "Lettura e scrittura per 7 giorni"], ["write", "30", "Lettura e scrittura per 30 giorni"], ["write", "none", "Lettura e scrittura senza scadenza"]];
  for (const [access, days, title] of presets) {
    if (readOnly && access === "write") continue;
    const option = document.createElement("option");
    option.value = access + ":" + days;
    option.textContent = uiText(title);
    select.append(option);
  }
  help.id = select.id + "-help";
  help.textContent = uiText("Il modello compila i campi sottostanti. Controlla destinatario, permessi e scadenza prima di confermare ogni invito.");
  select.setAttribute("aria-describedby", help.id);
  button2.type = "button";
  button2.textContent = uiText("Applica modello");
  button2.onclick = () => {
    const [access, days] = select.value.split(":");
    apply(access, days);
  };
  box.className = "sharing-presets";
  box.append(label, select, button2, help);
  parent.append(box);
}
