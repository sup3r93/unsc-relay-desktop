using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace UnscRelay.App;
sealed class PermissionReportWindow:Window
{
 readonly WebView2 view=new();readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);bool closed;
 public PermissionReportWindow(Window owner,string title){Owner=owner;Title=title;Width=850;Height=680;MinWidth=400;MinHeight=350;WindowStartupLocation=WindowStartupLocation.CenterOwner;Content=view;Closed+=(_,_)=>{closed=true;ready.TrySetCanceled();view.Dispose();};}
 public async Task<byte[]> Render(string directory,string html)
 {
  if(closed)throw new OperationCanceledException();var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(directory,"WebView","PermissionReports"));if(closed)throw new OperationCanceledException();var options=environment.CreateCoreWebView2ControllerOptions();options.IsInPrivateModeEnabled=true;options.ProfileName="PermissionReports";await view.EnsureCoreWebView2Async(environment,options);if(closed)throw new OperationCanceledException();var core=view.CoreWebView2;core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.IsScriptEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.NewWindowRequested+=(_,e)=>e.Handled=true;core.DownloadStarting+=(_,e)=>e.Cancel=true;core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
  core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);core.WebResourceRequested+=(_,e)=>{if(e.Request.Uri.StartsWith("http:",StringComparison.OrdinalIgnoreCase)||e.Request.Uri.StartsWith("https:",StringComparison.OrdinalIgnoreCase)||e.Request.Uri.StartsWith("file:",StringComparison.OrdinalIgnoreCase))e.Response=environment.CreateWebResourceResponse(null,403,"Blocked","");};core.NavigationStarting+=(_,e)=>{if(ready.Task.IsCompleted)e.Cancel=true;};core.NavigationCompleted+=(_,e)=>{if(e.IsSuccess)ready.TrySetResult();else if(e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)ready.TrySetException(new IOException("Impossibile preparare il riepilogo: "+e.WebErrorStatus+"."));};core.NavigateToString(html);await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));if(closed)throw new OperationCanceledException();
  var result=await core.CallDevToolsProtocolMethodAsync("Page.printToPDF","{\"printBackground\":true,\"preferCSSPageSize\":true,\"generateTaggedPDF\":true,\"generateDocumentOutline\":true}");if(closed)throw new OperationCanceledException();using var document=System.Text.Json.JsonDocument.Parse(result);var encoded=document.RootElement.GetProperty("data").GetString()??throw new IOException("PDF non disponibile.");if(encoded.Length>32*1024*1024)throw new IOException("Riepilogo troppo grande.");return Convert.FromBase64String(encoded);
 }
}
