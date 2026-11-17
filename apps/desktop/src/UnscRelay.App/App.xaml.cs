using System.Windows;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e) { base.OnStartup(e); if(NativeSftpTransport.IsInvocation(e.Args)){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{Shutdown(await NativeSftpTransport.Run(e.Args));}catch{Shutdown(1);}return;} if (e.Args.FirstOrDefault() == "--native-bridge" || Environment.GetEnvironmentVariable("RELAY_ASKPASS_HELPER") == "1" && Environment.GetEnvironmentVariable("RELAY_NATIVE_PIPE") != null) { ShutdownMode = ShutdownMode.OnExplicitShutdown; Shutdown(await NativeBridge.RunHelper(e.Args.FirstOrDefault() == "--native-bridge" ? e.Args.Skip(1).ToArray() : ["ask", .. e.Args])); return; } var window = new MainWindow(); MainWindow = window; window.Show(); }
}
