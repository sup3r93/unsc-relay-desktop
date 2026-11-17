# UNSC Relay 0.2.18

## English

- External remote editing opens only the selected program. No internal editor tab is created; saved UTF-8 files upload automatically through the existing hash-conflict guard. A compact status in the SFTP browser reports saves/errors and allows retry or disconnecting the file link.
- Reopening retains the same local copy. Atomic editor saves are detected; unrelated backup-file notifications are ignored. Failed uploads retain the local copy. Closing SFTP no longer depends on an invisible editor tab, and app/update close warnings explain which external copies remain on disk.
- Separate-window arrows are removed from tabs; the context menu keeps detach/reattach. Tab order, rename, splits and workspace restoration remain available.
- Online unlock distinguishes an outdated/unavailable enrollment API, a missing existing device link and temporary server failures. Invalid device IDs and malformed replies fail safely without replacing the encrypted local archive or silently enrolling a replacement device.
- Update recovery backups now include the protected account-device capsule with the encrypted vault, with a real synthetic restore/unlock regression.
- Includes the previously reviewed backup/link and external-executable hardening. CodeQL, queries and branch protections remain enabled; contextual alerts are documented, not bulk-dismissed.

Updates remain cumulative and preserve application data. Real account/Hello/RDP/hardware/provider acceptance is performed separately by the owner. Ed448/ML-DSA-87 SSH authentication remains unavailable: the included interoperable backend does not advertise these signatures. No simulated algorithms, beta releases or public Windows publisher trust are claimed. The installer uses the existing personal project signing certificate; Windows may show its normal trust prompt.

## Italiano

- L’editor remoto esterno apre soltanto il programma selezionato. Nessuna scheda interna: i salvataggi UTF-8 vengono caricati automaticamente con il controllo hash dei conflitti. Un’indicazione compatta nel browser SFTP segnala salvataggi/errori e permette di riprovare o scollegare il file.
- La riapertura conserva la stessa copia locale. Rilevati anche i salvataggi tramite sostituzione atomica del file; ignorate le notifiche dei file di backup estranei. Gli errori conservano la copia locale. La chiusura SFTP non dipende più da una scheda editor invisibile; gli avvisi di uscita/aggiornamento spiegano quali copie esterne restano sul PC.
- Rimossa la freccia delle finestre separate dalle schede: la funzione resta nel menu contestuale. Conservati ordinamento, rinomina, split e ripristino dei workspace.
- Lo sblocco online distingue servizio di collegamento non aggiornato/indisponibile, dispositivo già collegato ma non trovato e guasti temporanei del server. Dispositivi non validi e risposte malformate vengono rifiutati senza sostituire l’archivio locale cifrato o registrare automaticamente un altro dispositivo.
- I backup preventivi degli aggiornamenti includono ora il collegamento protetto del dispositivo insieme all’archivio cifrato, con prova reale di ripristino/sblocco su dati sintetici.
- Incluse le protezioni già esaminate per backup/collegamenti ed eseguibile dell’editor. CodeQL, query e protezioni dei rami restano attivi; gli avvisi contestuali sono documentati, senza chiusure indiscriminate.

Aggiornamenti cumulativi e conservazione dei dati. Il titolare esegue separatamente i collaudi reali di account/Hello/RDP/hardware/provider. Ed448/ML-DSA-87 come firme SSH restano indisponibili: il motore interoperabile incluso non le offre. Nessun algoritmo simulato, canale beta o dichiarazione di fiducia pubblica Windows. L’installer usa il certificato personale già previsto: Windows può mostrare il normale avviso di fiducia.
