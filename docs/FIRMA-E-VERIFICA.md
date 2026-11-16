# Signing and verification / Firma e verifica

## Italiano

Le build personali usano Authenticode e un certificato autofirmato: non è automaticamente attendibile da Windows e non garantisce l’accettazione da parte degli antivirus. La repository contiene solo il certificato pubblico; la chiave privata di firma non è inclusa.

Gli aggiornamenti verificano il digest SHA-256 e il firmatario atteso prima di avviare l’installer. Non modificano le protezioni antivirus. I report di sicurezza e i dati personali restano locali e non vengono pubblicati.

Per compilare i sorgenti usa le istruzioni del README desktop. Per distribuire una build devi fornire il tuo certificato e verificare esplicitamente la politica di attendibilità dell’updater. I controlli automatici del codice non equivalgono al collaudo completo di un installer firmato.

## English

Personal builds use Authenticode with a self-signed certificate. Windows does not trust it automatically, and it does not guarantee antivirus acceptance. Only the public certificate is included; the private signing key is excluded.

Updates check the SHA-256 digest and expected signer before launching the installer. They do not change antivirus protections. Security reports and personal data remain local and are not published.

Follow the desktop README to compile the source. Distribution requires your own certificate and an explicit review of the updater trust policy. Automated source checks do not replace testing a complete signed installer.
