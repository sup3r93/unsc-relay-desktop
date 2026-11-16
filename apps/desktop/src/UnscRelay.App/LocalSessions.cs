using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly Dictionary<string,LocalTerminal> localSessions=[];
    async Task<object> LocalAvailability(){string[] distributions=[];string? warning=null;try{distributions=await LocalShell.Distributions();}catch(IOException ex){warning=ex.Message;}return new{shells=LocalShell.Choices(),distributions,warning};}
    async Task<object> ConnectLocal(ConnectionProfile profile,string id,JsonElement payload,string? startupMacro=null)
    {
        if(profile.Protocol=="local"&&profile.LocalShell=="wsl"){var distributions=await LocalShell.Distributions();if(distributions.Length==0)throw new IOException("Nessuna distribuzione WSL installata. Configura WSL prima di aprire la sessione.");if(profile.WslDistribution.Length>0&&!distributions.Contains(profile.WslDistribution,StringComparer.Ordinal))throw new IOException("La distribuzione WSL scelta non e disponibile.");}
        if(nativeTasks.ContainsKey(profile.Id)&&!nativeTaskLaunches.ContainsKey(profile.Id))throw new IOException("Operazione terminata: crea una nuova richiesta dal gestore chiavi.");
        if(localSessions.Count>=30)throw new IOException("Massimo trenta sessioni locali aperte.");var session=new LocalTerminal();localSessions.Add(id,session);
        session.Output+=bytes=>Dispatcher.BeginInvoke(()=>{if(localSessions.TryGetValue(id,out var current)&&ReferenceEquals(current,session)){RecordOutput(id,bytes);Emit("output",new{sessionId=id,data=Convert.ToBase64String(bytes)});};});session.Exited+=code=>Dispatcher.BeginInvoke(async()=>{await FinishNativeTask(profile.Id,code);await EndNative(id);if(localSessions.TryGetValue(id,out var current)&&ReferenceEquals(current,session))Emit("localStatus",new{sessionId=id,connected=false,text="Processo terminato · codice "+code});});
        try{if(profile.Protocol=="native"){if(!nativeTaskLaunches.ContainsKey(profile.Id))await NativeClient.VerifyCapabilities(profile);if(profile.Native.ConfigFile.Length>0&&!Confirm("Aprire il config OpenSSH selezionato? Include, Match exec, ProxyCommand e altri comandi del file saranno gestiti dal client OpenSSH. Usa soltanto configurazioni di cui ti fidi.\n\n"+profile.Native.ConfigFile))throw new OperationCanceledException();var nativeLaunch=nativeTaskLaunches.Remove(profile.Id,out var prepared)?prepared:await PrepareNative(profile,id,startupMacro);nativeLaunch.SetHome(Path.Combine(dataPath,"NativeHome"));await session.StartExecutable(nativeLaunch,Path.Combine(dataPath,"LocalLaunch"),I(payload,"cols",100),I(payload,"rows",30));}else await session.Start(profile,Path.Combine(dataPath,"LocalLaunch"),I(payload,"cols",100),I(payload,"rows",30));return new{sessionId=id,filesAvailable=false,local=true,running=session.Running};}catch{localSessions.Remove(id);await session.DisposeAsync();await EndNative(id);throw;}
    }
    async Task CloseLocal(string id){if(localSessions.Remove(id,out var session)){await session.DisposeAsync();await EndNative(id);Emit("localStatus",new{sessionId=id,connected=false,text="Sessione locale chiusa"});}}
    async Task CloseAllLocal(){foreach(var id in localSessions.Keys.ToArray())await CloseLocal(id);}
    void ConfirmLocalChanges(ConnectionProfile profile,ConnectionProfile? old){if(profile.Protocol=="local"&&profile.StartupCommands.Length>0&&(old?.Protocol!="local"||old?.StartupCommands!=profile.StartupCommands||old?.LocalShell!=profile.LocalShell||old?.WslDistribution!=profile.WslDistribution)&&!Confirm("Eseguire questi comandi a ogni apertura di «"+profile.Name+"», anche da workspace?\n\nShell: "+profile.LocalShell+"\n\n"+profile.StartupCommands+"\n\nI comandi sono salvati nel profilo in chiaro: non inserire password o token."))throw new OperationCanceledException();}
}
