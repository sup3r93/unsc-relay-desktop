# Release verification 0.2.18

## English

Verified on 17 November 2026 using synthetic data and loopback services. No personal installed app or vault was opened.

| Area | Result |
| --- | --- |
| Normal signed package build | Passed; general 80, cloud migration 30, native certificates 13, security boundaries 37, account transport 19, update/workflow 41 |
| Desktop browser UI | 133 passed; personal soundtrack playback excluded from this silent fixture run |
| Signed packaged app | File/editor workflow 32, native integration 30, account/access 50; all passed |
| Included native tools | Package checks 29 and actual loopback native control 32 passed |
| FTP/S3 | FTP 27, S3 24, untrusted TLS rejection 1; all passed |
| Installer | Signed isolated fresh installation in a chosen spaced path; cumulative reinstall retained a synthetic vault and custom file; excessive path rejected |
| Documentation/community policy | Bilingual README validation and 4 branch-target policy cases passed |
| Desktop npm audit | Zero reported vulnerabilities |
| Website regression | 32 browser cases, 7 crypto cases, API suite, runtime 4, SMTP 6, worker 2 and production build passed |

The update backup now carries the protected account-device capsule. A synthetic vault was actually restored/unlocked from that backup; plaintext keys and server unlock halves remain excluded.

The installer is signed with the existing personal project certificate. Windows does not publicly trust its root; verification accepted only that expected untrusted root, without changing trust settings. Final release packaging is rebuilt from the merged main commit; its digest and signer are checked before publication.

Security scans and protections remain enabled. The remaining contextual CodeQL findings are described in CODEQL-REMAINING-199.md; no additional dismissals were made. The website build-only dependency picomatch 4.0.7 has newly reviewed advisory GHSA-fc5q-39xc-vjpf, with no patched version available at verification time. It is not copied into the Caddy production image. No audit exclusion, forced downgrade or false clean-audit claim was introduced.

Real account, Windows Hello, RDP, hardware/provider and physical interaction acceptance remains with the owner. Ed448/ML-DSA-87 SSH authentication is not implemented by the included backend and is not advertised as supported.

## Italiano

Verificato il 17 novembre 2026 con dati sintetici e servizi locali. Non sono stati aperti l’app installata o gli archivi personali.

| Area | Risultato |
| --- | --- |
| Compilazione normale del pacchetto firmato | Superata; generali 80, migrazione cloud 30, certificati nativi 13, sicurezza 37, trasporto account 19, aggiornamento/workflow 41 |
| Interfaccia desktop nel browser | 133 superati; riproduzione della colonna sonora personale esclusa da questa prova silenziosa |
| App firmata nel pacchetto | File/editor 32, integrazione nativa 30, account/accesso 50; tutti superati |
| Strumenti nativi inclusi | Pacchetto 29 e controllo nativo reale su server locale 32 superati |
| FTP/S3 | FTP 27, S3 24, rifiuto TLS non attendibile 1; tutti superati |
| Installer | Installazione firmata isolata in un percorso scelto con spazi; reinstallazione cumulativa conserva archivio sintetico e file aggiuntivo; percorso eccessivo rifiutato |
| Documentazione/regole community | README bilingui e 4 casi di destinazione delle PR superati |
| Audit npm desktop | Nessuna vulnerabilità segnalata |
| Regressione sito | 32 casi browser, 7 casi crittografici, suite API, runtime 4, SMTP 6, worker 2 e compilazione di produzione superati |

Il backup dell’aggiornamento conserva ora il collegamento protetto del dispositivo. Un archivio sintetico è stato realmente ripristinato/sbloccato dal backup; escluse chiavi in chiaro e metà della chiave custodita dal server.

L’installer usa il certificato personale del progetto. Windows non considera pubblicamente attendibile la sua radice: accettata nella verifica soltanto tale condizione prevista, senza modificare le impostazioni di fiducia. Il pacchetto definitivo viene ricompilato dal commit integrato in main, controllando firma e impronta prima della pubblicazione.

Scansioni e protezioni restano attive. Gli avvisi CodeQL contestuali residui sono descritti in CODEQL-REMAINING-199.md; nessuna nuova chiusura manuale. La dipendenza di sola compilazione del sito picomatch 4.0.7 presenta il nuovo avviso esaminato GHSA-fc5q-39xc-vjpf, senza una versione corretta disponibile al momento della verifica. Non viene copiata nell’immagine Caddy di produzione. Nessuna esclusione dall’audit, retrocessione forzata o falsa dichiarazione di audit pulito.

I collaudi reali di account, Windows Hello, RDP, hardware/provider e interazioni fisiche restano al titolare. Ed448/ML-DSA-87 come firme SSH non sono implementati dal motore incluso e non vengono dichiarati supportati.
