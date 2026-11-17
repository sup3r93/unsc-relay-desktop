using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    void ValidateNativeRecovery(string id,ConnectionProfile profile,bool automatic)
    {
        if(!nativeRecoveryProfiles.TryGetValue(id,out var snapshot))throw new IOException("Sessione OpenSSH non disponibile.");
        NativeRecovery.Validate(profile,snapshot,vault.IsUnlocked);
        if(File.Exists(Path.Combine(dataPath,"NativeHome",".ssh","config")))throw new IOException("Config OpenSSH presente: riapri manualmente la sessione per confermarne i comandi.");
        if(automatic&&!automaticRecoveryProfiles.Contains(profile.Id))throw new OperationCanceledException("Riconnessione automatica disattivata.");
        if(CriticalUpdateRequired||closing||updating||updateClosing)throw new OperationCanceledException("Chiusura in corso.");
    }
    async Task<object> RecoverNativeProfile(ConnectionProfile profile,JsonElement payload)
    {
        bool automatic=payload.TryGetProperty("automatic",out var a)&&a.GetBoolean();
        var ids=nativeRecoveryProfiles.Where(x=>JsonSerializer.Deserialize<ConnectionProfile>(x.Value,ProfileStore.Json)?.Id==profile.Id).Select(x=>x.Key).ToArray();
        if(ids.Length==0)throw new IOException("Nessuna sessione OpenSSH da ripristinare.");
        foreach(var id in ids)ValidateNativeRecovery(id,profile,automatic);
        recoveringProfiles.Add(profile.Id);
        var result=new List<object>();var errors=new List<string>();
        try
        {
            foreach(var id in ids)
            {
                if(nativeAuthenticated.Contains(id)&&localSessions.TryGetValue(id,out var running)&&running.Running){result.Add(new{sessionId=id,connected=true,filesAvailable=true,terminalRestarted=false});continue;}
                var children=nativeStorageParents.Where(x=>x.Value==id).Select(x=>x.Key).ToArray();
                try
                {
                    ValidateNativeRecovery(id,profile,automatic);
                    if(localSessions.Remove(id,out var old))await old.DisposeAsync();
                    await EndNative(id,false,preserveChildren:true);
                    await ConnectLocal(profile,id,payload,recovering:true);
                    if(!nativeControls.TryGetValue(id,out var control))throw new IOException("Trasporto OpenSSH non disponibile.");
                    await control.WaitReady();ValidateNativeRecovery(id,profile,automatic);
                    if(!localSessions.TryGetValue(id,out var terminal)||!terminal.Running)throw new IOException("Connessione OpenSSH terminata durante il recupero.");
                    nativeAuthenticated.Add(id);
                    result.Add(new{sessionId=id,connected=true,filesAvailable=true,terminalRestarted=true});
                    foreach(var child in children)
                    {
                        if(!nativeStorageParents.TryGetValue(child,out var parent)||parent!=id)continue;
                        if(advancedNativeChildren.Contains(child))
                        {
                            if(sessions.Remove(child,out var oldAdvanced))await Task.Run(oldAdvanced.Dispose);
                            sessions[child]=await SshSession.OpenNativeFiles(profile,control,id:child);
                            result.Add(new{sessionId=child,connected=true,filesAvailable=true,terminalRestarted=false});
                            continue;
                        }
                        if(storageSessions.Remove(child,out var oldFiles))await oldFiles.DisposeAsync();
                        storageSessions[child]=new StorageFiles(NativeClient.Find("s3")??throw new IOException("Motore file mancante."),control,profile.RemoteDirectory,profile.Subsystem);
                        string? followRoot=null;try{followRoot=await control.ResolveDirectory(profile.RemoteDirectory);}catch(Exception ex)when(ex is IOException or OperationCanceledException){}
                        result.Add(new{sessionId=child,connected=true,filesAvailable=true,terminalRestarted=false,followRoot});
                    }
                }
                catch(Exception ex)when(ex is IOException or TimeoutException or InvalidOperationException or OperationCanceledException or Renci.SshNet.Common.SshException)
                {
                    errors.Add(ex.Message);nativeAuthenticated.Remove(id);
                    if(localSessions.Remove(id,out var failed))await failed.DisposeAsync();
                    await EndNative(id,false,preserveChildren:true);
                    result.Add(new{sessionId=id,connected=false,filesAvailable=false,terminalRestarted=false});
                }
            }
            if(errors.Count==0)
            {
                var waiting=fileJobs.Values.Where(j=>j.ProfileId==profile.Id&&j.RecoveryPaused).ToArray();
                await Task.WhenAll(waiting.Where(j=>j.Running!=null).Select(j=>j.Running!));
                foreach(var job in waiting.Where(j=>j.Status=="Attesa connessione"&&sessions.ContainsKey(j.SessionId)))
                {CheckJobEndpoint(job,sessions[job.SessionId]);job.Running=RunJob(job,true,automatic);}
            }
            // Native client exit 255 does not distinguish a network failure from auth/trust errors.
            // Do not automatically retry a failed replacement and risk a server lockout.
            return new{success=errors.Count==0,retryable=false,error=string.Join(" / ",errors.Distinct()),sessions=result};
        }
        finally{recoveringProfiles.Remove(profile.Id);}
    }
}
