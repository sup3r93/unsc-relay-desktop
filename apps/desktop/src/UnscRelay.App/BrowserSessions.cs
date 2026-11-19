using System.IO;
using System.Text.Json;
using System.Windows.Forms.Integration;
using Microsoft.Web.WebView2.Core;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task<object> ConnectBrowser(ConnectionProfile profile,string id)
    {
        var generation=authenticationGeneration;RequireOperationSession(generation);profile.Validate();var host=new WindowsFormsHost{Width=800,Height=500};var control=new Microsoft.Web.WebView2.WinForms.WebView2{Dock=System.Windows.Forms.DockStyle.Fill};host.Child=control;var session=new DesktopSession{Host=host};
        try{
            DesktopLayer.Children.Add(host);desktopSessions.Add(id,session);ApplyDesktopLayout();host.UpdateLayout();control.CreateControl();
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(dataPath,"NativeBrowser"));await control.EnsureCoreWebView2Async(environment);RequireOperationSession(generation);
            // Untrusted web content has neither the app bridge nor host objects.
            control.CoreWebView2.Settings.IsWebMessageEnabled=false;control.CoreWebView2.Settings.AreHostObjectsAllowed=false;
            control.CoreWebView2.NavigationStarting+=(_,e)=>{if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https"))e.Cancel=true;};
            control.CoreWebView2.NewWindowRequested+=(_,e)=>{e.Handled=true;if(!OperationsLocked&&generation==authenticationGeneration&&Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https")control.CoreWebView2.Navigate(uri.AbsoluteUri);};
            control.CoreWebView2.NavigationCompleted+=(_,e)=>{if(!session.Disposed)Emit("desktopStatus",new{sessionId=id,connected=e.IsSuccess,text=e.IsSuccess?"Browser / pagina caricata":"Browser / errore "+e.WebErrorStatus,pending=false});};
            RequireOperationSession(generation);control.CoreWebView2.Navigate(profile.Native.Target);return new{sessionId=id,filesAvailable=false,desktop=true,pending=true};
        }catch{desktopSessions.Remove(id);DesktopLayer.Children.Remove(host);session.Dispose();throw;}
    }
}
