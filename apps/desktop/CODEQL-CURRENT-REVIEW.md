# Current CodeQL review

## English

GitHub snapshot retrieved on 18 November 2026: **163 open alerts** (154 path injection, 5 command-line injection, 4 ECB). This is the published-code snapshot, not a new scan of the uncommitted work. No new alert was dismissed and no scanner/query/protection was disabled.

The five command-line paths were reviewed with the authorized Daybreak chat: ExternalEditorFile starts an explicitly selected existing absolute EXE with UseShellExecute=false and one ArgumentList file argument; LocalShell lists WSL using constant arguments; the other three paths are isolated test harnesses. Execution of a program explicitly selected by the user is intentional and is not equivalent to interpolation into a command shell.

The four vendor ECB findings concern SSH.NET block primitives or a generic public implementation. CTR and chaining-mode NIST vectors and the actual offered SSH cipher dictionary are tested. TripleDesCipher.BlockImpl is excluded from the net10 target by its conditional compilation. No ECB cipher is advertised by SSH negotiation. These findings remain open for traceable review rather than being hidden.

A separate real issue was found: the integrated SSH.NET transport still offered 3des-cbc. The local change now applies an explicit AES/ChaCha20 cipher policy to SSH, SFTP and silently recreated recovery channels. Three tests inspect the actual instantiated transport configurations; all reject 3DES and retain modern alternatives. The bundled OpenSSH client's independent policy was not weakened. Legacy servers offering only retired TDEA must be upgraded. Basis: [NIST TDEA withdrawal](https://csrc.nist.gov/News/2023/nist-to-withdraw-sp-800-67-rev-2).

The remaining 154 path findings include 78 test paths, 44 application smoke-test paths and 32 runtime paths. Runtime review covers user-selected local files, generated KRL directories, recording paths, explicit OpenSSH configuration edits and agent sockets. This classification is not an authorization to dismiss them. ProfileStore has additionally been migrated to LocalDataFiles for bounded, validated file reads/writes/imports: linked files/parents and files larger than 32 MiB are rejected before parsing. The 27 local-file checks and 46 security-review checks pass locally. Complete remaining path review and post-commit rescanning are still required.

## Italiano

Il 18 novembre 2026 GitHub riportava **163 avvisi aperti**: 154 sui percorsi, 5 sulle righe di comando e 4 sulle primitive ECB. Il numero riguarda il codice pubblicato, non le modifiche locali. Nessun nuovo avviso è stato chiuso e nessun controllo è stato disattivato.

L’analisi con Daybreak ha distinto l’avvio intenzionale di programmi scelti dall’utente, con argomenti separati, dall’iniezione in una shell. Le primitive ECB del vendor sono usate per costruire altre modalità o non sono incluse nel target; la negoziazione SSH non offre ECB. Gli avvisi restano aperti e documentati.

È stato corretto un problema distinto: SSH, SFTP e i canali ricreati dopo un’interruzione non offrono più 3DES. I test controllano i trasporti realmente istanziati e conservano AES e ChaCha20. Un server che supporta esclusivamente TDEA richiede un aggiornamento.

Il caricamento, salvataggio e import dei profili usa ora percorsi locali verificati e un limite di 32 MiB, con rifiuto dei collegamenti. Passano 27 controlli sui file e 46 controlli di sicurezza. Restano da completare l’esame degli altri percorsi e la nuova scansione del codice finale.

### Additional runtime review and fixed SFTP issue

English: Daybreak reviewed the32 runtime path traces in read-only mode. The actionable finding was the FolderSync/SFTP source-open race (published alert46): the compared local file could be replaced before upload, and the old implementation checked its expected source hash only after writing a remote partial. The current code opens one non-replaceable, final-path-verified local handle, checks the source hash on that handle before any remote partial write, and transfers from that same handle. File size and timestamp are obtained from the opened object. Forty-five real loopback SFTP checks pass after explicitly rebuilding the test binary, including five new replacement, no-remote-partial and concurrent-write/rename cases. Daybreak reread and confirmed the corrected path. Other examined runtime paths were explicitly selected files or generated bounded names; no additional concrete traversal from remote input was found in that review. No alerts were dismissed. A fresh CodeQL scan of the final code remains required.

