using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    CloudAccountClient cloud = null!;
    readonly SemaphoreSlim cloudGate=new(1,1);
    readonly CancellationTokenSource cloudLifetime=new();
    readonly DispatcherTimer cloudTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    bool cloudConflict, cloudUiBusy, cloudRunning, accountLoggingOut, cloudPersonalRecovery;
    DateTimeOffset? cloudLastVerified;
    string cloudMessage="Collega l’account UNSC per attivare questo dispositivo.";
    bool AccountLocked=>accountLoggingOut||(!smokeTest&&!cloud.State.Activated);
    void InitializeCloud()
    {
        cloud=new(dataPath){UiLanguage=InterfaceLocale.Normalize(data.Settings.Language)};
        cloudMessage=cloud.State.Activated?"Copia locale disponibile. La sincronizzazione riprende dopo lo sblocco.":cloudMessage;
        cloudTimer.Tick+=async(_,_)=>{if(!CriticalUpdateRequired&&!smokeTest&&consoleReady&&!closing&&!updating&&!cloudUiBusy&&!PlainExportBusy&&!AccountErasureBusy&&activeOperations==0)await SynchronizeCloud(automatic:true);};
        if(!smokeTest)cloudTimer.Start();
    }
    object CloudStatus()=>new {requiresLink=AccountLocked,linked=cloud.State.Activated,name=cloud.State.Name,email=CloudAccountClient.RememberedEmail(cloud.State),
        owner=cloud.State.Owner,ready=cloud.State.Migrated&&cloud.State.Header!=null,revision=cloud.State.Revision,online=cloudLastVerified>DateTimeOffset.UtcNow.AddSeconds(-90),lastVerified=cloudLastVerified,lastSuccessfulSync=cloud.State.LastSuccessfulSync,conflict=cloudConflict,personalRecovery=cloudPersonalRecovery,meteredConnection=data.Settings.MeteredConnection,meteredExternalPending=editBuffers.Values.Count(edit=>edit.ExternalOnly&&edit.Dirty),busy=cloudRunning,message=cloudMessage};
    void CloudNotify(string message){cloudMessage=message;Emit("account",CloudStatus());}
    static SyncEnvelope HeaderOnly(SyncEnvelope value)=>value with{Iv=Convert.ToBase64String(new byte[12]),Ciphertext=Convert.ToBase64String(new byte[16])};
    void Checkpoint(long revision,string hash,SyncEnvelope envelope)
    {
        cloud.State.LastSuccessfulSync=DateTimeOffset.UtcNow;cloud.State.Revision=revision;cloud.State.Baseline=hash;cloud.State.Header=HeaderOnly(envelope);cloud.State.Migrated=true;cloud.Save();cloudConflict=false;
    }
    void PreserveRemote(RemoteVault remote)
    {
        var directory=Path.Combine(dataPath,"SyncConflicts");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,cloud.SyncOwner+"-r"+remote.Revision+".json");
        if(!File.Exists(path))AtomicFile.Write(path,JsonSerializer.SerializeToUtf8Bytes(remote,VaultSyncCrypto.Json));
    }
    void ApplyCloud(byte[] bytes,RemoteVault remote)
    {
        var incoming=VaultSyncCrypto.Parse(bytes);
        SyncLocalBackup.Create(dataPath,true);
        try {
            vault.ImportSyncRecords(incoming.Records);
            incoming.Data.IntegrityObservations=data.IntegrityObservations;incoming.Data.Settings=incoming.Data.PortablePreferences.Apply(data.Settings);incoming.Data.FileLocations=data.FileLocations;
            store.Save(incoming.Data);
            AtomicFile.Write(Path.Combine(dataPath,"hostkeys.json"),JsonSerializer.SerializeToUtf8Bytes(incoming.HostKeys));
            data=incoming.Data;nativeLanguage=InterfaceLocale.Normalize(data.Settings.Language);cloud.UiLanguage=nativeLanguage;
            var captured=VaultSyncCrypto.Capture(vault,data,hosts.List());
            try{Checkpoint(remote.Revision,VaultSyncCrypto.Hash(bytes),remote.Envelope);}finally{CryptographicOperations.ZeroMemory(captured);}
            SyncLocalBackup.Complete(dataPath);
        }catch {
            // Keep the journal until all four files have been restored successfully.
            LockVault();SyncLocalBackup.Recover(dataPath);data=store.Load();
            cloud.Dispose();cloud=new(dataPath){UiLanguage=InterfaceLocale.Normalize(data.Settings.Language)};AttachErasureNotification();throw;
        }
        Emit("data",Snapshot());
    }
    async Task SynchronizeCloud(string resolution="",bool entered=false,bool automatic=false)
    {
        if(AccountErasureLocked||PlainExportBusy||AccountErasureBusy)return;
        if(!entered&&!await cloudGate.WaitAsync(0))return;
        cloudRunning=true;
        byte[]? key=null,local=null,remotePlain=null;
        try {
            if(!cloud.State.Activated)return;
            await cloud.VerifyOnline(cloudLifetime.Token);if(AccountErasureLocked)return;cloudLastVerified=DateTimeOffset.UtcNow;
            if(automatic&&data.Settings.MeteredConnection){CloudNotify(NativeText("Connessione a consumo: sincronizzazione automatica in pausa. Usa Sincronizza adesso per procedere esplicitamente."));return;}
            if(!vault.IsUnlocked){CloudNotify("Archivio locale bloccato. Le modifiche saranno sincronizzate dopo lo sblocco.");return;}
            if(!cloud.State.Migrated||cloud.State.Header==null){CloudNotify("Completa la migrazione dell’archivio nelle impostazioni Account e sincronizzazione.");return;}
            key=vault.SyncKey(cloud.SyncOwner);
            if(key==null){CloudNotify("Sblocca l’archivio online con la sua password per collegare questo archivio locale.");return;}
            local=VaultSyncCrypto.Capture(vault,data,hosts.List());var localHash=VaultSyncCrypto.Hash(local);
            var remote=await cloud.Read(cloudLifetime.Token);if(AccountErasureLocked)throw new TaskCanceledException("Account bloccato durante la verifica.");
            if(remote==null){cloudPersonalRecovery=true;throw new OnlineAccessException("L’archivio personale online è vuoto o è stato isolato per sicurezza. La copia locale è conservata. Apri Account e sincronizzazione → Ripristina sincronizzazione dalla copia locale verificata.");}
            if(remote.Revision<cloud.State.Revision)throw new OnlineAccessException("La revisione online è precedente all’ultima verificata. Sincronizzazione sospesa per proteggere i dati.");
            var decrypted=await Task.Run(()=>VaultSyncCrypto.Open(remote.Envelope,cloud.SyncOwner,key));
            try{remotePlain=VaultSyncCrypto.Canonicalize(decrypted);}finally{CryptographicOperations.ZeroMemory(decrypted);}
            // User edits made while the network request was pending must never be overwritten.
            var current=VaultSyncCrypto.Capture(vault,data,hosts.List());
            try{if(VaultSyncCrypto.Hash(current)!=localHash){CloudNotify("Modifiche locali in corso: sincronizzazione rinviata al prossimo controllo.");return;}}finally{CryptographicOperations.ZeroMemory(current);}
            var remoteHash=VaultSyncCrypto.Hash(remotePlain);
            var decision=VaultSyncPolicy.Decide(cloud.State.Baseline,localHash,remoteHash);
            if(resolution=="local")decision=SyncDecision.Push;
            if(resolution=="online")decision=SyncDecision.Pull;
            if(decision==SyncDecision.Conflict){
                PreserveRemote(remote);cloudConflict=true;
                CloudNotify("Ci sono modifiche sia su questo PC sia online. Scegli quale copia usare: nessuna verrà sovrascritta senza conferma.");return;
            }
            if(decision==SyncDecision.Unchanged){Checkpoint(remote.Revision,localHash,remote.Envelope);CloudNotify("Sincronizzato · "+DateTime.Now.ToString("HH:mm"));return;}
            if(decision==SyncDecision.Pull){
                if(cloudUiBusy&&resolution.Length==0){CloudNotify("Aggiornamento online pronto: sarà applicato quando chiudi la finestra di modifica.");return;}
                ApplyCloud(remotePlain,remote);CloudNotify("Aggiornamento online ricevuto · "+DateTime.Now.ToString("HH:mm"));return;
            }
            // Always use the header and CAS revision just read, preserving concurrent writes.
            PreserveRemote(remote);cloud.State.Revision=remote.Revision;
            var encrypted=await Task.Run(()=>VaultSyncCrypto.Seal(remote.Envelope,key,local));
            var next=await cloud.Write(encrypted,cloudLifetime.Token);
            if(next==null){cloudConflict=true;CloudNotify("L’archivio è cambiato durante il salvataggio. Nessuna copia è stata sovrascritta. Riprova.");return;}
            Checkpoint(next.Value,localHash,encrypted);CloudNotify("Modifiche salvate online · "+DateTime.Now.ToString("HH:mm"));
        }catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or OnlineAccessException or IOException or CryptographicException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException){
            if(ex is PersonalArchiveChangedException)cloudPersonalRecovery=true;
            CloudNotify(ex is OnlineAccessException?ex.Message:ex is CryptographicException?"La chiave online è cambiata: apri le impostazioni e inserisci la password dell’archivio online. La copia locale è conservata.":"Non riesco a sincronizzare adesso. Puoi lavorare offline: riproverò automaticamente.");
        }finally{
            if(key!=null)CryptographicOperations.ZeroMemory(key);if(local!=null)CryptographicOperations.ZeroMemory(local);if(remotePlain!=null)CryptographicOperations.ZeroMemory(remotePlain);
            cloudRunning=false;Emit("account",CloudStatus());if(!entered)cloudGate.Release();
        }
    }
    async Task<object?> CloudAction(string action,JsonElement p)
    {
        if((PlainExportBusy||AccountErasureBusy)&&action is "accountLogin" or "accountLogout" or "accountSetup" or "accountSync" or "accountProfileSave" or "accountPasswordSave")throw new InvalidOperationException("Chiudi prima la finestra di esportazione o cancellazione.");
        if(action=="accountStatus")return CloudStatus();
        if(action=="accountSyncOverview"){var generation=authenticationGeneration;var page=I(p,"page",0);if(page is <0 or >400)throw new ArgumentException("Pagina non valida.");bool? pending=null;if(vault.IsUnlocked&&cloud.State.Migrated&&cloud.State.Baseline.Length>0){var captured=VaultSyncCrypto.Capture(vault,data,hosts.List());try{pending=VaultSyncCrypto.Hash(captured)!=cloud.State.Baseline;}finally{CryptographicOperations.ZeroMemory(captured);}}var archives=new List<object>();var remembered=vault.IsUnlocked&&cloud.State.Owner.Length==64?vault.RememberedArchives(cloud.State.Owner).OrderBy(value=>value.Name,StringComparer.CurrentCulture).ToArray():[];foreach(var archive in remembered.Skip(page*50).Take(50)){ArchiveCopySummary? summary=null;var invalid=false;try{summary=LocalArchiveCopies.Summary(archive);}catch(Exception error)when(error is IOException or UnauthorizedAccessException or CryptographicException or JsonException or InvalidOperationException){invalid=true;}archives.Add(new{id=archive.Id,name=archive.Name,access=archive.Access,revision=summary?.Revision,pending=summary?.Pending,observedAt=summary?.ObservedAt,copyUpdatedAt=summary?.CopyUpdatedAt,invalid});}RequireOperationSession(generation);return new{account=CloudStatus(),pendingLocalChanges=pending,archives,page,hasMore=(page+1)*50<remembered.Length,totalArchives=remembered.Length};}
        if(action=="accountAvailability")return new{registrationAllowed=await cloud.RegistrationAllowed(cloudLifetime.Token)};
        if(action=="accountUiBusy"){cloudUiBusy=p.GetProperty("busy").GetBoolean();return null;}
        if(action=="accountSite"){Process.Start(new ProcessStartInfo("https://unsc.responda.eu/console"){UseShellExecute=true});return null;}
        if(!await cloudGate.WaitAsync(0))throw new InvalidOperationException("Attendi il completamento della sincronizzazione.");
        try {
            if(action.StartsWith("archive",StringComparison.Ordinal))return await ArchiveAction(action,p);
            if(action=="accountLogin"||action=="accountRegister"||action=="accountSecurity"){
                if(startupSequencePlaying)throw new InvalidOperationException("Attendi la sequenza iniziale.");
                var accountAction=action=="accountRegister"?"register":action=="accountSecurity"?S(p,"action"):"login";
                var generation=authenticationGeneration;
                var remembered=new AccountWorkspaces(rootDataPath).Load();var binding=remembered.Accounts.SingleOrDefault(a=>a.Id==remembered.Active);
                if(cloud.State.Owner.Length==0&&binding is {Owner.Length:64})cloud.State.Owner=binding.Owner;
                await cloud.LoginAsync(cloudLifetime.Token,new InAppAccountBrowser(this,dataPath),accountAction);
                if(accountAction is "login" or "register"){await VerifyErasureBeforeUnlock();await UnlockOnlineAfterLogin(generation);}
                new AccountWorkspaces(rootDataPath).Remember(cloud.State);
                cloudLastVerified=DateTimeOffset.UtcNow;CloudNotify("Account verificato per "+cloud.State.Name+". Windows Hello offline è disponibile solo dopo abilitazione manuale.");return AccessBootstrap();
            }
            if(action=="accountMethodRename"){await cloud.RenameSecurityMethod(S(p,"id"),S(p,"name"),new InAppAccountBrowser(this,dataPath),cloudLifetime.Token);return true;}
            if(action=="accountMethodRemove"){await cloud.RemoveSecurityMethod(S(p,"id"),new InAppAccountBrowser(this,dataPath),cloudLifetime.Token);return true;}
            if(action=="accountProfileSave"){var result=await cloud.UpdatePersonalProfile(S(p,"firstName"),S(p,"lastName"),S(p,"email"),new InAppAccountBrowser(this,dataPath),cloudLifetime.Token);new AccountWorkspaces(rootDataPath).Remember(cloud.State);return result;}
            if(action=="accountPasswordSave")return await cloud.UpdatePersonalPassword(S(p,"password"),new InAppAccountBrowser(this,dataPath),cloudLifetime.Token);
            if(action=="accountMethods")return await cloud.SecurityMethods(cloudLifetime.Token);
            if(action is "ticketFind" or "ticketLinks" or "ticketLinkCreate" or "ticketLinkDelete"){
                var linkGeneration=authenticationGeneration;RequireOperationSession(linkGeneration);object links=action switch{"ticketFind"=>await cloud.Tickets(accountOperationStop.Token,S(p,"search")),"ticketLinks"=>await cloud.TicketLinks(Guid.Parse(S(p,"id")),accountOperationStop.Token),"ticketLinkCreate"=>await cloud.LinkTickets(Guid.Parse(S(p,"id")),Guid.Parse(S(p,"relatedId")),S(p,"kind"),accountOperationStop.Token),_=>await cloud.UnlinkTickets(Guid.Parse(S(p,"id")),Guid.Parse(S(p,"link")),accountOperationStop.Token)};RequireOperationSession(linkGeneration);return links;
            }
            if(action is "notificationOverview" or "notificationRead" or "notificationReadAll" or "notificationDisplay"){
                var notificationGeneration=authenticationGeneration;RequireOperationSession(notificationGeneration);object result;
                if(action=="notificationRead")result=await cloud.ReadNotification(Guid.Parse(S(p,"id")),accountOperationStop.Token);
                else if(action=="notificationReadAll")result=await cloud.ReadAllNotifications(accountOperationStop.Token);
                else if(action=="notificationDisplay")result=await cloud.NotificationDisplay(p.GetProperty("value"),p.GetProperty("version").GetInt64(),accountOperationStop.Token);
                else{var rows=await cloud.HomeNotifications(accountOperationStop.Token);var summary=await cloud.NotificationSummary(accountOperationStop.Token);var workspace=await cloud.WorkspaceSettings(accountOperationStop.Token);result=new{rows,summary,workspace};}
                RequireOperationSession(notificationGeneration);return result;
            }
            if(action=="accountStorageAttachmentDelete"){var generation=authenticationGeneration;RequireOperationSession(generation);var result=await cloud.DeleteStorageAttachment(p.GetProperty("attachmentId").GetGuid(),accountOperationStop.Token);RequireOperationSession(generation);return result;}
            if(action=="accountStorageAttachments"){var attachmentsGeneration=authenticationGeneration;RequireOperationSession(attachmentsGeneration);var attachments=await cloud.StorageAttachments(p.GetProperty("page").GetInt32(),accountOperationStop.Token);RequireOperationSession(attachmentsGeneration);return attachments;}
            if(action=="accountStorageUsage"){var usageGeneration=authenticationGeneration;RequireOperationSession(usageGeneration);var usage=await cloud.StorageUsage(accountOperationStop.Token);RequireOperationSession(usageGeneration);return usage;}
            if(action=="homeOverview"){
                var generation=authenticationGeneration;RequireOperationSession(generation);var owner=cloud.State.Owner;
                var archives=await cloud.Archives(accountOperationStop.Token);RequireOperationSession(generation);
                var tickets=await cloud.Tickets(accountOperationStop.Token);RequireOperationSession(generation);
                var notifications=await cloud.HomeNotifications(accountOperationStop.Token);RequireOperationSession(generation);
                if(owner!=cloud.State.Owner)throw new OperationCanceledException();return new{archives,tickets,notifications,checkedAt=DateTimeOffset.UtcNow};
            }
            if(action=="ticketList")return await cloud.Tickets(cloudLifetime.Token);
            if(action is "ticketAttachmentPreview" or "ticketAttachmentSave"){
                var attachmentGeneration=authenticationGeneration;RequireOperationSession(attachmentGeneration);var attachmentOwner=cloud.State.Owner;var preview=await cloud.TicketAttachment(Guid.Parse(S(p,"id")),Guid.Parse(S(p,"attachment")),accountOperationStop.Token);RequireOperationSession(attachmentGeneration);if(cloud.State.Owner!=attachmentOwner)throw new OperationCanceledException();if(action=="ticketAttachmentPreview")return preview;var destination=SaveFile(NativeText("Salva allegato"),preview.Name);if(destination==null)return false;RequireOperationSession(attachmentGeneration);var fullDestination=Path.GetFullPath(destination);if(fullDestination.StartsWith(Path.GetFullPath(dataPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||fullDestination.StartsWith(Path.GetFullPath(AppContext.BaseDirectory),StringComparison.OrdinalIgnoreCase))throw new IOException("Salva l’allegato fuori dalle cartelle dati e installazione dell’app.");var attachmentBytes=Convert.FromBase64String(preview.Data);try{PrivateKeyExport.Write(fullDestination,attachmentBytes);return true;}finally{CryptographicOperations.ZeroMemory(attachmentBytes);}
            }
            if(action=="ticketRead")return await cloud.Ticket(Guid.Parse(S(p,"id")),cloudLifetime.Token);
            if(action=="ticketCreate")return await cloud.CreateTicket(S(p,"title"),S(p,"body"),S(p,"category"),S(p,"priority"),cloudLifetime.Token);
            if(action=="ticketReply")return await cloud.ReplyTicket(Guid.Parse(S(p,"id")),S(p,"body"),p.GetProperty("version").GetInt64(),cloudLifetime.Token);
            if(action=="accountProfile")return await cloud.Profile(cloudLifetime.Token);
            if(action=="accountLogout"){
                if(activeOperations>1||transferActive||QueueBusy||EditorsBusy||ExportBusy||helloBusy||certificateRenewBusy||connectingSessions.Count>0)throw new InvalidOperationException("Completa o metti in pausa le operazioni in corso prima di uscire.");
                if(!Confirm("Uscire dall’account dell’app? Le sessioni remote saranno chiuse e l’archivio verrà bloccato. I dati cifrati restano su questo PC; le modifiche non salvate negli editor saranno scartate."))return Snapshot();
                accountLoggingOut=true;
                try{await CloseAllStorage();await CloseAllLocal();foreach(var editor in editBuffers.Keys.ToArray())CloseEditor(editor);
                foreach(var session in desktopSessions.Keys.ToArray())DisconnectDesktop(session);
                foreach(var session in sessions.Keys.ToArray())await DisconnectWithHooks(session,false);
                CloseDetachedWindows();StopAllRecordings();StopBundledAgent();ClearNativePlainExport();LockVault();connectionPasswords.Clear();connectionPassphrases.Clear();assistantOutput.Clear();
                appAuthenticated=false;consoleReady=false;authenticationGeneration++;store.Save(data);
                await cloud.LogoutAsync(cloudLifetime.Token);cloudLastVerified=null;CloudNotify("Account disconnesso. I dati locali restano cifrati.");return AccessBootstrap();
                }finally{accountLoggingOut=false;}
            }
            if(action=="accountSetup"){
                if(AccountLocked||!vault.IsUnlocked)throw new InvalidOperationException("Accedi al sito e sblocca prima l’archivio locale.");
                var password=S(p,"password");if(password.Length is <12 or >4096)throw new ArgumentException("Usa la password dell’archivio online (almeno 12 caratteri).");
                var recover=p.TryGetProperty("recoverPersonal",out var recoverValue)&&recoverValue.GetBoolean();
                if(recover)
                {
                    if(!p.TryGetProperty("reviewedLocal",out var reviewed)||!reviewed.GetBoolean())throw new InvalidOperationException("Verifica prima la copia locale o ripristinala da un backup attendibile.");
                    VaultPasswordPolicy.ValidateCreation(password,S(p,"confirmation"));
                    if(!Confirm("Ripristinare la sincronizzazione usando esclusivamente la copia locale che hai verificato? Nessun dato verrà importato dal vecchio archivio condiviso. Verrà creata una copia di recupero e sarà richiesta una nuova autenticazione con 2FA."))return Snapshot();
                    await cloud.LoginAsync(cloudLifetime.Token);var personal=await cloud.PersonalArchive(cloudLifetime.Token);
                    SyncLocalBackup.Create(dataPath,false);var local=VaultSyncCrypto.Capture(vault,data,hosts.List());
                    try{await PersonalArchiveRecovery.Commit(cloud.State,personal,password,local,()=>cloud.ReadArchive(personal,cloudLifetime.Token),value=>cloud.WriteArchive(personal,value,0,cloudLifetime.Token),(owner,bytes)=>vault.StoreSyncKey(owner,bytes),cloud.Save);}
                    catch(CryptographicException){throw new InvalidOperationException("Codice di recupero errato o copia online non integra. Nessuna copia è stata sovrascritta.");}
                    finally{CryptographicOperations.ZeroMemory(local);}
                    cloudPersonalRecovery=false;cloudConflict=false;CloudNotify("Sincronizzazione personale ripristinata dalla copia locale verificata. Il vecchio archivio è conservato.");return Snapshot();
                }
                var targetPersonal=await cloud.PersonalArchive(cloudLifetime.Token);
                if(targetPersonal.ArchiveOwner!=cloud.SyncOwner){cloudPersonalRecovery=true;throw new InvalidOperationException("L’archivio personale è cambiato. Usa Ripristina sincronizzazione dalla copia locale verificata; i dati esistenti sono conservati.");}
                var remote=await cloud.Read(cloudLifetime.Token);if(AccountErasureLocked)throw new TaskCanceledException("Account bloccato durante la verifica.");
                if(remote==null)VaultPasswordPolicy.ValidateCreation(password,S(p,"confirmation"));
                byte[]? key=null,plain=null;
                SyncLocalBackup.Create(dataPath,false);
                try {
                    if(remote==null){
                        key=RandomNumberGenerator.GetBytes(32);plain=VaultSyncCrypto.Capture(vault,data,hosts.List());
                        var envelope=await Task.Run(()=>VaultSyncCrypto.Create(cloud.SyncOwner,password,key,plain));
                        cloud.State.Revision=0;
                        var revision=await cloud.Write(envelope,cloudLifetime.Token);
                        if(revision==null)throw new OnlineAccessException("Un altro dispositivo ha creato l’archivio. Riprova con la password di quell’archivio; i dati locali sono conservati.");
                        vault.StoreSyncKey(cloud.SyncOwner,key);Checkpoint(revision.Value,VaultSyncCrypto.Hash(plain),envelope);
                        cloudPersonalRecovery=false;CloudNotify("Migrazione completata. Archivio, profili, cartelle e rubrica sono cifrati online. La copia locale è conservata.");
                    }else{
                        key=await Task.Run(()=>VaultSyncCrypto.UnlockKey(remote.Envelope,cloud.SyncOwner,password));
                        var decrypted=await Task.Run(()=>VaultSyncCrypto.Open(remote.Envelope,cloud.SyncOwner,key));
                        try{plain=VaultSyncCrypto.Canonicalize(decrypted);}finally{CryptographicOperations.ZeroMemory(decrypted);}
                        vault.StoreSyncKey(cloud.SyncOwner,key);cloud.State.Header=HeaderOnly(remote.Envelope);cloud.State.Migrated=true;cloud.State.Baseline="";cloud.Save();
                        var records=vault.ExportSyncRecords();bool empty;
                        try{using var doc=JsonDocument.Parse(records);empty=doc.RootElement.GetArrayLength()==0&&data.Profiles.Count==0&&data.Snippets.Count==0&&data.Machines.Count==0&&data.Workspaces.Count==0;}finally{CryptographicOperations.ZeroMemory(records);}
                        if(empty)ApplyCloud(plain,remote);else await SynchronizeCloud(entered:true);
                        if(!cloudConflict)CloudNotify("Archivio online collegato. Le prossime modifiche verranno sincronizzate automaticamente.");
                    }
                }catch(CryptographicException){throw new ArgumentException("Password dell’archivio online errata oppure copia online non integra. I dati locali sono conservati.");}
                finally{if(key!=null)CryptographicOperations.ZeroMemory(key);if(plain!=null)CryptographicOperations.ZeroMemory(plain);}
                return Snapshot();
            }
            if(action=="accountSync"){
                var choice=S(p,"resolution");if(choice is not("" or "local" or "online"))throw new ArgumentException("Scelta non valida.");
                if(choice.Length>0){if(!cloudConflict)throw new InvalidOperationException("Aggiorna prima lo stato dell’archivio.");if(!Confirm(choice=="local"?"Usare la copia di questo PC come versione online? Conserverò la precedente copia cifrata e le versioni disponibili.":"Ricevere la copia online su questo PC? Prima salverò una copia di recupero dei file locali."))return Snapshot();if(choice=="local")SyncLocalBackup.Create(dataPath,false);}
                await SynchronizeCloud(choice,true);return Snapshot();
            }
            throw new ArgumentException("Operazione account non disponibile.");
        }finally{cloudGate.Release();}
    }
}
