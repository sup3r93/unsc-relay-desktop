# Planned improvements

## English

### Installer appearance — requested 18 November 2026

- [ ] Extend the branded background across the complete installer window, including the installation/progress page and footer. Keep text contrast and readability at different display scales.
- [ ] Use the UNSC Relay icon consistently for the installer executable, window, taskbar entry and shortcuts; verify it in a real Windows installer run.

### Account selection and desktop account menu — requested 18 November 2026

- [ ] Replace the current initial access card with a Microsoft-inspired account-selection screen on both desktop and web, adapted to UNSC Relay branding. Show saved identities and “Use another account”; let the user choose the account before the corresponding authentication/unlock step.
- [ ] Make this selector the initial desktop screen when opening the app, rather than requiring an extra button to reach remembered accounts. Use the same selection-first approach when entering the web sign-in flow.
- [ ] Replace the desktop top-right “Account” button with a GitHub-inspired avatar/identity dropdown.
- [ ] Consolidate account actions, Settings and the Music on/off control in that dropdown instead of separate top-bar buttons.
- [ ] Add the requested “Switch account” button just below, reopening the same account-selection screen.
- [ ] Keep account data isolation, required authentication and manually enabled Windows Hello. The design references specify UI/UX, not reduced security. Support keyboard navigation, visible focus and screen readers.

### Complete Italian and English localization — requested 18 November 2026

- [ ] Also add French, Spanish and German, selected following the user’s authorization for three additional Western languages. The planned language set is Italian, English, French, Spanish and German. Apply the same complete translation, terminology, locale formatting and accessibility review to all five languages.
- [ ] Make both desktop and web fully multilingual: retain Italian and add English as the second language.
- [ ] Translate all user-facing text thoroughly and naturally: navigation, account selection, authentication/OTP, settings, dialogs, errors, validation, notifications, accessibility labels, archive sharing, transfers, updates, installer and public/help pages.
- [ ] Provide a language selector and remember the choice. Keep terminology consistent, use locale-appropriate dates/numbers/plurals and update document language and screen-reader text correctly.
- [ ] Review translation quality and completeness in both languages, including text expansion, keyboard navigation and screen-reader behavior. Avoid mixed-language screens, untranslated fallbacks or machine-like phrasing.

### Desktop reports and support tickets — requested 18 November 2026

- [ ] Add “Report a problem” / “Support and feedback” to the new avatar/account dropdown.
- [ ] Provide an accessible guided form with categories: bug, feature request and other support/feedback. Clearly state that titles, descriptions and replies must be written in English, regardless of the selected interface language.
- [ ] For bugs, collect a title, affected area, steps to reproduce, expected/actual behavior and optional attachments. Offer a feature-request form with the use case and requested improvement, plus a general-support form.
- [ ] Associate each ticket with the selected signed-in account. Reuse and extend the existing UNSC support ticket service for desktop submission, ticket numbers, status, replies and history, keeping different accounts’ tickets isolated.
- [ ] Provide “My tickets” with open/waiting/resolved/closed states, readable status notifications, replies and an explicit resolution/reopening flow where appropriate. Route management through the website’s existing support/admin area.
- [ ] Offer app version and optional diagnostic information for review before sending. Never attach credentials, archive contents, private keys, tokens or logs/screenshots automatically; let the user inspect and explicitly choose attachments.
- [ ] Define handling for missing connectivity: preserve an explicitly saved draft, show whether sending succeeded and avoid duplicate submissions. Do not silently send a draft later.
- [ ] Keep private support tickets separate from public GitHub issues. Any public escalation requires explicit consent and a review of the content to publish.

### Approved feature additions — approved 18 November 2026

- [ ] Account visual identity (desktop and web): personal aliases and colors such as Personal, Work and Test, clearly visible to reduce wrong-account actions.
- [ ] Synchronization center (desktop and web): one overview of connected archives, last successful synchronization, pending changes, errors and conflicts requiring action.
- [ ] Guided connection verification (desktop): explain address/DNS, port, authentication and server host-key checks without automatically executing connection macros.
- [ ] Connection templates (desktop): create connections from reusable configurations, explicitly choosing which settings and credentials to reuse.
- [ ] Shared-archive activity (desktop and web): show invitations, permission changes, revocations and ownership transfers without exposing secret contents.
- [ ] Expiring shared access (desktop and web): grant access until a chosen date. Expiration prevents subsequent service access; it does not erase copies already downloaded.
- [ ] Accessibility preferences (desktop and web): text size, density, contrast and motion preferences, with preview and remembered settings.

### Outro redesign — requested 18 November 2026

- [ ] Review the complete desktop outro. Replace the current closing angular panels with a sequence of recognizable armored science-fiction doors closing, inspired by the two reference door images supplied by the user.
- [ ] Develop a coherent UNSC-style mechanical design: frames, sliding door leaves and a clear closing/sealing movement, rather than the current abstract overlapping shutters. Review the sequence, visual composition, pacing and final frame.
- [ ] Coordinate the animation with existing outro audio and audio settings. Check ultrawide displays, window proportions and different display scales; avoid cut-off text or unintended geometry.
- [ ] Respect reduced-motion preferences and preserve the safe application/session shutdown flow. Prepare a preview for visual review before considering the redesign finished.

### Additional approved features and connection-picker tabs — approved 18 November 2026

