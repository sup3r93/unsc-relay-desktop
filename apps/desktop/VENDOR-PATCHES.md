# Terminal bundle patches

## English

The shipped xterm 6.0.0 and search addon 0.16.0 bundles contain an internal asynchronous scheduler copied from the VS Code platform code. Its window-message handler originally matched only a numeric queue identifier. Relay adds a check that the message source is the same window and its origin matches that window before inspecting the payload. This prevents another window from advancing queued terminal callbacks. It does not add access to the native bridge and is not described as arbitrary code execution.

Run `node apps/desktop/scripts/sync-terminal-vendor.cjs` after installing the exact locked dependencies. The script preserves the upstream bundle and license text, requires exactly one expected scheduler per bundle and fails if the implementation changes. `--check` verifies the shipped files without writing. Dependency changes require explicit review of this patch; it must not silently disappear on an upgrade. No CodeQL query or security check is disabled.

## Italiano

I pacchetti distribuiti xterm 6.0.0 e addon di ricerca 0.16.0 includono un sistema interno di pianificazione asincrona derivato dalla piattaforma VS Code. Il gestore dei messaggi verificava soltanto un identificativo numerico della coda. Relay verifica ora che il messaggio provenga dalla stessa finestra e dalla sua stessa origine prima di leggerne il contenuto. Un’altra finestra non può così anticipare le callback del terminale. La modifica non concede accesso al bridge nativo e non viene descritta come esecuzione arbitraria di codice.

Dopo l’installazione delle dipendenze bloccate, eseguire `node apps/desktop/scripts/sync-terminal-vendor.cjs`. Lo script conserva il pacchetto originale e i testi di licenza, richiede una sola corrispondenza prevista per pacchetto e si interrompe se cambia l’implementazione. Con `--check` verifica senza scrivere. Ogni aggiornamento richiede una revisione esplicita di questa correzione. Nessuna query o verifica di sicurezza viene disattivata.
