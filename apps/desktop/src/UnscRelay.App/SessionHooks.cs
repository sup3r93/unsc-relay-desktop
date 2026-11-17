using System.Collections.Concurrent;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly ConcurrentDictionary<string,ConnectionProfile> hookedSessions=[];
    void ConfirmHookChanges(ConnectionProfile profile,ConnectionProfile? old)
    {
        if(profile.Subsystem.Length>0&&old?.Subsystem!=profile.Subsystem&&!Confirm("Usare questo comando remoto per avviare SFTP su «"+profile.Name+"»?\n\n"+profile.Subsystem+"\n\nDeve produrre soltanto il flusso binario SFTP. Eventuali privilegi sudo dipendono dal server."))throw new OperationCanceledException();
        if(!profile.Hooks.Any(h=>h.Enabled))return;
        if(old!=null&&JsonSerializer.Serialize(old.Hooks)==JsonSerializer.Serialize(profile.Hooks))return;
        var description=string.Join("\n\n",profile.Hooks.Where(h=>h.Enabled).Select(h=>h.Phase+" / "+h.Kind+" / "+h.TimeoutSeconds+"s\n"+h.Command+"\n"+string.Join("\n",h.Arguments)));
        if(!Confirm("Abilitare questi hook per «"+profile.Name+"»? Verranno eseguiti per le connessioni manuali e alla loro chiusura, anche se la rete cade. Non durante aggiornamenti o riconnessioni automatiche.\nI comandi e gli argomenti sono salvati nel profilo, senza cifratura: non inserire segreti.\n\n"+description))throw new OperationCanceledException();
    }
    async Task RunHooks(ConnectionProfile profile,string phase,SshSession? session=null,bool required=false)
    {
        if(CriticalUpdateRequired)return;
        foreach(var hook in profile.Hooks.Where(h=>h.Enabled&&h.Phase==phase))
        {
            if(CriticalUpdateRequired)return;
            Emit("notice",new{text="Hook "+phase+" / "+profile.Name+" in esecuzione..."});
            try{if(hook.Kind=="local")await ConnectionHooks.RunLocal(hook);else if(session!=null)await session.RunRemoteHook(hook);}
            catch(Exception ex){if(required)throw;Emit("notice",new{text="Hook "+phase+" / "+profile.Name+": "+ex.Message});}
        }
    }
    async Task DisconnectWithHooks(string id,bool runHooks=true)
    {
        hookedSessions.TryRemove(id,out var profile);
        sessions.TryGetValue(id,out var session);
        if(runHooks&&profile!=null&&session?.Connected==true)await RunHooks(profile,"before-disconnect",session);
        brokenSessions.Remove(id); sessionRouteSnapshots.Remove(id);
        StopRecordingSession(id);
        if(sessions.Remove(id,out var ended))await Task.Run(ended.Dispose);
        if(runHooks&&profile!=null)await RunHooks(profile,"after-disconnect");
    }
    void SessionEnded(string id)
    {
        Dispatcher.BeginInvoke(async()=>{if(CriticalUpdateRequired||closing||updating||!hookedSessions.TryRemove(id,out var profile))return;if(!(brokenSessions.Contains(id)&&automaticRecoveryProfiles.Contains(profile.Id)))await RunHooks(profile,"after-disconnect");});
    }
}