- [ ] Presentation mode (desktop and web): hide account emails, server names, IP addresses and sensitive notifications while sharing a screen. Clearly indicate that this is visual masking, not a guarantee that every external capture or log is sanitized.
- [ ] Production-environment protection (desktop): clearly label production connections and require additional confirmation for file deletion, overwrite and bulk file operations. Do not promise inspection or interception of arbitrary terminal commands.
- [ ] Synchronization diagnostics (desktop and web): explain offline network, expired session, unauthorized device, quota and conflict conditions with relevant next steps. Integrate this into the already planned synchronization center rather than duplicating it.
- [ ] Local operation history (desktop): inspect connection, transfer and file-operation events without recording passwords or terminal contents; provide appropriate retention and clearing controls.
- [ ] Operational connection notes (desktop): associate instructions, contacts, maintenance notes and checklists with a server, without automatically executing those instructions.
- [ ] Configurable notifications (desktop and web): choose event categories, quiet mode and summaries to reduce notification overload while keeping important security information clear.
- [ ] Archive-access requests (desktop and web): users can request access; only the archive owner can approve, choose read/write permissions and set any expiry. Do not enable access automatically or reveal private archive details to unauthorized requesters.
- [ ] Divide the “New connection / What session do you want to create?” picker into accessible category tabs instead of a long stacked grid. Start from the categories in the supplied screenshot: Remote connections, Local terminals and Included components; refine grouping if needed without hiding available session types.
- [ ] Retain session-type search, show the selected category clearly and define how searching across categories reveals results. Support keyboard arrows, Home/End, visible focus, screen-reader tab/panel relationships, narrow windows and 200% text size.

### Integrated account and authentication-method management — requested 18 November 2026

- [ ] Replace the desktop account dialog’s website-launching management buttons with working in-app account management. Viewing profile and security information must reuse the selected account’s authenticated session, not open a separate page and request another login for every action.
- [ ] Support editing personal details and changing the account password within the app. Ask for password/identity confirmation immediately before sensitive changes where required, rather than before simply viewing account information. Preserve mandatory 2FA and administrator authentication requirements; do not replace them with an insecure password-only flow.
- [ ] In both desktop and web, prominently list the selected account’s registered passkeys, OTP authenticators and FIDO2 security keys, grouped by type. Show names and genuine available metadata such as registration/last-use dates; never invent unavailable metadata or reveal existing OTP secrets/private keys.
- [ ] Offer method renaming, revocation and replacement/reconfiguration. A passkey or FIDO2 credential is replaced through registration of a new credential and revocation of the old one, not by editing its private key. Reconfigure OTP through a verified new enrollment.
- [ ] Require a clear confirmation before revocation; preserve required factors and prevent accidental lockout, verifying a replacement before removing a required or last usable method.
- [ ] Keep every read/write operation bound to the selected authenticated account with server-side ownership checks and appropriate fresh verification. Refresh the displayed lists after changes and give clear accessible success/error feedback.
- [ ] Consolidate and verify existing web security-method management rather than duplicating it. Make desktop and web capabilities consistent and accessible, including keyboard/focus, validation and all planned interface languages.

### Long account recovery codes and PDF export — requested 18 November 2026

- [ ] Add one-time account recovery/security codes as an alternative recovery path when the usual authentication factors are unavailable. Specify which recovery stage they authorize and how required factors are securely re-enrolled afterward; keep account recovery distinct from encrypted-archive recovery keys and do not promise archive decryption from a code alone.
- [ ] Generate cryptographically random codes of at least 64 characters each, using a copy-friendly alphabet and optional visual grouping that does not reduce the actual code length. Verify identity and require an explicit action before issuing or regenerating codes.
- [ ] Let the user export an accessible PDF and choose its save location on desktop or download/save it through the browser. Include the account identifier, issue date, clear English/localized instructions, exact selectable code text and spaces to mark used codes; support all planned interface languages.
- [ ] Show plaintext codes only during issuance/export. Store only suitable verifier hashes on the server, not plaintext codes or the generated PDF. Keep codes out of logs, telemetry, automatic support attachments and routine clipboard operations.
- [ ] Consume each code atomically once, prevent replay/concurrent reuse, rate-limit verification and notify the account about recovery events without including codes.
- [ ] Show remaining-code count, permit explicit revocation, and support generation of a new set that invalidates the previous set. Preserve the administrator recovery policy and protection against account lockout.
- [ ] Explain that the PDF contains account-recovery secrets and must be kept privately. The user chooses where to store it; do not automatically upload it, email it or save it inside a shared archive.
- [ ] Assess identity-provider support for the requested length and securely implement an appropriate integration if the built-in format is shorter. Do not silently shorten the codes. Complete recovery, PDF/accessibility and security tests before release.

### New support ticket in a modal — requested 18 November 2026

- [ ] In the Support page, replace the inline new-ticket form with an “Open a ticket” button that opens an accessible modal dialog.
- [ ] Put title, category, priority, description and submission feedback inside the dialog. Keep the ticket list available on the underlying page and preserve the already requested English-only ticket-content guidance.
- [ ] Manage initial focus, keyboard navigation, Escape/close behavior and focus restoration. Prevent accidental loss of a partially completed report and show clear validation and successful-submission feedback.

### Notification-center bulk read and proportional layout — requested 18 November 2026

- [ ] Add a clearly visible “Mark all as read” action in the notification center. Apply it only to the selected authenticated account’s notifications; do not delete notifications or change other accounts’ state.
- [ ] Update unread badges/counts and the displayed read states after successful completion, including the header bell. Show pending/error feedback, avoid duplicate requests and disable or hide the action when nothing is unread.
- [ ] Replace oversized notification containers and excessive vertical gaps with compact, content-proportional rows/cards. Remove unnecessary fixed/minimum heights and excessive padding while retaining clear title, date, message, action and read/unread distinction.
- [ ] Keep responsive wrapping, comfortable reading width, accessible target sizes, visible keyboard focus and screen-reader status announcements. Verify long messages, narrow screens and 200% text size.

### Searchable grouped time-zone selector — requested 18 November 2026

- [ ] Replace the free-text Time zone field with an accessible searchable dropdown/combobox with autocomplete.
- [ ] Group zones clearly by geographic region/continent, with a distinct UTC option. Present readable localized city/zone labels together with their current UTC offset, while storing a valid canonical IANA identifier such as Europe/Rome.
- [ ] Search by city, region, IANA identifier or displayed offset. Handle daylight-saving changes using time-zone rules, not a permanently fixed offset. Preserve the user’s saved choice and validate it server-side.
- [ ] Support keyboard selection, visible focus, screen-reader announcements, loading/no-results feedback and all planned interface languages. Apply consistently wherever time-zone preferences are exposed on desktop and web.

