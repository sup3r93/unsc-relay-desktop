# Desktop development / Sviluppo desktop

## English

**The UNSC Relay source code and application were created by artificial intelligence (AI), based on the project owner’s requests and directions.**

Windows WPF/WebView2 application with an SSH/SFTP engine and MCP bridge. The website and services live in a separate repository. Paths below are relative to `apps/desktop`.

1. Install .NET SDK 10 and Node.js; run `npm ci`.
2. Build with `dotnet build UnscRelay.sln`.
3. After building the test project, run managed checks with `dotnet tests/UnscRelay.Tests/bin/Debug/net10.0/UnscRelay.Tests.dll --cloud-sync-only`.
4. For UI checks, run `npx playwright install chromium`, then `npm test`.
5. To create a personal installer, use `scripts/build.ps1 -Version <version> -Thumbprint <certificate>`. Local media, verified native components and Inno Setup are required. Never publish signing keys or credentials.

DLLs and tools are organized under `Librerie`. Keep caches, downloaded tools, artifacts and test data out of Git. Tests must not use personal vaults or credentials. Do not run unsigned apphosts on the owner's PC; run managed tests through `dotnet`.

### Next release

Additional managed checks: `--update-policy`, `--personal-archive-recovery`, `--account-workspaces`, `--archive-sharing`, `--archive-client`. The recovery fixture injects errors after remote creation and after local key storage; retries never replace an existing different archive. The native `--access-smoke-test` uses an isolated `.smoke-data` directory, and verifies critical-update RPC, process and hook suppression. Never run acceptance tests against a user’s installed copy or personal data. Release metadata is generated with `scripts/write-update-policy.ps1` and published alongside the installer; `release.ps1` accepts Categories, SecurityLevel and MinimumSupportedVersion. Use `critical` only with an explicit supported minimum; release notes alone cannot lock the app.

## Italiano

**Il codice e l’applicazione UNSC Relay sono stati realizzati da intelligenza artificiale (IA), sulla base delle richieste e delle indicazioni del titolare del progetto.**

Applicazione Windows WPF/WebView2 con motore SSH/SFTP e bridge MCP. Il sito e i servizi sono in una repository distinta. I percorsi di questo documento sono relativi a `apps/desktop`.

1. Installa .NET SDK 10 e Node.js; esegui `npm ci`.
2. Compila con `dotnet build UnscRelay.sln`.
3. Esegui i controlli gestiti con `dotnet tests/UnscRelay.Tests/bin/Debug/net10.0/UnscRelay.Tests.dll --cloud-sync-only` dopo la build dei test.
4. Per l’interfaccia: `npx playwright install chromium`, quindi `npm test`.
5. Per creare un installer personale usa `scripts/build.ps1 -Version <versione> -Thumbprint <certificato>`. Richiede le risorse multimediali locali, i componenti nativi verificati e Inno Setup. Non pubblicare chiavi di firma o credenziali.

DLL e strumenti vengono organizzati in `Librerie`. Cache, strumenti scaricati, artefatti e dati dei test non devono entrare in Git. I test non devono usare archivi o credenziali personali. Non avviare apphost non firmati sul PC del titolare; i test gestiti si eseguono tramite `dotnet`.

### Prossima versione

Controlli aggiuntivi del motore: `--update-policy`, `--personal-archive-recovery`, `--account-workspaces`, `--archive-sharing`, `--archive-client`. La fixture di recupero simula errori dopo la creazione remota e dopo il salvataggio locale della chiave; i tentativi successivi non sostituiscono archivi esistenti diversi. Il test nativo `--access-smoke-test` usa una directory `.smoke-data` isolata e verifica il blocco di RPC, processi e hook durante gli aggiornamenti critici. Non eseguire i collaudi sulla copia installata o sui dati personali. I metadati vengono generati da `scripts/write-update-policy.ps1` e pubblicati con l’installer; `release.ps1` accetta Categories, SecurityLevel e MinimumSupportedVersion. Usare `critical` solo con un minimo supportato esplicito; le sole note di rilascio non possono bloccare l’app.
