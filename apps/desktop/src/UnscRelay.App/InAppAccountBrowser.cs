using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using UnscRelay.Core;
namespace UnscRelay.App;

// Identity content has its own profile and no access to the Relay bridge or host objects.
internal sealed class InAppAccountBrowser(MainWindow owner,string directory,Action<CoreWebView2>? diagnostic=null) : IBrowser
{
    public Task<BrowserResult> InvokeAsync(BrowserOptions options,CancellationToken cancellationToken=default) =>
        owner.Dispatcher.InvokeAsync(()=>Show(options,cancellationToken)).Task.Unwrap();
    async Task<BrowserResult> Show(BrowserOptions options,CancellationToken token)
    {
        if(!DesktopAccountNavigation.IsIdentityUrl(options.StartUrl)||options.EndUrl!=CloudAccountClient.Callback)throw new InvalidOperationException("Destinazione identità non autorizzata.");
        var done=new TaskCompletionSource<BrowserResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var root=(Grid)owner.Content;
        var layout=new Grid{Margin=new Thickness(24),Background=new SolidColorBrush(Color.FromRgb(9,21,23))};
        layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());
        var header=new DockPanel{Margin=new Thickness(18,12,18,12)};
        var close=new Button{Content=owner.NativeText("Annulla e torna a Relay"),Padding=new Thickness(16,10,16,10),HorizontalAlignment=HorizontalAlignment.Right,MinHeight=44,Background=new SolidColorBrush(Color.FromRgb(190,210,146)),Foreground=Brushes.Black};
        close.Click+=(_,_)=>done.TrySetResult(new BrowserResult{ResultType=BrowserResultType.UserCancel});DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        var title=new TextBlock{Text=owner.NativeText("UNSC RELAY  /  ACCOUNT PERSONALE"),Foreground=new SolidColorBrush(Color.FromRgb(190,210,146)),VerticalAlignment=VerticalAlignment.Center,FontSize=17};header.Children.Add(title);
        layout.Children.Add(header);var view=new WebView2{DefaultBackgroundColor=System.Drawing.Color.FromArgb(7,17,20)};Grid.SetRow(view,1);layout.Children.Add(view);
        var previousVisibility=owner.Browser.Visibility;
        // WebView2 uses a native child window: hide it while the identity view owns the surface.
        owner.Browser.Visibility=Visibility.Hidden;
        var overlay=new Border{Name="AccountBrowserOverlay",Background=new SolidColorBrush(Color.FromRgb(7,17,20)),Child=layout};Panel.SetZIndex(overlay,100);root.Children.Add(overlay);
        System.ComponentModel.CancelEventHandler ownerClosing=(_,_)=>done.TrySetResult(new BrowserResult{ResultType=BrowserResultType.UserCancel});
        owner.Closing+=ownerClosing;
        using var registration=token.Register(()=>done.TrySetResult(new BrowserResult{ResultType=BrowserResultType.UserCancel}));
        try
        {
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(directory,"IdentityWebView"),new CoreWebView2EnvironmentOptions("--mute-audio"));
            await view.EnsureCoreWebView2Async(environment);var core=view.CoreWebView2;core.IsMuted=true;
            core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;
            core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.DownloadStarting+=(_,e)=>{e.Cancel=true;e.Handled=true;};
            core.ServerCertificateErrorDetected+=(_,e)=>e.Action=CoreWebView2ServerCertificateErrorAction.Cancel;
            core.NavigationStarting+=(_,e)=>{
                if(DesktopAccountNavigation.IsCallback(e.Uri)){e.Cancel=true;done.TrySetResult(new BrowserResult{ResultType=BrowserResultType.Success,Response=e.Uri});return;}
                if(!DesktopAccountNavigation.IsIdentityUrl(e.Uri)){e.Cancel=true;title.Text=owner.NativeText("Link esterno bloccato · resta nell’accesso UNSC");}
            };
            core.NewWindowRequested+=(_,e)=>{e.Handled=true;if(DesktopAccountNavigation.IsIdentityUrl(e.Uri))core.Navigate(e.Uri);};
            core.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess)done.TrySetResult(new BrowserResult{ResultType=BrowserResultType.UnknownError,Error="Non riesco a caricare la schermata di accesso. Riprova."});};
            core.ProcessFailed+=(_,_)=>done.TrySetResult(new BrowserResult{ResultType=BrowserResultType.UnknownError,Error="La schermata di accesso si è interrotta. Riprova."});
            diagnostic?.Invoke(core);core.Navigate(options.StartUrl);view.Focus();return await done.Task;
        }
        finally
        {
            // No SSO cookies survive into another user's login or the system browser.
            if(view.CoreWebView2 is {} core){try{await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);}catch{} }
            owner.Closing-=ownerClosing;root.Children.Remove(overlay);view.Dispose();owner.Browser.Visibility=previousVisibility;owner.Browser.Focus();
        }
    }
}