### Clear branded account-change confirmation — requested 18 November 2026

- [ ] Replace the browser-native generic account-change confirm dialog shown in the user’s screenshot with an accessible UNSC-styled confirmation modal.
- [ ] Use a specific title, identify the affected account and summarize the actual proposed changes, showing old/new values where appropriate without exposing secrets. Explain relevant consequences, such as verifying a changed email address.
- [ ] Replace generic OK with an explicit action such as “Save changes”; keep “Cancel” clearly available. Localize the copy in all planned languages and use stronger, consequence-specific wording for destructive actions.
- [ ] Keep this confirmation distinct from required identity/password verification. Support initial focus, keyboard navigation, Escape/cancel, focus restoration and clear submission/error feedback without accidental duplicate submissions.

### Richer administrator user list and wider account-management dialog — requested 18 November 2026

- [ ] Show useful account information directly in the administrator user list, including separate archive and attachment quotas, actual used space where available, remaining space/usage indicators, and a clear Unlimited state. Do not invent usage figures or infer them from quota limits.
- [ ] Keep account identity, email verification, role and status readable alongside the new information. Prioritize useful columns and provide responsive presentation instead of overcrowding the table.
- [ ] Redesign the administrator “Manage account” modal to be wider and more horizontal on large screens, with a balanced arrangement of personal details, status, role, storage quotas and security sections.
- [ ] Size cards to their content rather than stretching a short form into a tall empty container. Reduce unnecessary vertical scrolling and excess gaps; keep primary actions and the account identity easy to find.
- [ ] Preserve a usable stacked layout on narrow screens and at 200% text size, accessible keyboard/focus behavior, and all existing role-change, quota, identity-confirmation and administrative safeguards.

### Storage, integrity and operation controls — approved 18 November 2026

- [ ] Storage usage explorer (desktop and web): identify archives and attachments consuming space, with an explicit cleanup preview before deleting anything.
- [ ] Archive integrity checks (desktop and web): verify encrypted-copy structure and consistency and, where authenticated decryption is needed, perform it in the authorized client without exposing plaintext to the server. Show the last completed verification and do not claim recovery without a verified recoverable copy.
- [x] Reusable sharing-permission presets (desktop and web), such as read-only access for seven days, with owner confirmation on every invitation.
- [ ] Transfer preflight (desktop): report insufficient space, incompatible names, existing destinations and symbolic links when these can actually be checked before starting.
- [ ] Unified operation panel (desktop): transfers, synchronization and exports with progress, errors and cancellation where supported. Coordinate with the planned synchronization center, without duplicate/conflicting state.
- [x] Exit summary (desktop): summarize open sessions, active transfers and unsaved changes, with explicit options to continue closing or return to work. Integrate with optional background mode and the planned outro without changing safety guarantees.

### Twenty additional approved features — approved 18 November 2026

1. [ ] Customizable personal home (desktop/web): selectable archive, activity, ticket and notification summaries.
2. [ ] Saved views and filters (desktop/web): reusable searches for frequent user, ticket and other list queries.
3. [ ] Remote file search (desktop): authorized folders, filename/extension/size/date criteria, bounded execution and cancellation.
4. [ ] Server-to-server transfers (desktop): clearly explain whether data passes through the PC and the required temporary storage.
5. [x] Live remote log viewer (desktop): search/filter and pause scrolling without repeatedly downloading the entire file.
6. [x] Connection topology (desktop): visualize the PC, jump hosts, destination, tunnels and ports.
7. [ ] Guided public-key installation (desktop): preview authorized SSH configuration changes and verify new access before retiring the old method.
8. [ ] On-demand server information (desktop): available OS/disk details with collection timestamps and explicit authorized inspection.
9. [ ] Protected attachment previews (desktop/web): preview supported formats without automatically launching executables or active content.
10. [ ] Periodic shared-access reviews (desktop/web): remind archive owners to review continuing permissions.
11. [ ] Permission-report PDF/CSV export (desktop/web): users, permissions and expiry dates without secret content.
12. [ ] Sharing suspension (desktop/web): owner-controlled suspension with an explicit review and controlled reactivation; downloaded copies cannot be erased remotely.
13. [ ] Comments on shared items (desktop/web): conversations visible only to authorized members, with suitable encryption/access controls.
14. [ ] Editable English support-response templates (web): staff explicitly review and send responses.
15. [ ] Related/duplicate ticket links (desktop/web): group issues without exposing another requester’s private conversation.
16. [ ] Contextual help and onboarding (desktop/web): guided account, connection and sharing workflows.
17. [ ] Selective preference synchronization (desktop/web): users choose transferable settings; device-specific settings stay separate.
18. [x] Metered-connection mode (desktop): control automatic transfers and heavy downloads, with clear pending states and explicit exceptions.
19. [ ] Resource-saving mode (desktop): reduce inactive-tab rendering/animation while preserving active sessions.
20. [ ] Data/privacy center (desktop/web): explain local/service data and provide separate controls for cache, diagnostics and temporary data.

### Complete plaintext archive export — requested 18 November 2026

- [ ] Allow the owner/authorized user to download the complete archive in plaintext, including all associated items and attachments within their export rights. Define a documented portable format with a manifest and preserve exact original key/file bytes where applicable.
- [ ] Require fresh 2FA and a separate one-time email verification code sent to the account’s verified address. Bind both proofs to the selected account and this export operation, expire them, rate-limit attempts and prevent replay.
- [ ] Decrypt/export in the authorized client, preserving the existing encryption boundary. Explicitly warn that the resulting files contain unencrypted secrets, let the user choose the destination and avoid automatic uploads/email delivery or persistent plaintext staging.
- [ ] Specify export rights for shared archives: do not let an unrelated member or administrator export another owner’s data merely because of a management role. Confirm scope and warn that previously downloaded plaintext cannot be revoked.
- [ ] Provide progress, cancellation, failure cleanup and an account notification/audit event without including exported contents or codes.

