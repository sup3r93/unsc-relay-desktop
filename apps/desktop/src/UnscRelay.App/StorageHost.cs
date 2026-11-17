using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly Dictionary<string,StorageFiles> storageSessions=[];
    readonly Dictionary<string,string> nativeStorageParents=[];
    readonly HashSet<string> advancedNativeChildren=[];
    async Task CloseStorage(string id){nativeStorageParents.Remove(id);advancedNativeChildren.Remove(id);if(sessions.Remove(id,out var nativeFiles))await Task.Run(nativeFiles.Dispose);if(storageSessions.Remove(id,out var session))await session.DisposeAsync();}
    async Task<object> OpenNativeFileWorkspace(JsonElement p)
    {
        var id=S(p,"sessionId");var source=S(p,"sourceSessionId");
        if(!Guid.TryParse(id,out _)||sessions.ContainsKey(id)||storageSessions.ContainsKey(id)||localSessions.ContainsKey(id)||nativeStorageParents.TryGetValue(id,out var previousParent)&&nativeControls.ContainsKey(previousParent)||!nativeStorageParents.ContainsKey(id)&&nativeStorageParents.Count>=30)throw new ArgumentException("Scheda SFTP non valida o limite raggiunto.");
        if(!nativeControls.TryGetValue(source,out var control)||!nativeSessionProfiles.TryGetValue(source,out var profile)||profile.Id!=S(p,"profileId")||!nativeAuthenticated.Contains(source))throw new IOException("Apri prima la sessione OpenSSH di questo profilo.");
        if(editBuffers.Values.Any(e=>e.SessionId==id&&e.NativeSnapshot!=NativeRecovery.Snapshot(profile)))throw new IOException("Chiudi gli editor prima di collegare questa scheda a un profilo OpenSSH modificato.");
        var files=await SshSession.OpenNativeFiles(profile,control,id:id);
        if(!nativeControls.TryGetValue(source,out var current)||!ReferenceEquals(current,control)||sessions.ContainsKey(id)){files.Dispose();throw new IOException("Sessione OpenSSH cambiata durante l'apertura.");}
        sessions.Add(id,files);nativeStorageParents[id]=source;advancedNativeChildren.Add(id);RebindFileQueue(profile.Id,id);
        return new{sessionId=id,filesAvailable=true};
    }
    async Task CloseAllStorage(){foreach(var id in storageSessions.Keys.Union(nativeStorageParents.Keys).ToArray())await CloseStorage(id);}
    async Task<object?> StorageAction(string action,JsonElement p)
    {
        var id=S(p,"sessionId");
        if(action=="storageNativeOpen"){
            var source=S(p,"sourceSessionId");
            if(string.IsNullOrWhiteSpace(id)||id.Length>100||storageSessions.ContainsKey(id)||nativeStorageParents.ContainsKey(id)||localSessions.ContainsKey(id)||storageSessions.Keys.Union(nativeStorageParents.Keys).Count()>=30)throw new ArgumentException("Scheda SFTP non valida o limite raggiunto.");
            if(!nativeControls.TryGetValue(source,out var control)||!nativeSessionProfiles.TryGetValue(source,out var nativeProfile)||nativeProfile.Id!=S(p,"profileId"))throw new IOException("Apri prima la sessione OpenSSH di questo profilo.");
            await control.WaitReady();
            if(!nativeControls.TryGetValue(source,out var current)||!ReferenceEquals(current,control))throw new IOException("Sessione OpenSSH chiusa.");
            var session=new StorageFiles(NativeClient.Find("s3")??throw new IOException("Motore file incluso mancante."),control,nativeProfile.RemoteDirectory,nativeProfile.Subsystem);
            storageSessions.Add(id,session);nativeStorageParents.Add(id,source);
            try{string? followRoot=null;try{followRoot=await control.ResolveDirectory(nativeProfile.RemoteDirectory);}catch(Exception ex)when(ex is IOException or OperationCanceledException){}return new{entries=await session.List(""),path="",followRoot};}catch{await CloseStorage(id);throw;}
        }
        if(action=="storageOpen"){
            if(string.IsNullOrWhiteSpace(id)||id.Length>100||storageSessions.ContainsKey(id)||storageSessions.Count>=30)throw new ArgumentException("Sessione storage non valida o limite raggiunto.");
            var profile=data.Profiles.Single(x=>x.Id==S(p,"profileId"));profile.Validate();
            if(!profile.Storage.Browser)throw new ArgumentException("Non e un profilo storage grafico.");
            if(profile.Native.Kind=="ftp"&&!profile.Storage.Tls&&!Confirm("Questa connessione FTP invia credenziali e file senza cifratura. Continuare soltanto su una rete fidata?"))throw new OperationCanceledException();
            var session=new StorageFiles(NativeClient.Find("s3")??throw new IOException("Motore Rclone incluso mancante: ripara l'installazione."),profile,ConnectionPassword(profile));
            storageSessions.Add(id,session);
            try{return new{entries=await session.List(""),path=""};}catch{await CloseStorage(id);throw;}
        }
        if(!storageSessions.TryGetValue(id,out var files))throw new IOException("Browser non connesso. Riapri la scheda.");
        if(action=="storageCancel"){files.Cancel();return null;}
        var path=StorageFiles.Normalize(S(p,"path"));
        if(action=="storageList")return await files.List(path);
        if(action=="storageCompare"){
            var local=OpenFile("Scegli il file locale da confrontare");if(local==null)return new{cancelled=true};
            // The local path is selected by Windows, never supplied by page content.
            var localText=await ReadLocalComparison(local);
            var remoteText=await files.ReadText(path);
            return new{local=localText,remote=remoteText};
        }
        if(action=="storageTransfer"){
            var upload=p.TryGetProperty("upload",out var direction)&&direction.GetBoolean();
            var local=upload?OpenFile("Seleziona il file da caricare"):SaveFile("Scarica file",path.Split('/').Last());if(local==null)return new{cancelled=true};
            if(upload){var name=Path.GetFileName(local);path=(path.Length>0?path+"/":"")+name;}
            var replace=false;
            if(upload){var slash=path.LastIndexOf('/');var parent=slash<0?"":path[..slash];var existing=(await files.List(parent)).SingleOrDefault(x=>x.Path==path);if(existing!=null){if(existing.Directory)throw new IOException("Esiste una cartella con questo nome.");if(!Confirm("Il file remoto esiste gia. Sostituire «"+path+"»?"))return new{cancelled=true};replace=true;}}
            else replace=File.Exists(local); // SaveFileDialog already asks before replacing.
            await files.Transfer(path,local,upload,replace);return new{cancelled=false};
        }
        if(action=="storageChange"){
            var verb=S(p,"verb");if(verb=="deletefile"&&!Confirm("Eliminare definitivamente il file remoto «"+path+"»?"))return new{cancelled=true};
            await files.Change(verb,path,verb=="moveto"?S(p,"destination"):null);return new{cancelled=false};
        }
        throw new ArgumentException("Operazione storage non riconosciuta.");
    }
}
