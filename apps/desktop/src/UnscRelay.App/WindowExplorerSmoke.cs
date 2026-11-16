using System.IO;
using System.Text.Json;
using System.Windows.Forms.Integration;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckWindowExplorer()
    {
        var checks=new Dictionary<string,bool>();string? error=null;
        JsonElement P(object value)=>JsonSerializer.SerializeToElement(value,ProfileStore.Json);
        async Task<bool> JS(string script)=>await Browser.CoreWebView2.ExecuteScriptAsync(script)=="true";
        try
        {
            var root=Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")??throw new IOException("Fixture missing");File.WriteAllText(Path.Combine(root,"drag-fixture.txt"),"native export fixture");Directory.CreateDirectory(Path.Combine(root,"drag-folder"));File.WriteAllText(Path.Combine(root,"drag-folder","nested.txt"),"nested fixture");
            var profile=new ConnectionProfile{Name="Windows fixture",Host="127.0.0.1",Port=int.Parse(Environment.GetEnvironmentVariable("RELAY_FILE_PORT")!),Username="test",Protocol="ssh",Authentication="password"};
            var sid=Guid.NewGuid().ToString();var session=new SshSession(profile,"relay-test-password",null,"",hosts,_=>true,_=>""){Id=sid};sessions.Add(sid,session);await session.ConnectAsync(100,30);session.Output+=bytes=>Emit("output",new{sessionId=sid,data=Convert.ToBase64String(bytes)});
            var sidJson=JsonSerializer.Serialize(sid);await Browser.CoreWebView2.ExecuteScriptAsync("(()=>{const pane=toolPane("+JsonSerializer.Serialize(profile,ProfileStore.Json)+",'Windows fixture','fixture',"+sidJson+");pane.surface.className='terminal-surface';pane.surface.style.flex='1';pane.term=new Terminal({fontFamily:'Consolas',fontSize:13,scrollback:1000});pane.fit=new FitAddon.FitAddon();pane.term.loadAddon(pane.fit);pane.term.open(pane.surface);pane.connected=true;pane.term.onData(data=>rpc('input',{sessionId:pane.id,data}));pane.term.writeln('BEFORE DETACH');detachPane(pane)})()");
            for(int i=0;i<40&&!await JS("!!panes.get("+sidJson+")?.detachedWindow");i++)await Task.Delay(100);
            checks["nativePopupAdoptsSamePane"]=detachedWindows.ContainsKey(sid)&&await JS("panes.get("+sidJson+").container.ownerDocument!==document");
            var child=detachedWindows[sid];WindowState=System.Windows.WindowState.Minimized;session.Write("AFTER DETACH ECHO\r");await Task.Delay(400);
            checks["outputWhileMainMinimized"]=await JS("(()=>{const t=panes.get("+sidJson+").term;return Array.from({length:t.buffer.active.length},(_,i)=>t.buffer.active.getLine(i).translateToString()).join('').includes('AFTER DETACH ECHO')})()");
            checks["sameSshTransportStillConnected"]=ReferenceEquals(sessions[sid],session)&&session.CanWrite;
            WindowState=System.Windows.WindowState.Maximized;
            var nativeHost=new WindowsFormsHost{Child=new System.Windows.Forms.Panel()};DesktopLayer.Children.Add(nativeHost);desktopSessions.Add(sid,new(){Host=nativeHost});
            DesktopLayout(P(new{rects=new[]{new{id=sid,windowId=sid,left=10,top=80,width=600,height=350}}}));checks["nativeDesktopHostMovesToChildLayer"]=nativeHost.Parent==child.DesktopLayer;
            var initial=WorkspaceWindowPosition.Capture(child,0);var target=initial with{X=.12,Y=.14,Width=.5,Height=.55,Maximized=false};WorkspaceWindowPosition.Apply(child,target);await Task.Delay(150);var captured=WorkspaceWindowPosition.Capture(child,0);
            checks["nativeWindowPositionApplied"]=Math.Abs(captured.X-target.X)<.01&&Math.Abs(captured.Y-target.Y)<.01;
            child.WindowState=System.Windows.WindowState.Maximized;await Task.Delay(150);var maximized=WorkspaceWindowPosition.Capture(child,0);checks["maximizedPreservesNormalGeometry"]=maximized.Maximized&&Math.Abs(maximized.X-captured.X)<.01;
            child.WindowState=System.Windows.WindowState.Minimized;var minimized=WorkspaceWindowPosition.Capture(child,0);checks["minimizedRemembersVisibleState"]=minimized.Maximized;
            var workspace=new Workspace("Native fixture","grid",[profile.Id],["Native fixture"],["session"],[maximized],0,[0,-1,-1,-1],[.63,.42]);workspace.Validate();data.Profiles.Add(profile);data.Workspaces.RemoveAll(w=>w.Name==workspace.Name);data.Workspaces.Add(workspace);store.Save(data);var loaded=store.Load().Workspaces.Single(w=>w.Name==workspace.Name);checks["nativeWorkspacePersistsGeometry"]=loaded.WindowPlacements![0]==maximized&&loaded.SplitRatios!.SequenceEqual(workspace.SplitRatios!);
            child.Close();for(int i=0;i<30&&detachedWindows.ContainsKey(sid);i++)await Task.Delay(100);
            checks["windowCloseReturnsPaneWithoutDisconnect"]=!detachedWindows.ContainsKey(sid)&&await JS("panes.get("+sidJson+").container.ownerDocument===document")&&session.CanWrite;
            WorkspaceWindowAction("workspaceWindowPrepare",P(new{id=sid,placement=loaded.WindowPlacements![0]}));await Browser.CoreWebView2.ExecuteScriptAsync("detachPane(panes.get("+sidJson+"))");for(int i=0;i<40&&!await JS("!!panes.get("+sidJson+")?.detachedWindow");i++)await Task.Delay(100);var restored=detachedWindows[sid];var restoredBounds=WorkspaceWindowPosition.Capture(restored,0);checks["preparedWindowRestoresMaximizedGeometry"]=restoredBounds.Maximized&&Math.Abs(restoredBounds.X-maximized.X)<.01;
            checks["restoreKeepsSameSessionAndPane"]=session.CanWrite&&await JS("panes.get("+sidJson+").container.ownerDocument!==document");restored.Close();for(int i=0;i<30&&detachedWindows.ContainsKey(sid);i++)await Task.Delay(100);
            DesktopLayout(P(new{rects=new[]{new{id=sid,windowId="",left=10,top=180,width=600,height=350}}}));checks["nativeDesktopHostReturnsToMain"]=nativeHost.Parent==DesktopLayer;DisconnectDesktop(sid);
            await PrepareExplorerExport(P(new{sessionId=sid,paths=new[]{"/drag-fixture.txt","/drag-folder"}}));var export=exportWindows.Single();checks["downloadPreparationCompletes"]=!export.Busy&&!ExportBusy;
            var cache=Directory.GetDirectories(Path.Combine(dataPath,"DragExports")).OrderByDescending(Directory.GetCreationTimeUtc).First();
            checks["fileAndDirectoryDownloadedExactly"]=File.ReadAllText(Path.Combine(cache,"drag-fixture.txt"))=="native export fixture"&&File.ReadAllText(Path.Combine(cache,"drag-folder","nested.txt"))=="nested fixture";
            var fileDrop=ExportDragWindow.CreateFileDrop([Path.Combine(cache,"drag-fixture.txt"),Path.Combine(cache,"drag-folder")]);checks["nativeWindowsFileDropContainsRealPaths"]=fileDrop.GetDataPresent(System.Windows.DataFormats.FileDrop)&&(fileDrop.GetData(System.Windows.DataFormats.FileDrop) is string[] dropPaths && dropPaths.Length==2);
            checks["remoteOriginalsUnchanged"]=File.ReadAllText(Path.Combine(root,"drag-fixture.txt"))=="native export fixture";
            if(Environment.GetEnvironmentVariable("RELAY_DRAG_PREVIEW")=="1"){var closed=new TaskCompletionSource();export.Closed+=(_,_)=>closed.TrySetResult();WindowState=System.Windows.WindowState.Minimized;export.Activate();await Task.WhenAny(closed.Task,Task.Delay(TimeSpan.FromSeconds(160)));}
            export.Close();var canceled=PrepareExplorerExport(P(new{sessionId=sid,paths=new[]{"/drag-fixture.txt"}}));exportWindows.Single().Close();await canceled;checks["closingPreparationCancelsAndReleasesBusy"]=!ExportBusy&&exportWindows.Count==0;
        }
        catch(Exception ex){error=ex.ToString();}
        finally{CloseDetachedWindows();CloseExportWindows();foreach(var session in sessions.Values)session.Dispose();sessions.Clear();}
        var success=error==null&&checks.Count==17&&checks.Values.All(v=>v);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"windows-explorer-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error},ProfileStore.Json));CompleteSmoke(success);
    }
}