### Account deletion with reconsideration period — requested 18 November 2026

- [ ] Add a complete account-deletion request covering account identity, owned archives, attachments, devices/key grants, sessions/tokens, preferences and other associated personal data, with explicit scope and consequences.
- [ ] Require fresh identity verification with 2FA and email confirmation for the destructive request. Provide a reasonable configurable reconsideration period; provisionally plan 30 days, clearly displayed with the exact scheduled deletion date and timezone. The user has not yet selected a final duration.
- [ ] Permit explicit cancellation during that period after identity verification, with clear request/cancellation notifications. Define access and synchronization behavior while deletion is pending; never silently reset the deadline on login.
- [ ] Before accepting the request, explicitly handle ownership of shared archives: allow a verified transfer to another owner, or clearly confirm deletion and its consequences for collaborators. Remove the departing account’s memberships/grants from other owners’ archives without deleting those other archives.
- [ ] At the deadline, invalidate sessions and grants, revoke device/server unlock material and securely erase owned archive data, attachments and other associated data across the managed services and reachable app-managed local caches. Track completion/retries and prevent deleted data from reappearing through synchronization or restore.
- [ ] Define and disclose backup retention/purge and any strictly necessary retained records with an actual basis. Do not claim instant destruction of every backup, third-party copy or offline device. Copies/exports already saved by users or collaborators cannot be remotely guaranteed erased.
- [ ] Coordinate any encryption-key destruction with device copies, backups and genuinely recoverable material; do not label deletion irreversible while a usable managed recovery path remains. Keep secrets out of deletion logs and show an honest completion status.
- [ ] Protect the last active administrator: require a safe replacement/ownership transition before deleting an account whose removal would leave the service without administration.

These items are planned for a future version. They are not included in the published 0.2.19 package.

## Italiano

### Grafica dell’installer — richiesta del 18 novembre 2026

- [ ] Estendere lo sfondo personalizzato a tutta la finestra dell’installer, comprese pagina di installazione/avanzamento e fascia inferiore. Mantenere contrasto e leggibilità alle diverse scale dello schermo.
- [ ] Applicare l’icona UNSC Relay in modo coerente a eseguibile di installazione, finestra, barra delle applicazioni e collegamenti; verificarla durante un’installazione Windows reale.

### Selezione account e menu account desktop — richiesta del 18 novembre 2026

- [ ] Sostituire l’attuale scheda iniziale di accesso con una schermata di selezione account ispirata a Microsoft, sia nell’app sia sul web e adattata alla grafica UNSC Relay. Mostrare gli account salvati e “Usa un altro account”; l’utente sceglie prima l’account e poi esegue l’autenticazione o lo sblocco relativo.
- [ ] All’apertura dell’app mostrare direttamente questa schermata, senza un pulsante aggiuntivo per raggiungere gli account ricordati. Applicare la stessa selezione iniziale all’ingresso nel flusso di accesso web.
- [ ] Sostituire il pulsante “Account” in alto a destra nell’app con un menu a discesa basato su avatar/identità, ispirato a GitHub.
- [ ] Accorpare nel menu le azioni dell’account, Impostazioni e il controllo Musica attiva/disattiva, eliminando i pulsanti separati dalla barra superiore.
- [ ] Aggiungere appena sotto il pulsante “Cambia account”, che riapre la stessa schermata di selezione account.
- [ ] Conservare la separazione dei dati, le verifiche richieste e Windows Hello abilitato manualmente. I riferimenti riguardano UI/UX, non una riduzione della sicurezza. Garantire tastiera, focus visibile e lettori di schermo.

### Localizzazione completa in italiano e inglese — richiesta del 18 novembre 2026

- [ ] Aggiungere anche francese, spagnolo e tedesco, scelti su autorizzazione dell’utente per tre ulteriori lingue occidentali. Le lingue previste sono italiano, inglese, francese, spagnolo e tedesco. Applicare a tutte e cinque gli stessi requisiti di traduzione completa, terminologia, formati locali e verifica dell’accessibilità.
- [ ] Rendere desktop e web completamente multilingua: mantenere l’italiano e aggiungere l’inglese come seconda lingua.
- [ ] Tradurre integralmente e con cura ogni testo rivolto all’utente: navigazione, selezione account, accesso/OTP, impostazioni, finestre, errori, validazione, notifiche, etichette accessibili, condivisione archivi, trasferimenti, aggiornamenti, installer e pagine pubbliche/di assistenza.
- [ ] Prevedere un selettore della lingua e ricordare la scelta. Usare terminologia coerente, date/numeri/plurali adeguati alla lingua e aggiornare correttamente lingua del documento e testi per lettori di schermo.
- [ ] Verificare qualità e completezza in entrambe le lingue, compresi ingombri dei testi, tastiera e lettori di schermo. Evitare schermate miste, testi residui non tradotti o formulazioni innaturali.

### Segnalazioni e ticket dal desktop — richiesta del 18 novembre 2026

