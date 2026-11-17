# CodeQL release triage 0.2.17

## English

The default scan was reviewed without disabling languages, queries or required checks. Four execution/cleanup flows are hardened: the policy test executes a pinned literal after matching the workflow; storage tests allow only known bundled executables and use TemporaryDirectory ownership; the native SFTP helper starts only the fixed bundled SSH executable and rejects an environment mismatch. These alerts are left to close as fixed on reanalysis.

Other new path-flow findings were reviewed as local desktop/test-context false positives, not remote path permissions. No arbitrary remote path is used as an application-data root. Review groups:

- Test/smoke files: synthetic loopback fixtures, newly allocated temporary roots and exact owned cleanup. Tests do not process production accounts or vaults.
- CloudSync: application-owned SyncConflicts root; owner is validated as exactly 64 ASCII hex digits before activation/load; revision is numeric.
- FileWorkspace: transfers journal is a fixed filename under the application data root.
- AccountUnlock, CloudAccount, HostKeys, KeyVault, Models: fixed application-owned state paths; generic file APIs intentionally accept local paths supplied by the desktop owner. Atomic temporary files share the selected destination directory.
- VaultSync: fixed five-file allowlist, leaf-only backup journal names, validated manifest entries and validation before restore writes.
- StorageFiles: local source/destination comes from the native Windows selection dialog; staging is next to that explicit destination. Remote browser paths are normalized and cannot select a local root.
- ExternalCertificates: application-owned GUID scratch directory and fixed public/certificate filenames; configured local provider execution is explicit functionality.
- ExplorerExport: canonical root-prefix validation, Windows filename restrictions, traversal/duplicate checks, depth/size limits and reparse-point guards.
- RecordingRetention: app recording directory, GUID filenames, magic header, age and reparse-point checks before deletion.

Remote alert-state changes require the owner’s explicit approval; this commit does not perform broad dismissals.

Reviewed alert locations (GitHub IDs; source line numbers refer to the initial release scan):

- apps/desktop/src/UnscRelay.App/CloudSync.cs: 21@38, 20@36
- apps/desktop/src/UnscRelay.App/FileWorkspace.cs: 24@40, 23@37
- apps/desktop/src/UnscRelay.App/NativeIntegrationSmoke.cs: 241@83, 240@67
- apps/desktop/src/UnscRelay.Core/AccountUnlock.cs: 242@17
- apps/desktop/src/UnscRelay.Core/CloudAccount.cs: 197@38
- apps/desktop/src/UnscRelay.Core/ExplorerExport.cs: 32@27, 31@27
- apps/desktop/src/UnscRelay.Core/ExternalCertificates.cs: 40@43, 37@45, 36@45, 35@45, 34@38, 33@36
- apps/desktop/src/UnscRelay.Core/HostKeys.cs: 44@11, 38@11
- apps/desktop/src/UnscRelay.Core/KeyVault.cs: 48@33, 47@22
- apps/desktop/src/UnscRelay.Core/Models.cs: 203@156, 54@137, 53@159, 52@159, 51@158, 50@137
- apps/desktop/src/UnscRelay.Core/RecordingRetention.cs: 66@8
- apps/desktop/src/UnscRelay.Core/StorageFiles.cs: 255@113, 254@111, 250@113, 249@113
- apps/desktop/src/UnscRelay.Core/VaultSync.cs: 217@186, 214@187, 213@183, 212@176, 211@171, 90@188, 89@180, 88@179, 87@169
- apps/desktop/tests/UnscRelay.Tests/AccountTransportChecks.cs: 248@36, 247@52, 246@39, 245@33, 244@22
- apps/desktop/tests/UnscRelay.Tests/AccountUnlockChecks.cs: 256@61, 253@15, 252@14, 251@9, 243@35
- apps/desktop/tests/UnscRelay.Tests/KeyParsingChecks.cs: 258@19, 257@17
- apps/desktop/tests/UnscRelay.Tests/NativeControlChecks.cs: 282@83, 281@71, 271@93, 270@72, 269@68, 268@64, 267@49, 266@47, 265@46, 264@42, 263@39, 262@39, 261@36, 260@17, 259@9
- apps/desktop/tests/UnscRelay.Tests/NativeHookIntegrationChecks.cs: 280@43, 279@38, 278@38, 277@38, 276@32, 275@25, 274@9
- apps/desktop/tests/UnscRelay.Tests/NativeHostCertificateChecks.cs: 273@58, 272@10

## Italiano

Scansione predefinita esaminata senza disabilitare linguaggi, query o controlli richiesti. Corretti quattro flussi: codice letterale fissato nel test della policy, soli eseguibili inclusi nelle prove storage, directory temporanee possedute dal relativo oggetto standard e percorso SSH incluso fisso nel trasporto SFTP. Queste segnalazioni sono lasciate chiudere come corrette dalla successiva analisi.

Le altre segnalazioni di percorso elencate sopra riguardano file locali scelti dal titolare o dati sintetici, non permessi su percorsi remoti. Verificati radici applicative, proprietario account esadecimale, allowlist del ripristino, nomi fissi/GUID, confinamento export e protezioni della conservazione. Le chiusure motivate come falsi positivi o uso nei test riguardano soltanto questi ID esaminati; non costituiscono una dichiarazione che tutti gli avvisi storici della repository siano stati risolti.
