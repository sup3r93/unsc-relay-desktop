# SSH.NET 2026.0.0 - Relay extension

Source: https://github.com/sshnet/SSH.NET/tree/2026.0.0, MIT license in LICENSE. Archive SHA256 in UPSTREAM.json. The subsystem extension in SftpClient.cs and SubsystemSession.cs adds an optional pre-connection SubsystemCommand property and use the existing channel SendExecRequest instead of SendSubsystemRequest when it is set. Every SFTP packet, transfer, host authentication and cryptographic primitive still uses upstream code. No reflection, binary patching or custom protocol parser.

Relay.SshNet.csproj targets only net10, sets PackageId SSH.NET to replace the transitive official runtime consistently, and uses upstream BouncyCastle 2.7.0 / logging 11.0.0 dependencies. AssemblyVersion 2026.0.0.1 and the upstream public repository strong-name key preserve compatibility with Agent/Keygen; a strong name is not a publisher Authenticode signature. RelayVersion.cs identifies the modified build in its SSH identification banner. Keep this patch explicit when updating upstream. No upstream build scripts are executed.


## Private key parsing and diagnostics

Relay limits private-key input to 1 MiB of text before parsing, applies a one-second regex timeout, and removes a redundant nested repetition from the PuTTY data pattern. Temporary character buffers are cleared on success and failure. This prevents malformed key imports from consuming unbounded memory or CPU; it does not change supported algorithms or key encodings. Regression checks cover oversized/malformed input and valid Ed25519, RSA and ECDSA imports.

The SFTP fstat fallback log records only the exception type, excluding the remote path and server-supplied exception text. Other upstream log messages have not been declared audited by this change.

## Authenticated native subsystem streams

`SftpClient.FromSubsystemStreams` accepts ownership of an already authenticated subsystem's input/output streams and close callback. Relay supplies an owned OpenSSH `-O proxy` child on its authenticated master; no new transport, authentication or trust fallback is opened. The existing upstream SFTP parser, requests, file streams and extensions are reused. Reads run separately from request waits, writes are serialized, EOF/errors wake outstanding operations, and closing suppresses late callbacks before disposing wait handles. Conservative 32 KiB packet limits apply to the stream adapter. Each transfer/editor operation owns its channel, not the master.

## Italiano

Estensione locale di SSH.NET 2026.0.0, sorgente e licenza MIT indicati sopra. SftpClient.cs e SubsystemSession.cs aggiungono il comando subsystem facoltativo usando i canali originali; protocolli e primitive crittografiche restano quelli della libreria. La compilazione net10 mantiene la compatibilità con Agent/Keygen. Lo strong name non è una firma Authenticode del produttore.

Il parser delle chiavi limita il testo a 1 MiB, impone un timeout di un secondo alle espressioni regolari e rimuove una ripetizione annidata ridondante nel formato PuTTY. I buffer temporanei vengono cancellati anche in caso di errore. Le prove comprendono input malformati o eccessivi e chiavi valide Ed25519, RSA ed ECDSA.

Il messaggio diagnostico di ripiego fstat SFTP conserva solo il tipo di eccezione, senza percorso remoto o testo ricevuto dal server. Questa modifica non dichiara verificati tutti gli altri messaggi della libreria. Mantenere esplicite queste differenze negli aggiornamenti upstream.

Il collegamento ai flussi del subsystem nativo riusa parser e operazioni SFTP originali. Il processo OpenSSH appartiene alla singola operazione e usa il master già autenticato senza ripiego su nuove connessioni. Scritture serializzate, gestione di fine flusso/errori e chiusura dei canali proteggono le operazioni in attesa; il limite pacchetto è 32 KiB. Il canale non possiede il master.