- [ ] Aggiungere “Segnala un problema” / “Assistenza e feedback” nel nuovo menu avatar/account.
- [ ] Prevedere un modulo guidato accessibile con categorie: bug, richiesta di funzionalità e altra assistenza/feedback. Specificare chiaramente che titolo, descrizione e risposte devono essere scritti in inglese, indipendentemente dalla lingua dell’interfaccia.
- [ ] Per i bug raccogliere titolo, area interessata, passaggi per riprodurre, comportamento atteso/effettivo e allegati facoltativi. Per le nuove funzionalità raccogliere esigenza e miglioramento richiesto; prevedere anche una segnalazione generica.
- [ ] Associare ogni ticket all’account selezionato e autenticato. Riutilizzare ed estendere il servizio ticket UNSC già presente per invio dal desktop, numero pratica, stato, risposte e storico, mantenendo separati i ticket dei diversi account.
- [ ] Prevedere “I miei ticket” con stati aperto/in attesa/risolto/chiuso, notifiche leggibili, risposte e un flusso esplicito di risoluzione/riapertura quando previsto. Gestire le richieste dall’area assistenza/amministrazione del sito già esistente.
- [ ] Proporre versione dell’app ed eventuale diagnostica da verificare prima dell’invio. Non allegare automaticamente credenziali, contenuti degli archivi, chiavi private, token, log o schermate; l’utente controlla e sceglie esplicitamente gli allegati.
- [ ] In assenza di rete consentire il salvataggio esplicito di una bozza, mostrare chiaramente l’esito dell’invio ed evitare duplicati. Non inviare automaticamente le bozze in seguito.
- [ ] Mantenere distinti i ticket privati e le issue pubbliche GitHub. Un’eventuale pubblicazione richiede consenso esplicito e revisione dei contenuti da rendere pubblici.

### Funzionalità approvate — approvazione del 18 novembre 2026

- [ ] Identità visiva degli account (desktop e web): alias e colori personali come Personale, Lavoro e Test, sempre riconoscibili per ridurre le operazioni con l’account sbagliato.
- [ ] Centro sincronizzazione (desktop e web): riepilogo degli archivi collegati, ultima sincronizzazione riuscita, modifiche in attesa, errori e conflitti da risolvere.
- [ ] Verifica guidata della connessione (desktop): spiegare controlli su indirizzo/DNS, porta, autenticazione e chiave del server senza eseguire automaticamente macro di connessione.
- [ ] Modelli di connessione (desktop): creare connessioni da configurazioni riutilizzabili, scegliendo esplicitamente quali impostazioni e credenziali riprendere.
- [ ] Attività degli archivi condivisi (desktop e web): mostrare inviti, modifiche ai permessi, revoche e trasferimenti di proprietà senza esporre contenuti segreti.
- [ ] Accessi condivisi con scadenza (desktop e web): concedere accesso fino a una data scelta. La scadenza impedisce accessi successivi al servizio, ma non cancella copie già scaricate.
- [ ] Preferenze di accessibilità (desktop e web): dimensione del testo, densità, contrasto e movimento regolabili, con anteprima e impostazioni ricordate.

### Revisione dell’outro — richiesta del 18 novembre 2026

- [ ] Revisionare l’intera outro desktop. Sostituire gli attuali pannelli angolari che si chiudono con una serie di porte corazzate fantascientifiche riconoscibili che si chiudono, ispirate alle due immagini di riferimento fornite dall’utente.
- [ ] Progettare un linguaggio meccanico coerente con UNSC: telai, ante scorrevoli e movimento chiaro di chiusura/sigillatura, al posto delle attuali forme astratte sovrapposte. Rivedere sequenza, composizione visiva, ritmo e fotogramma conclusivo.
- [ ] Coordinare l’animazione con l’audio dell’outro e le impostazioni audio esistenti. Verificare schermi ultrawide, proporzioni della finestra e scale diverse, evitando testi tagliati o geometrie involontarie.
- [ ] Rispettare le preferenze di movimento ridotto e conservare il flusso sicuro di chiusura di app e sessioni. Preparare un’anteprima da revisionare visivamente prima di considerare concluso il nuovo design.

### Ulteriori funzionalità e tab per nuove connessioni — approvazione del 18 novembre 2026

- [ ] Modalità presentazione (desktop e web): nascondere email, nomi dei server, indirizzi IP e notifiche sensibili durante la condivisione dello schermo. Chiarire che si tratta di mascheramento visivo, non della garanzia di anonimizzare ogni acquisizione esterna o log.
- [ ] Protezione degli ambienti di produzione (desktop): contrassegnare chiaramente le connessioni di produzione e richiedere conferma aggiuntiva per eliminazioni, sovrascritture e operazioni massive sui file. Non promettere analisi o intercettazione dei comandi arbitrari del terminale.
- [ ] Diagnostica della sincronizzazione (desktop e web): spiegare assenza di rete, sessione scaduta, dispositivo non autorizzato, quota e conflitti, indicando le azioni pertinenti. Integrarla nel centro sincronizzazione già previsto, evitando duplicazioni.
- [ ] Registro delle operazioni locali (desktop): consultare eventi di connessione, trasferimento e operazioni sui file senza registrare password o contenuti dei terminali; prevedere conservazione e cancellazione adeguate.
- [ ] Note operative per connessione (desktop): associare al server istruzioni, referenti, note di manutenzione e checklist senza eseguire automaticamente le istruzioni.
- [ ] Notifiche configurabili (desktop e web): scegliere categorie di eventi, modalità silenziosa e riepiloghi per ridurre il sovraccarico, mantenendo chiare le informazioni importanti per la sicurezza.
- [ ] Richieste di accesso agli archivi (desktop e web): l’utente può richiedere accesso; soltanto il titolare può approvare, scegliere lettura/scrittura ed eventuale scadenza. Nessuna abilitazione automatica e nessuna esposizione di dettagli privati a richiedenti non autorizzati.
- [ ] Dividere il popup “Nuova connessione / Che sessione vuoi creare?” in tab accessibili per categoria, sostituendo la lunga griglia verticale. Partire dalle categorie dello screenshot: Connessioni remote, Terminali locali e Componenti inclusi; affinare i gruppi se necessario senza nascondere i tipi di sessione disponibili.
- [ ] Conservare la ricerca dei tipi di sessione, rendere evidente la categoria selezionata e definire come mostrare i risultati tra categorie. Supportare frecce da tastiera, Home/End, focus visibile, associazioni tab/pannello per lettori di schermo, finestre strette e testo al 200%.

### Gestione integrata di account e metodi di autenticazione — richiesta del 18 novembre 2026

