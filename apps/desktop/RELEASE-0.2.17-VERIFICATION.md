# Release verification 0.2.17

## English

- Desktop UI: 133 passing checks. Personal soundtrack decoding is excluded; interaction tests use simulated playback.
- Web UI: 32 passing checks. API, seven encrypted browser/desktop interoperability checks, four runtime checks, worker/SMTP tests and bilingual README checks pass. Both npm dependency audits report zero vulnerabilities.
- Native engine: 32 real OpenSSH control/SFTP checks, including single authentication, verified resume, custom subsystem and no authentication fallback. Included tool package: 29 checks.
- Signed packaged application: 30 native/queue/editor/recovery checks, 50 account-access checks and 25 integrated file/editor checks. FTP/S3/TLS loopback: 52 checks. Synthetic accounts, keys, archives and loopback servers only.
- Managed vault migration, account unlock/transport, host/user certificates, hook lifecycle, bounded key parsing, recovery policy and transfer-window checks pass. Desktop builds have no warnings/errors.
- Isolated fresh installation into a custom spaced path and cumulative reinstall pass, preserving both a custom file and an isolated vault. The production file/cleanup/code sections are used with test-only identity, registry/shortcuts/autorun disabled. Separate legacy cleanup regression also passes. Overlong installation roots are rejected before file copying.
- Native component source archive matches the bundled fetch recipe and package lock. Installer and project-owned Windows executables use the existing personal publisher certificate; Windows public trust is not claimed or installed.

The project owner explicitly performs real account migration/login, physical Windows Hello, RDP/RD Gateway/multimonitor and hardware/provider acceptance separately. These manual checks are not reported as automated results and, by the owner's instruction, do not block this stable release. Ed448/ML-DSA-87 remain unavailable without an interoperable SSH backend.

## Italiano

- Interfaccia desktop: 133 prove passate. Esclusa la decodifica della colonna sonora personale; le prove di interazione simulano la riproduzione.
- Interfaccia web: 32 prove passate. API, sette controlli cifrati browser/desktop, quattro runtime, worker/SMTP e README bilingui superati. Entrambi gli audit npm non segnalano vulnerabilità.
- Motore nativo: 32 controlli OpenSSH/SFTP reali, con accesso unico, ripresa verificata, subsystem personalizzato e assenza di nuovi accessi di ripiego. Pacchetto strumenti: 29 controlli.
- App firmata nel pacchetto: 30 controlli nativi/coda/editor/recupero, 50 accesso account e 25 file/editor integrati. FTP/S3/TLS locali: 52. Soltanto account, chiavi, archivi e server locali sintetici.
- Superati migrazione archivio, sblocco/trasporto account, certificati host/utente, hook, parser chiavi, recupero e fasce orarie. Compilazione desktop senza avvisi/errori.
- Superate prima installazione in un percorso personalizzato con spazi e reinstallazione cumulativa, conservando file personalizzato e archivio isolato. Stesse sezioni di file/pulizia/codice della produzione, con identità di prova e senza registro personale, collegamenti o avvio app. Superata anche la regressione della pulizia delle versioni precedenti. I percorsi eccessivamente lunghi vengono rifiutati prima della copia.
- Sorgenti dei componenti coerenti con ricetta di acquisizione e lock. Installer ed eseguibili del progetto firmati con il certificato personale esistente; nessuna dichiarazione o installazione di fiducia pubblica Windows.

Come concordato, il titolare esegue separatamente login/migrazione reali, Hello fisico, RDP/RD Gateway/multimonitor e dispositivi/provider. Non sono dichiarati risultati automatici e, su istruzione del titolare, non bloccano questa versione stabile. Ed448/ML-DSA-87 restano indisponibili senza backend SSH interoperabile.
