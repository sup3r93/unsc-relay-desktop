using System.IO;
using System.Text.Json;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Web.WebView2.Core;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckAccountBrowser()
    {
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var checks=new List<bool>();
        var browser=new InAppAccountBrowser(this,dataPath,core=>{
            checks.Add(core.IsMuted&&!core.Settings.IsWebMessageEnabled&&!core.Settings.AreHostObjectsAllowed&&!core.Settings.AreDevToolsEnabled);
            core.NavigationCompleted+=async(_,e)=>{
                if(!e.IsSuccess)return;
                try{
                    var visible=await core.ExecuteScriptAsync("!!document.querySelector('input[name=username]') && !!document.querySelector('input[type=password]')");
                    if(visible!="true")return;
                    checks.Add(DesktopAccountNavigation.IsIdentityUrl(core.Source));
                    checks.Add(System.Windows.Application.Current.Windows.Count==1);
                    checks.Add(Browser.Visibility==System.Windows.Visibility.Hidden && ((System.Windows.Controls.Grid)Content).Children.OfType<System.Windows.Controls.Border>().Any(b=>b.Name=="AccountBrowserOverlay"&&b.IsVisible&&b.ActualWidth>500));
                    var screenshot=Path.Combine(AppContext.BaseDirectory,"account-browser.png");
                    using(var output=File.Create(screenshot))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                    cancel.Cancel();
                }catch{cancel.Cancel();}
            };
        });
        var start=CloudAccountClient.Authority+"/protocol/openid-connect/auth?client_id=unsc-desktop&redirect_uri="+Uri.EscapeDataString(CloudAccountClient.Callback)+"&response_type=code&scope=openid%20profile%20email&state=diagnostic-no-login&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&code_challenge_method=S256&prompt=login";
        try{
            var result=await browser.InvokeAsync(new BrowserOptions(start,CloudAccountClient.Callback),cancel.Token);
            checks.Add(result.ResultType==BrowserResultType.UserCancel);
            var passed=checks.Count==5&&checks.All(v=>v);
            AtomicFile.Write(Path.Combine(AppContext.BaseDirectory,"account-browser-checks.json"),JsonSerializer.SerializeToUtf8Bytes(new{passed,checks}));CompleteSmoke(passed);
        }catch{CompleteSmoke(false);}
    }
}