- [ ] Sostituire i pulsanti della pagina account desktop che aprono il sito con una gestione dell’account effettiva dentro l’app. La consultazione di profilo e sicurezza deve usare la sessione autenticata dell’account selezionato, senza aprire una pagina separata e richiedere un nuovo login per ogni azione.
- [ ] Consentire modifica dei dati personali e cambio della password dell’account dentro l’app. Richiedere password/conferma dell’identità prima delle modifiche sensibili quando necessario, non per la semplice consultazione. Conservare 2FA obbligatoria e requisiti di accesso degli amministratori, senza introdurre un flusso insicuro basato soltanto sulla password.
- [ ] Mostrare chiaramente, sia nel desktop sia nel web, l’elenco di passkey, autenticatori OTP e chiavi FIDO2 registrati per l’account selezionato, raggruppati per tipo. Riportare nomi e metadati realmente disponibili, come registrazione/ultimo utilizzo, senza inventare date o mostrare segreti OTP già registrati e chiavi private.
- [ ] Consentire rinomina, revoca e sostituzione/riconfigurazione dei metodi. Passkey e credenziali FIDO2 si sostituiscono registrando una nuova credenziale e revocando quella precedente, non modificandone la chiave privata. Riconfigurare OTP mediante una nuova registrazione verificata.
- [ ] Richiedere conferma esplicita prima della revoca e preservare i fattori obbligatori, evitando blocchi involontari dell’account: verificare il sostituto prima di rimuovere un metodo richiesto o l’ultimo metodo utilizzabile.
- [ ] Vincolare letture e modifiche all’account selezionato e autenticato, con verifica server della titolarità e conferma recente adeguata. Aggiornare gli elenchi dopo le modifiche e fornire esiti/errori chiari e accessibili.
- [ ] Consolidare e verificare la gestione web dei metodi già esistente, senza duplicarla. Rendere coerenti e accessibili le funzioni di app e sito, compresi tastiera/focus, validazione e tutte le lingue pianificate.

### Codici di recupero lunghi ed esportazione PDF — richiesta del 18 novembre 2026

- [ ] Aggiungere codici di sicurezza/recupero monouso per riaccedere all’account quando i normali fattori non sono disponibili. Definire quale fase del recupero autorizzano e come riconfigurare in sicurezza i fattori obbligatori; mantenere distinti recupero dell’account e chiavi dell’archivio cifrato, senza promettere la decifratura dell’archivio tramite il solo codice.
- [ ] Generare codici casuali crittograficamente sicuri, ciascuno lungo almeno 64 caratteri, con alfabeto agevole da copiare ed eventuali raggruppamenti visivi che non riducano la lunghezza effettiva. Verificare l’identità e richiedere un’azione esplicita per emissione o rigenerazione.
- [ ] Consentire l’esportazione in PDF accessibile e la scelta del percorso nel desktop, oppure il download/salvataggio tramite browser. Includere identificativo dell’account, data di emissione, istruzioni chiare nella lingua selezionata, testo dei codici selezionabile e spazi per segnare quelli utilizzati; supportare tutte le lingue pianificate.
- [ ] Mostrare i codici in chiaro soltanto durante emissione/esportazione. Conservare sul server soltanto hash di verifica adeguati, non i codici in chiaro o il PDF generato. Escludere i codici da log, telemetria, allegati automatici di assistenza e copie automatiche negli appunti.
- [ ] Consumare ogni codice una sola volta in modo atomico, impedire riutilizzi anche simultanei, limitare i tentativi e notificare gli eventi di recupero senza includere i codici.
- [ ] Mostrare quanti codici rimangono, consentire revoca esplicita e generazione di un nuovo insieme che invalidi quello precedente. Conservare le regole di recupero degli amministratori e le protezioni contro il blocco involontario dell’account.
- [ ] Spiegare che il PDF contiene segreti di recupero e va conservato privatamente. L’utente sceglie dove salvarlo; non caricarlo, inviarlo per email o salvarlo automaticamente in un archivio condiviso.
- [ ] Verificare il supporto del servizio di identità alla lunghezza richiesta e predisporre un’integrazione sicura se il formato nativo è più corto. Non ridurre silenziosamente la lunghezza dei codici. Completare test di recupero, PDF/accessibilità e sicurezza prima del rilascio.

### Nuovo ticket di assistenza in un popup — richiesta del 18 novembre 2026

- [ ] Nella scheda Assistenza sostituire il modulo di creazione ticket in-line con un pulsante “Apri un ticket” che apra un popup modale accessibile.
- [ ] Inserire nel popup titolo, categoria, priorità, descrizione ed esito dell’invio. Mantenere l’elenco dei ticket nella pagina sottostante e l’indicazione già richiesta di scrivere il contenuto dei ticket in inglese.
- [ ] Gestire focus iniziale, tastiera, chiusura/Esc e ritorno del focus al pulsante di apertura. Evitare la perdita accidentale di una segnalazione già compilata e mostrare chiaramente errori di validazione e conferma di creazione.

### Centro notifiche: lettura globale e layout proporzionato — richiesta del 18 novembre 2026

- [ ] Aggiungere nel centro notifiche un pulsante ben visibile “Segna tutto come letto”. Applicarlo soltanto alle notifiche dell’account selezionato e autenticato, senza cancellarle o modificare quelle degli altri account.
- [ ] Aggiornare badge/conteggi dei non letti e stato delle notifiche dopo il completamento, compresa la campanella nell’intestazione. Mostrare avanzamento/errori, evitare invii duplicati e disabilitare o nascondere l’azione quando non ci sono notifiche non lette.
- [ ] Ridurre i contenitori sovradimensionati e gli spazi verticali eccessivi, usando righe/schede compatte e proporzionate al contenuto. Eliminare altezze fisse/minime non necessarie e padding eccessivo, mantenendo chiari titolo, data, messaggio, azione e distinzione letto/non letto.
- [ ] Conservare adattamento dei testi, larghezza di lettura confortevole, dimensioni accessibili dei comandi, focus visibile e annunci di stato per lettori di schermo. Verificare messaggi lunghi, schermi stretti e testo al 200%.

