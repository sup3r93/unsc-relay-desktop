using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckLocalSessions(){var checks=new Dictionary<string,bool>();string? error=null;
    async Task<bool> JS(string script)=>await Browser.CoreWebView2.ExecuteScriptAsync(script)=="true";
    async Task Wait(string script){for(int i=0;i<180&&!await JS(script);i++)await Task.Delay(50);if(!await JS(script))throw new IOException("Local UI condition timed out: "+script);}
    try{
        var working=Path.Combine(dataPath,"fixture working folder");Directory.CreateDirectory(working);var cmd=new ConnectionProfile{Name="CMD locale",Protocol="local",LocalShell="cmd",StartupDirectory=working,StartupCommands="echo NATIVE-CMD-START"};var ps=new ConnectionProfile{Name="PowerShell locale",Protocol="local",LocalShell="powershell",StartupDirectory=working,StartupCommands="Write-Output 'NATIVE-PS-START'"};data.Profiles=[cmd,ps];store.Save(data);
        await Browser.CoreWebView2.ExecuteScriptAsync("update("+JsonSerializer.Serialize(Snapshot(),ProfileStore.Json)+");window.localSmokeOpen=connect(state.data.profiles[0]);");
        const string allText="Array.from({length:panes.values().next().value.term.buffer.active.length},(_,i)=>panes.values().next().value.term.buffer.active.getLine(i).translateToString()).join('\\n')";
        await Wait("panes.size===1&&panes.values().next().value.connected&&("+allText+").includes('NATIVE-CMD-START')");checks["cmdStartsInEmbeddedTab"]=true;var id=localSessions.Keys.Single();var terminal=localSessions[id];var idJson=JsonSerializer.Serialize(id);
        await Browser.CoreWebView2.ExecuteScriptAsync("panes.get("+idJson+").term.paste('echo NATIVE-^INTERACTIVE\\r');");await Wait("("+allText+").includes('NATIVE-INTERACTIVE')");checks["xtermInputReachesLocalProcess"]=true;
        await Browser.CoreWebView2.ExecuteScriptAsync("window.localSmokeSecond=connect(state.data.profiles[1]);");await Wait("panes.size===2&&[...panes.values()].every(p=>p.connected)");await Browser.CoreWebView2.ExecuteScriptAsync("setSplitLayout('columns');");checks["localTabsShareSplitLayout"]=await JS("document.querySelectorAll('#terminals .terminal-pane').length===2&&[...panes.values()].every(p=>p.container.style.display==='flex')");
        await Browser.CoreWebView2.ExecuteScriptAsync("detachPane(panes.get("+idJson+"));");await Wait("!!panes.get("+idJson+").detachedWindow");checks["localPaneDetachesWithoutRestart"]=ReferenceEquals(localSessions[id],terminal)&&terminal.Running;
        terminal.Write("echo FLOAT-^ALIVE\r");await Wait("("+allText+").includes('FLOAT-ALIVE')");checks["floatingLocalTerminalKeepsOutput"]=true;
        var placement=WorkspaceWindowPosition.Capture(detachedWindows[id],0);var workspace=new Workspace("Local fixture","columns",[cmd.Id,ps.Id],["CMD saved","PS saved"],["session","session"],[placement,null],1,[1,-1],[.62,.5]);workspace.Validate();data.Workspaces.Add(workspace);store.Save(data);checks["localProfilesAndWorkspacePersist"]=store.Load().Profiles.All(p=>p.Protocol=="local")&&store.Load().Workspaces.Any(w=>w.Name=="Local fixture");
        await Browser.CoreWebView2.ExecuteScriptAsync("window.nativeRestore=restoreWorkspace("+JsonSerializer.Serialize(workspace,ProfileStore.Json)+");");await Wait("panes.size===4&&!workspaceRestoring&&[...panes.values()].filter(p=>p.detachedWindow).length===2");checks["localWorkspaceReopensShellsAndWindows"]=localSessions.Count==4&&localSessions.Values.All(s=>s.Running);
        await Browser.CoreWebView2.ExecuteScriptAsync("reattachDetachedPane("+idJson+");");checks["localPaneReturnsWithoutRestart"]=ReferenceEquals(localSessions[id],terminal)&&await JS("panes.get("+idJson+").container.ownerDocument===document");
        checks["noSshVaultOrNetworkSessionNeeded"]=sessions.Count==0&&!vault.IsUnlocked;var ids=localSessions.Keys.ToArray();await CloseAllLocal();await Wait("[...panes.values()].every(p=>!p.connected)");checks["localClosureUpdatesUiAndStopsProcesses"]=localSessions.Count==0&&ids.Length==4;
    }catch(Exception ex){error=ex.ToString();}finally{await CloseAllLocal();CloseDetachedWindows();}
    var success=error==null&&checks.Count==10&&checks.Values.All(x=>x);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"local-sessions-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error},ProfileStore.Json));CompleteSmoke(success);}
}