Italiano: La revisione in sola lettura dei32 percorsi di esecuzione ha individuato un problema concreto nel caricamento SFTP: un file sostituito dopo il confronto poteva inviare byte non selezionati al parziale remoto. Ora apertura verificata, impronta e trasferimento usano lo stesso handle, senza scrivere il parziale se l’impronta non coincide. Sono passati45 controlli reali locali, compresi cinque nuovi casi. Gli avvisi restano tracciabili; nessuna chiusura aggiuntiva o disattivazione dei controlli. Serve ancora la nuova scansione del codice finale.

### Latest remote state checked during implementation — 18 November 2026

English: The fresh GitHub REST snapshot still reports 163 open desktop alerts: 154 path, 5 command-line and 4 ECB findings. There are no open desktop PRs. Web Dependabot PR #3 proposes .NET 10→11 SDK/runtime, Node 24→26 and Python 3.13→3.15 together; it remains unmerged because the .NET 10 application is not qualified on the proposed .NET 11 runtime and the other major-runtime changes need their own qualification. No approval rules or scanner settings were changed. The private web code-scanning endpoint returns HTTP 403 with “Code scanning is not enabled”; this is not proof of zero findings. No paid security feature was activated.

Italiano: La nuova lettura GitHub conferma 163 avvisi desktop aperti: 154 percorsi, 5 righe di comando e 4 ECB. Nessuna PR desktop aperta. La PR Dependabot web #3 propone insieme .NET 11, Node 26 e Python 3.15; resta da qualificare e non viene unita automaticamente. L’API di scansione della repository web privata risponde 403 e indica scansione non abilitata: non significa assenza di problemi. Nessun controllo modificato e nessuna funzione a pagamento attivata.

### Final local scans — 18 November 2026

English: Full security-and-quality C# analysis covers 568 files. Its three security-tagged findings are vendor AES block primitives, not advertised ECB transport. A complete JavaScript/TypeScript scan, with compact files explicitly included, covers 69/69 modules: zero security-tagged results and 249 quality results. The earlier 35-file extraction was incomplete and is superseded. Two missing-origin checks were fixed in the pinned xterm/search scheduler handlers, with same-window/same-origin browser tests and a deterministic vendor regeneration check. The worker checks both origin and bounded UTF-8 input. No alert was dismissed and no query or scanner was disabled. Local results do not replace the published GitHub alert state until a new committed scan completes.

Italiano: L’analisi completa C# comprende 568 file e tre risultati sulle primitive AES del fornitore. La scansione JavaScript/TypeScript inclusiva comprende tutti i 69 moduli: nessun risultato di sicurezza e 249 suggerimenti di qualità. Sostituisce l’estrazione precedente incompleta di 35 file. Corretti i due controlli dell’origine nei componenti del terminale, con test browser e rigenerazione verificabile; il worker controlla origine e dimensione UTF-8. Nessuna chiusura di avvisi o disattivazione. Questi risultati locali non sostituiscono lo stato pubblicato su GitHub prima della scansione del nuovo commit.


### GitHub PR #23 temporary-path follow-up — 19 November 2026

The required GitHub scan reported 32 path-flow results from three inputs: the two opt-in smoke fixture environment reads (26 flows) and the native agent temporary-directory value (six flows). Smoke fixture access now reconstructs only an existing, unlinked direct `relay-ssh-` child of the checked temporary directory, rejects lexical traversal and rejects outside/similar-prefix roots. Native agent socket paths use the same canonical child boundary as application metadata. Stale socket cleanup keeps its failure isolation so a rejected or busy socket cannot interrupt vault locking. The security regression includes existing-target traversal and foreign-root cases. These results have not been dismissed; the updated required scan must pass before publication.

Italiano: La scansione obbligatoria della PR #23 ha segnalato 32 percorsi da tre origini: due variabili delle fixture di collaudo (26 flussi) e la cartella temporanea del socket nativo (sei). Le fixture sono ora limitate a una sottocartella diretta esistente `relay-ssh-`, senza collegamenti né attraversamenti lessicali; percorsi esterni e prefissi simili vengono rifiutati. Il socket usa il perimetro canonico dei file locali. Un socket occupato o rifiutato non interrompe il blocco dell’archivio. Aggiunte prove con attraversamento verso una cartella esistente e cartelle estranee. Nessun avviso chiuso: la nuova scansione deve passare prima della pubblicazione.