### Selettore dei fusi orari raggruppato con ricerca — richiesta del 18 novembre 2026

- [ ] Sostituire il campo Fuso orario a testo libero con un elenco a discesa/combobox accessibile, dotato di ricerca e completamento automatico.
- [ ] Raggruppare chiaramente i fusi per area geografica/continente, con UTC separato. Mostrare nomi leggibili e localizzati di città/fusi e lo scarto attuale rispetto a UTC, conservando un identificativo IANA canonico valido come Europe/Rome.
- [ ] Consentire ricerca per città, area, identificativo IANA o scarto visualizzato. Gestire l’ora legale mediante le regole del fuso, senza memorizzare soltanto uno scarto fisso. Conservare la scelta già salvata e validarla sul server.
- [ ] Supportare selezione da tastiera, focus visibile, annunci per lettori di schermo, stato di caricamento/nessun risultato e tutte le lingue pianificate. Applicare lo stesso comportamento dove le preferenze di fuso orario sono disponibili in desktop e web.

### Conferma chiara delle modifiche account in stile UNSC — richiesta del 18 novembre 2026

- [ ] Sostituire il messaggio generico di conferma nativo del browser mostrato nello screenshot con un popup di conferma accessibile e coerente con la grafica UNSC.
- [ ] Usare un titolo specifico, identificare l’account interessato e riepilogare le modifiche effettivamente proposte, con valori precedenti/nuovi quando opportuno e senza esporre segreti. Spiegare le conseguenze pertinenti, come la verifica di un nuovo indirizzo email.
- [ ] Sostituire il generico OK con un’azione esplicita come “Salva modifiche”, mantenendo “Annulla” ben visibile. Tradurre i testi nelle lingue pianificate e usare formulazioni specifiche per conseguenze/azioni distruttive.
- [ ] Mantenere distinta questa conferma dalla verifica di identità/password eventualmente richiesta. Gestire focus iniziale, tastiera, Esc/annullamento, ripristino del focus ed esiti/errori chiari, evitando invii duplicati accidentali.

### Elenco utenti più informativo e gestione account più orizzontale — richiesta del 18 novembre 2026

- [ ] Mostrare più dati utili direttamente nell’elenco utenti amministrativo, comprese quote distinte per archivio e allegati, spazio effettivamente utilizzato quando disponibile, spazio residuo/indicatori di utilizzo e stato Illimitato ben riconoscibile. Non inventare consumi e non dedurli dal solo limite della quota.
- [ ] Mantenere leggibili identità, verifica email, ruolo e stato insieme alle nuove informazioni. Dare priorità alle colonne utili e adattare la visualizzazione agli schermi stretti senza affollare la tabella.
- [ ] Ridisegnare il popup amministrativo “Gestisci account” rendendolo più largo e orizzontale sugli schermi grandi, con distribuzione equilibrata di dati personali, stato, ruolo, quote di spazio e sicurezza.
- [ ] Proporzionare i container al contenuto invece di estendere un modulo breve in un riquadro alto e vuoto. Ridurre scorrimento verticale superfluo e spazi eccessivi; mantenere facili da individuare identità dell’account e azioni principali.
- [ ] Conservare un layout impilato utilizzabile su schermi stretti e con testo al 200%, gestione accessibile di tastiera/focus e tutte le protezioni esistenti per ruolo, quote, conferma dell’identità e operazioni amministrative.

### Spazio, integrità e controllo delle operazioni — approvazione del 18 novembre 2026

- [ ] Analisi dello spazio occupato (desktop e web): individuare archivi e allegati che consumano più spazio, con anteprima esplicita della pulizia prima di eliminare dati.
- [x] Controlli di integrità degli archivi (desktop e web): verificare struttura e coerenza delle copie cifrate e, quando serve decifrare, operare nel client autorizzato senza esporre contenuti in chiaro al server. Mostrare l’ultima verifica completata e non dichiarare recuperabili dati senza una copia effettivamente verificata.
- [x] Modelli riutilizzabili dei permessi di condivisione (desktop e web), per esempio accesso in sola lettura per sette giorni, con conferma del titolare a ogni invito.
- [x] Controlli prima dei trasferimenti (desktop): segnalare spazio insufficiente, nomi incompatibili, destinazioni esistenti e collegamenti simbolici quando verificabili prima dell’avvio.
- [x] Pannello unificato delle operazioni (desktop): trasferimenti, sincronizzazioni ed esportazioni con avanzamento, errori e annullamento quando supportato. Coordinarlo con il centro sincronizzazione previsto, evitando stati duplicati o contraddittori.
- [x] Riepilogo prima dell’uscita (desktop): mostrare sessioni aperte, trasferimenti attivi e modifiche non salvate, con scelta esplicita tra proseguire nella chiusura e tornare al lavoro. Integrarlo con modalità in background e nuova outro senza modificare le garanzie di sicurezza.

### Venti ulteriori funzionalità approvate — approvazione del 18 novembre 2026

