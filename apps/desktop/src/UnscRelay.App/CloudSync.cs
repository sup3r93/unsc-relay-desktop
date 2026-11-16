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
    bool cloudConflict, cloudUiBusy, cloudRunning, accountLoggingOut;
    DateTimeOffset? cloudLastVerified;
    string cloudMessage="Collega l’account UNSC per attivare questo dispositivo.";
    bool AccountLocked=>accountLoggingOut||(!smokeTest&&!cloud.State.Activated);
    void InitializeCloud()
    {
        cloud=new(dataPath);
        cloudMessage=cloud.State.Activated?"Copia locale disponibile. La sincronizzazione riprende dopo lo sblocco.":cloudMessage;
        cloudTimer.Tick+=async(_,_)=>{if(!smokeTest&&consoleReady&&!closing&&!updating&&!cloudUiBusy&&activeOperations==0)await SynchronizeCloud();};
        if(!smokeTest)cloudTimer.Start();
    }
    object CloudStatus()=>new {requiresLink=AccountLocked,linked=cloud.State.Activated,name=cloud.State.Name,
        ready=cloud.State.Migrated&&cloud.State.Header!=null,revision=cloud.State.Revision,online=cloudLastVerified>DateTimeOffset.UtcNow.AddSeconds(-90),lastVerified=cloudLastVerified,conflict=cloudConflict,busy=cloudRunning,message=cloudMessage};
    void CloudNotify(string message){cloudMessage=message;Emit("account",CloudStatus());}
    static SyncEnvelope HeaderOnly(SyncEnvelope value)=>value with{Iv=Convert.ToBase64String(new byte[12]),Ciphertext=Convert.ToBase64String(new byte[16])};
    void Checkpoint(long revision,string hash,SyncEnvelope envelope)
    {
        cloud.State.Revision=revision;cloud.State.Baseline=hash;cloud.State.Header=HeaderOnly(envelope);cloud.State.Migrated=true;cloud.Save();cloudConflict=false;
    }
    void PreserveRemote(RemoteVault remote)
    {
        var directory=Path.Combine(dataPath,"SyncConflicts");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,cloud.State.Owner+"-r"+remote.Revision+".json");
        if(!File.Exists(path))AtomicFile.Write(path,JsonSerializer.SerializeToUtf8Bytes(remote,VaultSyncCrypto.Json));
    }
    void ApplyCloud(byte[] bytes,RemoteVault remote)
    {
        var incoming=VaultSyncCrypto.Parse(bytes);
        SyncLocalBackup.Create(dataPath,true);
        try {
            vault.ImportSyncRecords(incoming.Records);
            incoming.Data.Settings=data.Settings;incoming.Data.FileLocations=data.FileLocations;
            store.Save(incoming.Data);
            AtomicFile.Write(Path.Combine(dataPath,"hostkeys.json"),JsonSerializer.SerializeToUtf8Bytes(incoming.HostKeys));
            data=incoming.Data;
            var captured=VaultSyncCrypto.Capture(vault,data,hosts.List());
            try{Checkpoint(remote.Revision,VaultSyncCrypto.Hash(bytes),remote.Envelope);}finally{CryptographicOperations.ZeroMemory(captured);}
            SyncLocalBackup.Complete(dataPath);
        }catch {
            // Keep the journal until all four files have been restored successfully.
            vault.Lock();SyncLocalBackup.Recover(dataPath);data=store.Load();
            cloud.Dispose();cloud=new(dataPath);throw;
        }
        Emit("data",Snapshot());
    }
    async Task SynchronizeCloud(string resolution="",bool entered=false)
    {
        if(!entered&&!await cloudGate.WaitAsync(0))return;
        cloudRunning=true;
        byte[]? key=null,local=null,remotePlain=null;
        try {
            if(!cloud.State.Activated)return;
            await cloud.VerifyOnline(cloudLifetime.Token);cloudLastVerified=DateTimeOffset.UtcNow;
            if(!vault.IsUnlocked){CloudNotify("Archivio locale bloccato. Le modifiche saranno sincronizzate dopo lo sblocco.");return;}
            if(!cloud.State.Migrated||cloud.State.Header==null){CloudNotify("Completa la migrazione dell’archivio nelle impostazioni Account e sincronizzazione.");return;}
            key=vault.SyncKey(cloud.State.Owner);
            if(key==null){CloudNotify("Sblocca l’archivio online con la sua password per collegare questo archivio locale.");return;}
            local=VaultSyncCrypto.Capture(vault,data,hosts.List());var localHash=VaultSyncCrypto.Hash(local);
            var remote=await cloud.Read(cloudLifetime.Token);
            if(remote==null)throw new OnlineAccessException("L’archivio online non è disponibile. La copia locale è conservata; non verrà sostituita automaticamente.");
            if(remote.Revision<cloud.State.Revision)throw new OnlineAccessException("La revisione online è precedente all’ultima verificata. Sincronizzazione sospesa per proteggere i dati.");
            var decrypted=await Task.Run(()=>VaultSyncCrypto.Open(remote.Envelope,cloud.State.Owner,key));
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
            CloudNotify(ex is OnlineAccessException?ex.Message:ex is CryptographicException?"La chiave online è cambiata: apri le impostazioni e inserisci la password dell’archivio online. La copia locale è conservata.":"Non riesco a sincronizzare adesso. Puoi lavorare offline: riproverò automaticamente.");
        }finally{
            if(key!=null)CryptographicOperations.ZeroMemory(key);if(local!=null)CryptographicOperations.ZeroMemory(local);if(remotePlain!=null)CryptographicOperations.ZeroMemory(remotePlain);
            cloudRunning=false;Emit("account",CloudStatus());if(!entered)cloudGate.Release();
        }
    }
    async Task<object?> CloudAction(string action,JsonElement p)
    {
        if(action=="accountStatus")return CloudStatus();
        if(action=="accountAvailability")return new{registrationAllowed=await cloud.RegistrationAllowed(cloudLifetime.Token)};
        if(action=="accountUiBusy"){cloudUiBusy=p.GetProperty("busy").GetBoolean();return null;}
        if(action=="accountSite"){Process.Start(new ProcessStartInfo("https://unsc.responda.eu/console"){UseShellExecute=true});return null;}
        if(!await cloudGate.WaitAsync(0))throw new InvalidOperationException("Attendi il completamento della sincronizzazione.");
        try {
            if(action=="accountLogin"||action=="accountRegister"||action=="accountSecurity"){
                if(startupSequencePlaying)throw new InvalidOperationException("Attendi la sequenza iniziale.");
                var accountAction=action=="accountRegister"?"register":action=="accountSecurity"?S(p,"action"):"login";
                var generation=authenticationGeneration;
                await cloud.LoginAsync(cloudLifetime.Token,new InAppAccountBrowser(this,dataPath),accountAction);
                if(accountAction is "login" or "register")await UnlockOnlineAfterLogin(generation);
                cloudLastVerified=DateTimeOffset.UtcNow;CloudNotify("Account verificato per "+cloud.State.Name+". Windows Hello offline è disponibile solo dopo abilitazione manuale.");return AccessBootstrap();
            }
            if(action=="accountProfile")return await cloud.Profile(cloudLifetime.Token);
            if(action=="accountLogout"){
                if(activeOperations>1||transferActive||QueueBusy||EditorsBusy||ExportBusy||helloBusy||certificateRenewBusy||connectingSessions.Count>0)throw new InvalidOperationException("Completa o metti in pausa le operazioni in corso prima di uscire.");
                if(!Confirm("Uscire dall’account dell’app? Le sessioni remote saranno chiuse e l’archivio verrà bloccato. I dati cifrati restano su questo PC; le modifiche non salvate negli editor saranno scartate."))return Snapshot();
                accountLoggingOut=true;
                try{await CloseAllLocal();foreach(var editor in editBuffers.Keys.ToArray())CloseEditor(editor);
                foreach(var session in desktopSessions.Keys.ToArray())DisconnectDesktop(session);
                foreach(var session in sessions.Keys.ToArray())await DisconnectWithHooks(session,false);
                CloseDetachedWindows();StopAllRecordings();StopBundledAgent();vault.Lock();connectionPasswords.Clear();connectionPassphrases.Clear();assistantOutput.Clear();
                appAuthenticated=false;consoleReady=false;authenticationGeneration++;store.Save(data);
                await cloud.LogoutAsync(cloudLifetime.Token);cloudLastVerified=null;CloudNotify("Account disconnesso. I dati locali restano cifrati.");return AccessBootstrap();
                }finally{accountLoggingOut=false;}
            }
            if(action=="accountSetup"){
                if(AccountLocked||!vault.IsUnlocked)throw new InvalidOperationException("Accedi al sito e sblocca prima l’archivio locale.");
                var password=S(p,"password");if(password.Length is <12 or >4096)throw new ArgumentException("Usa la password dell’archivio online (almeno 12 caratteri).");
                var remote=await cloud.Read(cloudLifetime.Token);
                if(remote==null)VaultPasswordPolicy.ValidateCreation(password,S(p,"confirmation"));
                byte[]? key=null,plain=null;
                SyncLocalBackup.Create(dataPath,false);
                try {
                    if(remote==null){
                        key=RandomNumberGenerator.GetBytes(32);plain=VaultSyncCrypto.Capture(vault,data,hosts.List());
                        var envelope=await Task.Run(()=>VaultSyncCrypto.Create(cloud.State.Owner,password,key,plain));
                        cloud.State.Revision=0;
                        var revision=await cloud.Write(envelope,cloudLifetime.Token);
                        if(revision==null)throw new OnlineAccessException("Un altro dispositivo ha creato l’archivio. Riprova con la password di quell’archivio; i dati locali sono conservati.");
                        vault.StoreSyncKey(cloud.State.Owner,key);Checkpoint(revision.Value,VaultSyncCrypto.Hash(plain),envelope);
                        CloudNotify("Migrazione completata. Archivio, profili, cartelle e rubrica sono cifrati online. La copia locale è conservata.");
                    }else{
                        key=await Task.Run(()=>VaultSyncCrypto.UnlockKey(remote.Envelope,cloud.State.Owner,password));
                        var decrypted=await Task.Run(()=>VaultSyncCrypto.Open(remote.Envelope,cloud.State.Owner,key));
                        try{plain=VaultSyncCrypto.Canonicalize(decrypted);}finally{CryptographicOperations.ZeroMemory(decrypted);}
                        vault.StoreSyncKey(cloud.State.Owner,key);cloud.State.Header=HeaderOnly(remote.Envelope);cloud.State.Migrated=true;cloud.State.Baseline="";cloud.Save();
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
