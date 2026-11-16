# UNSC Relay Desktop

[English](#english) · [Italiano](#italiano) · [Website](https://unsc.responda.eu)

## English

**The UNSC Relay source code and application were created by artificial intelligence (AI), based on the project owner’s requests and directions.**

Windows desktop client for SSH/SFTP, RDP/VNC, local terminals, tunnels, file transfers, remote editing, macros and workspaces. UNSC-inspired interface. Source baseline: **0.2.16**; this repository reorganization does not publish a new installer.

- `apps/desktop`: .NET solution, TypeScript/JavaScript interface, tests and installer.
- `scripts`: build and release entry points from the repository root.
- `main`: stable source, protected by review and additional checks.
- `dev`: development and automated checks. No beta or prerelease distribution.

The website, API and infrastructure are maintained separately by the owner. Initial app sign-in requires a personal account; website administrator accounts are not allowed. The account/manual-Hello sign-in changes are under development on `dev` and have not been shipped.

Build with `dotnet build apps/desktop/UnscRelay.sln`. For UI tests, run `npm ci` and `npm test` in `apps/desktop` after installing the Playwright browser. [Development guide](apps/desktop/README.md) · [Contributing and branches](CONTRIBUTING.md) · [Security](SECURITY.md).

Installers are signed EXE files. The first installation asks for a folder; upgrades retain it. Personal data stays separate in `%LOCALAPPDATA%\UNSC Relay`. Drive `A:` is not required. Build artifacts are written to `apps/desktop/artifacts/installer`.

Personal audio tracks and videos are excluded from this public repository. [Media assets](apps/desktop/src/UnscRelay.App/Web/media/README.md). Personal keys, tokens, user data and production configuration are not published. No open-source license has yet been selected for the project code; dependencies retain their own licenses.

Halo, UNSC and related trademarks belong to their respective owners. This independent, unofficial project is not affiliated with or endorsed by Microsoft or Halo Studios.

## Italiano

**Il codice e l’applicazione UNSC Relay sono stati realizzati da intelligenza artificiale (IA), sulla base delle richieste e delle indicazioni del titolare del progetto.**

Client desktop Windows per SSH/SFTP, RDP/VNC, terminali locali, tunnel, trasferimenti, editor remoto, macro e workspace. Interfaccia ispirata a UNSC. Baseline del codice: **0.2.16**; questa riorganizzazione non pubblica un nuovo installer.

- `apps/desktop`: soluzione .NET, interfaccia TypeScript/JavaScript, test e installer.
- `scripts`: comandi di build e rilascio dalla radice.
- `main`: codice stabile, protetto da revisione e controlli aggiuntivi.
- `dev`: sviluppo e verifiche automatiche. Nessuna release beta o prerelease.

Sito, API e infrastruttura sono mantenuti separatamente dal titolare. Il primo accesso dell’app richiede un account personale; gli account amministrativi del sito non sono ammessi. Le modifiche al nuovo accesso account/Hello sono in sviluppo su `dev`, non ancora distribuite.

Compilazione: `dotnet build apps/desktop/UnscRelay.sln`. Test UI: `npm ci` e `npm test` da `apps/desktop`, dopo aver installato il browser Playwright. [Guida di sviluppo](apps/desktop/README.md) · [Contributi e branch](CONTRIBUTING.md) · [Sicurezza](SECURITY.md).

Gli installer sono file EXE firmati. Il percorso è scelto al primo setup e mantenuto durante gli aggiornamenti; i dati personali restano separati in `%LOCALAPPDATA%\UNSC Relay`. `A:` non è un requisito. Gli artefatti si creano in `apps/desktop/artifacts/installer`.

Le tracce audio e i filmati personali non sono inclusi nel repository pubblico. [Risorse multimediali](apps/desktop/src/UnscRelay.App/Web/media/README.md). Non sono pubblicati chiavi personali, token, dati utente o configurazioni di produzione. Non è ancora stata scelta una licenza open source per il codice del progetto; le dipendenze conservano le proprie licenze.

Halo, UNSC e i relativi marchi appartengono ai rispettivi titolari. Progetto indipendente, non ufficiale, non affiliato né approvato da Microsoft o Halo Studios.
