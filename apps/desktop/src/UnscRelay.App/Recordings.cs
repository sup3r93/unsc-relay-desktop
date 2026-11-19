using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly ConcurrentDictionary<string,SessionRecording> recordings=[];
    RetentionResult? retentionResult;
    readonly System.Windows.Threading.DispatcherTimer retentionTimer=new(){Interval=TimeSpan.FromHours(1)};
    void InitializeRecordingRetention(){if(data.Settings.RecordingRetentionDays is <0 or >3650)data.Settings=data.Settings with {RecordingRetentionDays=0};RunRecordingRetention();retentionTimer.Tick+=(_,_)=>RunRecordingRetention();retentionTimer.Start();Closed+=(_,_)=>retentionTimer.Stop();}
    void RunRecordingRetention(){try{retentionResult=RecordingRetention.Apply(RecordingsPath,data.Settings.RecordingRetentionDays,recordings.Values.Select(r=>r.FilePath),DateTime.UtcNow);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){retentionResult=new(0,1,DateTime.UtcNow);}}
    string RecordingsPath=>Path.Combine(dataPath,"Recordings");
    void StopRecordingSession(string id)
    {
        if(recordings.TryRemove(id,out var recorder)){try{recorder.Dispose();}catch(Exception ex){Dispatcher.BeginInvoke(()=>Emit("notice",new{text="Chiusura registrazione: "+ex.Message}));}Dispatcher.BeginInvoke(()=>Emit("recordingStatus",new{sessionId=id,active=false}));}
    }
    void StopAllRecordings(){foreach(var id in recordings.Keys.ToArray())StopRecordingSession(id);}
    void RecordOutput(string id,byte[] bytes)
    {
        if(!recordings.TryGetValue(id,out var recorder))return;
        if(!vault.IsUnlocked){StopRecordingSession(id);return;}
        try{recorder.Write(bytes);}catch(ObjectDisposedException){}catch(Exception ex){StopRecordingSession(id);Dispatcher.BeginInvoke(()=>Emit("notice",new{text=ex.Message}));}
    }
    string RecordingPath(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Registrazione non valida.");return Path.Combine(RecordingsPath,id+".urec");}
    object? RecordingAction(string action,JsonElement p)
    {
        if(action=="recordingRetentionStatus")return new{days=data.Settings.RecordingRetentionDays,result=retentionResult};
        if(action=="recordingRetentionSet")
        {
            var days=I(p,"days",0);if(days is <0 or >3650)throw new ArgumentException("Indica 0 (disattivata) oppure da 1 a 3650 giorni.");
            if(days>0&&days!=data.Settings.RecordingRetentionDays&&!Confirm("Eliminare automaticamente le registrazioni chiuse più vecchie di "+days+" giorni, all’avvio e ogni ora? La cancellazione locale è definitiva. Le registrazioni attive vengono escluse."))throw new OperationCanceledException();
            data.Settings=data.Settings with {RecordingRetentionDays=days};store.Save(data);RunRecordingRetention();return Snapshot();
        }
        if(action=="recordingList")return Directory.Exists(RecordingsPath)?Directory.GetFiles(RecordingsPath,"*.urec").Select(path=>new FileInfo(path)).Where(f=>Guid.TryParseExact(Path.GetFileNameWithoutExtension(f.Name),"N",out _)).OrderByDescending(f=>f.CreationTimeUtc).Take(1000).Select(f=>new{id=Path.GetFileNameWithoutExtension(f.Name),created=f.CreationTimeUtc,bytes=f.Length}).ToArray():[];
        if(action=="recordingStop"){StopRecordingSession(S(p,"sessionId"));return null;}
        if(action=="recordingStart")
        {
            var id=S(p,"sessionId");ConnectionProfile recordingProfile;if(nativeSessionProfiles.TryGetValue(id,out var nativeProfile)&&nativeProfile.Native.Kind=="ssh"&&localSessions.TryGetValue(id,out var nativeSession)&&nativeSession.Running)recordingProfile=nativeProfile;else{var session=Session(p);if(session.Profile.Protocol!="ssh"||!session.CanWrite)throw new IOException("Seleziona un terminale SSH connesso.");recordingProfile=session.Profile;}
            if(recordings.ContainsKey(id))throw new IOException("Registrazione gia attiva.");
            if(!vault.IsUnlocked)throw new InvalidOperationException("Sblocca prima l'archivio chiavi.");
            if(!Confirm("Registrare l'output di "+recordingProfile.Name+" / "+(recordingProfile.Protocol=="native"?recordingProfile.Native.Target:recordingProfile.Host)+"?\n\nViene salvato soltanto l'output ricevuto, ma il server puo visualizzare comandi, password o dati sensibili. Nessun input da tastiera viene registrato separatamente. I file restano cifrati sul PC; per rileggerli serve questo archivio. La registrazione si arresta al blocco dell'archivio o alla disconnessione."))throw new OperationCanceledException();
            var material=vault.RecordingKey();try{Directory.CreateDirectory(RecordingsPath);var recorder=new SessionRecording(RecordingPath(Guid.NewGuid().ToString("N")),material.Id,material.Key,recordingProfile.Name+" / "+recordingProfile.Username+"@"+(recordingProfile.Protocol=="native"?recordingProfile.Native.Target:recordingProfile.Host)+":"+recordingProfile.Port);try{if(!recordings.TryAdd(id,recorder))throw new IOException("Registrazione gia attiva.");}catch{recorder.Dispose();throw;}}finally{CryptographicOperations.ZeroMemory(material.Key);}
            Emit("recordingStatus",new{sessionId=id,active=true});return null;
        }
        if(action=="recordingRead")
        {
            var path=RecordingPath(S(p,"recordingId"));var material=vault.RecordingKey(SessionRecording.KeyId(path));
            try{return SessionRecording.Read(path,material.Key);}catch(CryptographicException ex){throw new IOException("Registrazione alterata o non decifrabile con questo archivio.",ex);}finally{CryptographicOperations.ZeroMemory(material.Key);}
        }
        if(action=="recordingExportText")
        {
            if(!vault.IsUnlocked)throw new IOException("Sblocca prima l'archivio.");
            var source=RecordingPath(S(p,"recordingId"));var material=vault.RecordingKey(SessionRecording.KeyId(source));byte[] text=[];
            try
            {
                var replay=SessionRecording.Read(source,material.Key);text=RecordingText.Export(replay);
                if(!Confirm("Esportare questa registrazione in un file di testo NON cifrato?\n\nIl testo può contenere comandi, indirizzi, password o altri dati mostrati dal server. L'esportazione non rimuove automaticamente questi dati. L'originale cifrato resta invariato. I colori e le sequenze di controllo saranno rimossi."))throw new OperationCanceledException();
                var path=SaveFile("Esporta trascrizione in chiaro · scegli un nuovo file .txt","relay-session.txt");if(path==null)return null;
                var full=Path.GetFullPath(path);if(full.StartsWith(Path.GetFullPath(dataPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Salva il testo fuori dalla cartella dati dell'app.");
                RecordingText.SaveNew(full,text);return new{saved=true,fileName=Path.GetFileName(full),bytes=text.Length};
            }
            catch(CryptographicException ex){throw new IOException("Registrazione alterata o non decifrabile: nessun testo esportato.",ex);}
            finally{CryptographicOperations.ZeroMemory(material.Key);CryptographicOperations.ZeroMemory(text);}
        }
        if(action=="recordingDelete")
        {
            if(recordings.Count>0)throw new IOException("Arresta le registrazioni prima di eliminare file.");
            var path=RecordingPath(S(p,"recordingId"));if(!Confirm("Eliminare definitivamente questa registrazione locale?"))throw new OperationCanceledException();File.Delete(path);return null;
        }
        if(action=="recordingPrune")
        {
            if(recordings.Count>0)throw new IOException("Arresta le registrazioni prima della pulizia.");int days=I(p,"days",30);if(days is <1 or >3650)throw new ArgumentException("Indica da 1 a 3650 giorni.");
            var cutoff=DateTime.UtcNow.AddDays(-days);var files=Directory.Exists(RecordingsPath)?Directory.GetFiles(RecordingsPath,"*.urec").Where(f=>Guid.TryParseExact(Path.GetFileNameWithoutExtension(f),"N",out _)&&File.GetLastWriteTimeUtc(f)<cutoff).ToArray():[];
            if(!Confirm("Eliminare "+files.Length+" registrazioni locali piu vecchie di "+days+" giorni?"))throw new OperationCanceledException();foreach(var f in files)File.Delete(f);return null;
        }
        throw new ArgumentException("Comando registrazione non valido.");
    }
}
