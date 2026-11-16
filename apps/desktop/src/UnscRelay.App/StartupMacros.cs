using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    string MacroDestination(ConnectionProfile profile)=>profile.Protocol=="local"?profile.LocalShell+(profile.WslDistribution.Length>0?" / "+profile.WslDistribution:""):profile.Protocol=="native"?profile.Native.Target:profile.Username+"@"+profile.Host+":"+profile.Port;
    void ConfirmStartupMacroChanges(ConnectionProfile profile,ConnectionProfile? old)
    {
        if(profile.StartupMacro?.Enabled!=true)return;
        if(old?.StartupMacro?.Enabled==true&&StartupMacros.ApprovalKey(profile)==StartupMacros.ApprovalKey(old))return;
        var command=StartupMacros.Prepare(profile)!;if(profile.Protocol=="local")StartupMacros.LocalProfile(profile,command);
        if(!Confirm("Abilitare la macro «"+profile.StartupMacro.Name+"» all’apertura di «"+profile.Name+"»?\n\nDestinazione: "+MacroDestination(profile)+"\n\n"+command+"\n\nLa macro viene eseguita, anche aprendo un workspace. Non parte nelle riconnessioni automatiche o in SFTP. I valori sono salvati in chiaro nel profilo: non inserire password o token."))throw new OperationCanceledException();
    }
    string? PrepareStartupMacro(ConnectionProfile profile,bool automatic)
    {
        var command=StartupMacros.Prepare(profile,automatic);if(command==null)return null;
        if(profile.StartupMacro!.ConfirmEachLaunch&&!Confirm("Eseguire la macro «"+profile.StartupMacro.Name+"» in questa nuova sessione?\n\n"+MacroDestination(profile)+"\n\n"+command))throw new OperationCanceledException();
        return command;
    }
    async Task RunStartupMacro(SshSession session,string? command)
    {
        if(command==null)return;
        try{if(!session.CanWrite)throw new InvalidOperationException("Il terminale non è disponibile.");await session.WriteInputAsync(command.Replace("\n","\r").TrimEnd('\r')+"\r");Emit("notice",new{text="Macro di avvio inviata: "+session.Profile.StartupMacro?.Name+". L’esito dei comandi è nel terminale."});}
        catch(Exception ex) when(ex is InvalidOperationException or System.IO.IOException or Renci.SshNet.Common.SshException){Emit("notice",new{text="Macro di avvio non completata: "+ex.Message+". Non verrà ripetuta automaticamente."});}
    }
}
