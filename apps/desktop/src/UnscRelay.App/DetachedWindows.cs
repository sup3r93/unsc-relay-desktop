using System.IO;
using UnscRelay.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace UnscRelay.App;
internal sealed class DetachedWindow : Window
{
    public WebView2 Browser {get;}=new(){DefaultBackgroundColor=System.Drawing.Color.FromArgb(9,16,19)};
    public Canvas DesktopLayer {get;}=new();
    public bool PermitClose;
    public WindowArea? LastNormalArea;
    public WindowState LastNonMinimizedState=WindowState.Normal;
    void TrackBounds(){if(IsLoaded&&WindowState==WindowState.Normal)LastNormalArea=WorkspaceWindowPosition.Read(this);}
    public DetachedWindow(){Title="UNSC Relay · finestra separata";Width=1250;Height=820;MinWidth=850;MinHeight=580;Background=new SolidColorBrush(Color.FromRgb(9,16,19));WindowStartupLocation=WindowStartupLocation.CenterScreen;var grid=new Grid();grid.Children.Add(Browser);grid.Children.Add(DesktopLayer);Content=grid;LocationChanged+=(_,_)=>TrackBounds();SizeChanged+=(_,_)=>TrackBounds();StateChanged+=(_,_)=>{if(WindowState!=WindowState.Minimized)LastNonMinimizedState=WindowState;};}
}
public partial class MainWindow
{
    readonly Dictionary<string,DetachedWindow> detachedWindows=[];
    async void OpenDetachedWindow(object? sender,CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled=true;
        if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)||uri.GetLeftPart(UriPartial.Path)!=Origin+"/detached.html"||!Guid.TryParse(uri.Fragment.TrimStart('#'),out _)||detachedWindows.Count>=10||nativePresentationMode||CriticalUpdateRequired||closing||updating)return;
        var id=uri.Fragment.TrimStart('#');if(detachedWindows.ContainsKey(id))return;
        using var deferral=e.GetDeferral();var window=new DetachedWindow();detachedWindows.Add(id,window);
        try
        {
            var restored=pendingWindowPlacements.Remove(id,out var pendingPlacement)&&pendingPlacement.Expires>DateTime.UtcNow;
            if(restored)window.WindowStartupLocation=WindowStartupLocation.Manual;
            window.Show();if(restored&&WorkspaceWindowPosition.Apply(window,pendingPlacement.Placement))Emit("notice",new{text="Monitor del workspace non disponibile: finestra riportata su un monitor collegato."});await window.Browser.EnsureCoreWebView2Async(Browser.CoreWebView2.Environment);var core=window.Browser.CoreWebView2;if(nativePresentationMode){presentationHiddenWindows.Add(id);window.Hide();window.DesktopLayer.Visibility=Visibility.Collapsed;}
            core.SetVirtualHostNameToFolderMapping("relay.local",Path.Combine(AppContext.BaseDirectory,"Web"),CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            core.NewWindowRequested+=(_,args)=>args.Handled=true;core.PermissionRequested+=(_,args)=>args.State=CoreWebView2PermissionState.Deny;
            core.NavigationStarting+=(_,args)=>{if(!args.Uri.StartsWith(Origin+"/detached.html#",StringComparison.Ordinal))args.Cancel=true;};
            core.WindowCloseRequested+=(_,_)=>window.Close();window.Closing+=async(_,args)=>
            {
                if(window.PermitClose||exitAllowed)return;args.Cancel=true;
                try{var result=await Browser.CoreWebView2.ExecuteScriptAsync("reattachDetachedPane("+JsonSerializer.Serialize(id)+")");if(result!="true")throw new IOException("Scheda non ancora pronta: riprova tra un istante.");window.PermitClose=true;window.Close();if(!closing){if(WindowState==WindowState.Minimized)WindowState=WindowState.Maximized;Activate();}}
                catch(Exception ex){Emit("notice",new{text="Impossibile riportare la scheda: "+ex.Message});}
            };
            window.Closed+=(_,_)=>{detachedWindows.Remove(id);window.Browser.Dispose();lastDetachedLayoutCleanup(id);};e.NewWindow=core;
        }
        catch(Exception ex){window.PermitClose=true;window.Close();detachedWindows.Remove(id);Emit("notice",new{text="Finestra separata non disponibile: "+ex.Message});}
    }
    void lastDetachedLayoutCleanup(string id){foreach(var key in desktopWindows.Where(k=>k.Value==id).Select(k=>k.Key).ToArray()){desktopWindows.Remove(key);desktopRects.Remove(key);}ApplyDesktopLayout();}
    object? DetachedAction(string action,JsonElement payload)
    {
        if(action=="activateMain"){if(WindowState==WindowState.Minimized)WindowState=WindowState.Maximized;Activate();return null;}
        if(!detachedWindows.TryGetValue(S(payload,"id"),out var window))return null;
        if(action=="detachedFocus"&&!nativePresentationMode){if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Activate();}
        if(action=="detachedClose")window.Close();return null;
    }
    void CloseDetachedWindows(){foreach(var window in detachedWindows.Values.ToArray()){window.PermitClose=true;window.Close();}detachedWindows.Clear();}
}
