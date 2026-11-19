# UNSC Relay 0.2.20

## English

This release brings account selection and isolated workspaces to the desktop and website. Personal account details and registered security methods can be managed in the app, with a fresh operation-specific identity check for sensitive changes. Human administrators can access only their own or explicitly shared archives; management stays website-only and requires the stronger administrator factors.

- Italian, English, French, German and Spanish interfaces, including authentication and recovery guidance.
- Multiple encrypted archives, one owner, read/write permissions, expiry, revocation, verified ownership transfer, access requests and periodic reviews.
- Single-use 64-character recovery codes with local PDF export. Plaintext export requires fresh 2FA and a separate verified-email code. Account erasure has a displayed 30-day reconsideration period and last-administrator safeguards.
- Storage exploration and explicit attachment deletion, limited to the selected account as both owner and author. Quota remains occupied until all stored versions are verifiably destroyed. Existing downloaded copies are outside remote control; backups follow the disclosed retention policy.
- Native SFTP queues, external-editor save synchronization without opening the internal editor, remote search and live logs, connection templates, public-key installation, notes/checklists and a unified operations view.
- Account avatar menu, private support tickets, compact notifications, feature/connection category tabs, searchable time zones and accessible confirmations. Presentation, resource and metered-connection controls are available.
- Optional tray/background operation, update checks and signed update categories/security levels; future critical policies can require an update. Windows Hello remains an explicit, per-device choice for offline access.
- Full-window installer branding and a revised closing sequence. Keys, tokens, decrypted vault contents and personal production configuration are not included in the public source.

Verification: 91 web UI checks; the 197-test desktop regression plus focused new account/storage checks; 33 web unit checks; database/HTTP isolation and concurrency checks (including 50 erasure-worker cases); real disposable Keycloak OTP/passkey/token and binding checks; real OpenBao version-destruction and network-isolation checks; eight Linux permission/atomicity cases. The final packaged app passes 93 access and 46 file checks. Complete local C#/JavaScript CodeQL scans remain enabled; known contextual vendor/configuration findings are documented, not dismissed. Dependency audits report no known findings for the audited packages.

Installers use the existing personal development signing identity. Windows may not trust that certificate root; no system trust setting is changed. Real personal-account, Hello, RDP and hardware qualification remains with the owner. Automated accessibility checks do not certify complete legal or WCAG conformance.

## Italiano

La versione introduce selezione degli account e spazi separati su app e sito. Dati personali e metodi di sicurezza registrati si gestiscono nell’app; le modifiche sensibili richiedono una nuova verifica legata all’operazione. Gli amministratori possono usare soltanto i propri archivi o quelli condivisi esplicitamente, con i fattori forti previsti. La gestione del servizio resta solo sul sito.

- Interfacce in italiano, inglese, francese, tedesco e spagnolo, comprese autenticazione e istruzioni di recupero.
- Più archivi cifrati, un solo titolare, permessi di lettura/scrittura, scadenze, revoche, trasferimento verificato, richieste di accesso e revisioni periodiche.
- Codici di recupero monouso da 64 caratteri con PDF locale. Esportazione in chiaro con nuova 2FA e codice email separato. Cancellazione dell’account con 30 giorni di ripensamento mostrati e protezione dell’ultimo amministratore.
- Esplorazione dello spazio e cancellazione esplicita degli allegati di cui l’account selezionato è proprietario e autore. La quota si libera solo dopo la distruzione verificata di tutte le versioni. Le copie già scaricate non sono eliminabili a distanza; i backup seguono la conservazione dichiarata.
- Code SFTP native, sincronizzazione dei salvataggi dell’editor esterno senza aprire quello interno, ricerca e log remoti, modelli di connessione, installazione delle chiavi pubbliche, note/checklist e centro operazioni.
- Menu account, assistenza privata, notifiche compatte, categorie per funzionalità e connessioni, fusi orari ricercabili e conferme accessibili. Controlli di presentazione, risorse e connessione a consumo.
- Permanenza facoltativa in background, ricerca aggiornamenti e categorie/livelli di sicurezza firmati; gli aggiornamenti critici futuri possono diventare obbligatori. Windows Hello offline richiede sempre l’abilitazione manuale sul dispositivo.
- Grafica dell’intera finestra dell’installer e nuova sequenza di chiusura. Nessuna chiave, token, contenuto decifrato o configurazione personale di produzione nel codice pubblico.

Verifiche: 91 test dell’interfaccia web; regressione desktop di 197 test e controlli mirati successivi su account e spazio; 33 test unitari web; integrazione database/API, isolamento e concorrenza, con 50 casi del processo di cancellazione; prove reali isolate di Keycloak con OTP e passkey; distruzione delle versioni e isolamento di rete OpenBao; otto test Linux su permessi e scritture atomiche. L’app del pacchetto supera 93 controlli di accesso e 46 sui file. Scansioni CodeQL complete mantenute attive, con risultati contestuali documentati e non chiusi. Nessuna vulnerabilità nota negli audit delle dipendenze esaminati.

La firma usa il certificato personale già in uso, che Windows può non considerare attendibile; nessuna modifica all’attendibilità del sistema. I collaudi reali di account personali, Hello, RDP e hardware restano al titolare. I test automatici di accessibilità non certificano la piena conformità normativa o WCAG.
