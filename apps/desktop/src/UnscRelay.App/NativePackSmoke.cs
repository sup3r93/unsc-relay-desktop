using System.IO;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using System.Windows.Forms.Integration;
using Microsoft.Web.WebView2.WinForms;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckNativePack()
    {
        var checks=new Dictionary<string,bool>();string? error=null;
        try{
            var rdp=new RdpView((_,_)=>{});((ISupportInitialize)rdp).BeginInit();using(var host=new WindowsFormsHost{Child=rdp,Width=800,Height=600}){
                DesktopLayer.Children.Add(host);host.UpdateLayout();((ISupportInitialize)rdp).EndInit();rdp.CreateControl();
                rdp.Configure(new ConnectionProfile{Protocol="rdp",Host="server.invalid",Username="fixture",Authentication="password",Rdp=new RdpOptions{Gateway="gateway.invalid",GatewayUsername="fixture",GatewayDomain="LAB",RedirectDrives=true,RedirectPrinters=true,RedirectPorts=true,RedirectSmartCards=true,AudioMode=0}},"",800,600);
                var options=JsonSerializer.SerializeToElement(rdp.AdvancedSnapshot());checks["realGatewayForced"]=options.GetProperty("gateway").GetString()=="gateway.invalid"&&options.GetProperty("gatewayMode").GetUInt32()==1;
                checks["realRedirectionsApplied"]=new[]{"drives","printers","ports","smartCards"}.All(n=>options.GetProperty(n).GetBoolean())&&options.GetProperty("audio").GetUInt32()==0;
                checks["nlaAndCertificateChecksPreserved"]=rdp.EndpointSettings().Nla&&rdp.EndpointSettings().Authentication==2;DesktopLayer.Children.Remove(host);
            }
            var profile=new ConnectionProfile{Name="Native fixture",Protocol="native",Authentication="auto",Native=new NativeOptions{Kind="bash",ArgumentsJson=JsonSerializer.Serialize(new[]{"--noprofile","--norc","-c","printf BUNDLED-NATIVE-OK"})}};var output=new StringBuilder();await using(var terminal=new LocalTerminal()){
                terminal.Output+=b=>{lock(output)output.Append(Encoding.UTF8.GetString(b));};using var launch=NativeClient.Launch(profile);launch.SetHome(Path.Combine(dataPath,"NativeHome"));await terminal.StartExecutable(launch,Path.Combine(dataPath,"launch"),100,30);for(var i=0;i<100;i++){lock(output)if(output.ToString().Contains("BUNDLED-NATIVE-OK"))break;await Task.Delay(50);}lock(output)checks["bundledProgramRunsInConPty"]=output.ToString().Contains("BUNDLED-NATIVE-OK");
            }
            var browser=new ConnectionProfile{Protocol="native",Native=new NativeOptions{Kind="browser",Target="http://127.0.0.1:1"}};var id=Guid.NewGuid().ToString("N");await ConnectBrowser(browser,id);var web=(WebView2)desktopSessions[id].Host.Child;checks["webContentCannotUseAppBridge"]=!web.CoreWebView2.Settings.IsWebMessageEnabled&&!web.CoreWebView2.Settings.AreHostObjectsAllowed;DisconnectDesktop(id);checks["browserDisposedOnTabClose"]=!desktopSessions.ContainsKey(id);
            var baseInfo=new DirectoryInfo(AppContext.BaseDirectory);var languages=Path.Combine(baseInfo.Name.Equals("Librerie",StringComparison.OrdinalIgnoreCase)?baseInfo.Parent!.FullName:baseInfo.FullName,"languages");
            if(Directory.Exists(languages)){var satellite=Directory.GetFiles(languages,"*.resources.dll",SearchOption.AllDirectories).First();var name=System.Reflection.AssemblyName.GetAssemblyName(satellite);var loaded=System.Reflection.Assembly.Load(name);checks["languagesLoadFromDedicatedFolder"]=Path.GetFullPath(loaded.Location).StartsWith(Path.GetFullPath(languages),StringComparison.OrdinalIgnoreCase);}else checks["languagesLoadFromDedicatedFolder"]=false;
        }catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;}
        var success=error==null&&checks.Count==7&&checks.Values.All(v=>v);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"native-pack-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error},ProfileStore.Json));CompleteSmoke(success);
    }
}