1. [x] Home personale configurabile (desktop/web): riepiloghi selezionabili di archivi, attività, ticket e notifiche.
2. [x] Viste e filtri salvati (desktop/web): ricerche riutilizzabili per utenti, ticket e altri elenchi.
3. [x] Ricerca remota dei file (desktop): cartelle autorizzate, filtri per nome/estensione/dimensione/data, limiti e annullamento.
4. [ ] Trasferimenti tra server (desktop): indicare se i dati transitano dal PC e lo spazio temporaneo richiesto.
5. [x] Visualizzatore di log remoti in tempo reale (desktop): ricerca/filtri e pausa dello scorrimento senza riscaricare continuamente l’intero file.
6. [x] Mappa delle connessioni (desktop): visualizzare PC, server intermedi, destinazione, tunnel e porte.
7. [x] Installazione guidata della chiave pubblica (desktop): anteprima delle modifiche SSH autorizzate e verifica del nuovo accesso prima di abbandonare il precedente.
8. [x] Informazioni del server su richiesta (desktop): dati disponibili di sistema/dischi, data del rilevamento e controlli esplicitamente autorizzati.
9. [x] Anteprima protetta degli allegati (desktop/web): formati supportati senza avvio automatico di eseguibili o contenuti attivi.
10. [x] Revisione periodica degli accessi condivisi (desktop/web): promemoria al titolare per verificare i permessi ancora necessari.
11. [x] Rapporti dei permessi esportabili PDF/CSV (desktop/web): utenti, autorizzazioni e scadenze senza segreti.
12. [x] Sospensione delle condivisioni (desktop/web): controllo del titolare, riepilogo e riattivazione controllata; impossibilità di cancellare a distanza copie già scaricate.
13. [x] Commenti sugli elementi condivisi (desktop/web): conversazioni limitate ai membri autorizzati, con adeguate protezioni di accesso/cifratura.
14. [x] Risposte predefinite in inglese per l’assistenza (web): revisione e invio espliciti dell’operatore.
15. [x] Collegamento di ticket correlati/duplicati (desktop/web): raggruppare problemi senza esporre conversazioni private di altri richiedenti.
16. [x] Guide contestuali e primo avvio (desktop/web): percorsi guidati per account, connessioni e condivisioni.
17. [x] Sincronizzazione selettiva delle preferenze (desktop/web): scelta delle impostazioni trasferibili; quelle del dispositivo restano separate.
18. [x] Modalità connessione a consumo (desktop): controllo di trasferimenti automatici e download pesanti, stato delle attività in attesa ed eccezioni esplicite.
19. [x] Risparmio risorse (desktop): ridurre rendering/animazioni delle schede inattive preservando le sessioni operative.
20. [x] Centro dati e privacy (desktop/web): dati locali/del servizio e controlli distinti per cache, diagnostica e dati temporanei.

### Esportazione completa dell’archivio in chiaro — richiesta del 18 novembre 2026

- [ ] Consentire al titolare/utente autorizzato di scaricare l’archivio completo in chiaro, compresi elementi e allegati nei limiti dei suoi diritti di esportazione. Definire un formato portabile documentato con manifesto e conservare i byte originali di chiavi/file dove necessario.
- [ ] Richiedere nuova verifica 2FA e un codice email monouso separato, inviato all’indirizzo verificato dell’account. Vincolare entrambe le prove all’account selezionato e a quella specifica esportazione, con scadenza, limiti ai tentativi e protezione dal riutilizzo.
- [ ] Decifrare/esportare nel client autorizzato, conservando l’attuale separazione crittografica. Avvisare esplicitamente che i file prodotti contengono segreti non cifrati, consentire la scelta della destinazione ed evitare invii automatici/caricamenti o copie temporanee persistenti in chiaro.
- [ ] Definire i diritti di esportazione degli archivi condivisi: un membro estraneo o un amministratore non deve poter esportare dati del titolare per il solo ruolo gestionale. Confermare l’ambito e spiegare che le copie in chiaro già scaricate non sono revocabili.
- [ ] Prevedere avanzamento, annullamento, pulizia in caso di errore e notifica/registrazione dell’evento senza contenuti esportati o codici.

### Cancellazione account con periodo di ripensamento — richiesta del 18 novembre 2026

- [ ] Aggiungere la richiesta di cancellazione completa dell’account: identità, archivi di proprietà, allegati, dispositivi/concessioni di chiavi, sessioni/token, preferenze e altri dati personali associati, con ambito e conseguenze espliciti.
- [ ] Richiedere nuova verifica dell’identità con 2FA e conferma email per la richiesta distruttiva. Prevedere un periodo di ripensamento ragionevole e configurabile; pianificare provvisoriamente 30 giorni, mostrando data e fuso orario esatti della cancellazione. La durata definitiva non è ancora stata scelta dall’utente.
- [ ] Consentire annullamento esplicito durante il periodo dopo verifica dell’identità, con notifiche chiare di richiesta/annullamento. Definire accesso e sincronizzazione mentre la cancellazione è in attesa; il semplice login non deve spostare silenziosamente la scadenza.
- [ ] Prima di accettare la richiesta, gestire esplicitamente la proprietà degli archivi condivisi: trasferimento verificato a un altro titolare oppure conferma chiara della loro cancellazione e delle conseguenze per i collaboratori. Rimuovere adesioni/concessioni dell’account agli archivi altrui senza cancellare questi ultimi.
- [ ] Alla scadenza invalidare sessioni e concessioni, revocare il materiale di sblocco server/dispositivi ed eliminare in sicurezza archivi di proprietà, allegati e altri dati associati nei servizi gestiti e nelle cache locali dell’app raggiungibili. Gestire completamento/tentativi e impedire la ricomparsa dei dati tramite sincronizzazione o ripristino.
- [ ] Definire e comunicare conservazione/eliminazione dei backup ed eventuali registrazioni strettamente necessarie con una base effettiva. Non promettere distruzione istantanea di ogni backup, copia di terzi o dispositivo offline. Le copie/esportazioni già salvate da utenti o collaboratori non possono essere garantite cancellate a distanza.
- [ ] Coordinare la distruzione delle chiavi con copie sui dispositivi, backup e materiale realmente recuperabile; non dichiarare la cancellazione irreversibile se rimane un percorso di recupero gestito utilizzabile. Non includere segreti nei log e mostrare uno stato di completamento veritiero.
- [ ] Proteggere l’ultimo amministratore attivo: prima della cancellazione richiedere una sostituzione/transizione sicura che non lasci il servizio senza amministrazione.

Punti da completare in una prossima versione. Non sono inclusi nel pacchetto 0.2.19 già pubblicato.

Qualifica 18 novembre 2026: vedere IMPLEMENTATION-STATUS.md per ambito, limiti ed evidenze; informazioni del server limitate ai dati SFTP disponibili. Le caselle non qualificano il rilascio completo.
