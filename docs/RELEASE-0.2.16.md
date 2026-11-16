# UNSC Relay 0.2.16

## Accesso integrato
La schermata di identità ora nasconde temporaneamente il browser principale, evitando che la finestra nativa WebView2 la copra. Annullando si torna alla schermata precedente. Un errore di navigazione termina la richiesta con un messaggio, senza lasciarla in attesa indefinita.

## Chiusura prima del login
Il pulsante Esci da Relay usa la normale sequenza di chiusura. L’outro rimuove la schermata di accesso e interrompe la musica dell’intro. Gli aggiornamenti continuano a chiudere senza outro.

## Sito e configurazione OTP
Profilo, Sicurezza e App desktop mantengono pagine distinte senza tab duplicate. Amministrazione apre una nuova scheda. La configurazione manuale OTP offre una stringa otpauth completa e copiabile con segreto, algoritmo, cifre e intervallo effettivi; nessun segreto viene inviato a servizi esterni.

L’aggiornamento conserva dati, profili e archivio. Le verifiche automatiche non includono l’accesso con credenziali personali o la registrazione di un autenticatore reale.
