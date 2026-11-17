using System.Windows;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e) { base.OnStartup(e); if(NativeSftpTransport.IsInvocation(e.Args)){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{Shutdown(await NativeSftpTransport.Run(e.Args));}catch{Shutdown(1);}return;} if (e.Args.FirstOrDefault() == "--native-bridge" || Environment.GetEnvironmentVariable("RELAY_ASKPASS_HELPER") == "1" && Environment.GetEnvironmentVariable("RELAY_NATIVE_PIPE") != null) { ShutdownMode = ShutdownMode.OnExplicitShutdown; Shutdown(await NativeBridge.RunHelper(e.Args.FirstOrDefault() == "--native-bridge" ? e.Args.Skip(1).ToArray() : ["ask", .. e.Args])); return; } if(e.Args.Length==2&&e.Args[0]=="--account-switch-wait"){
 ShutdownMode=ShutdownMode.OnExplicitShutdown;
 try{if(!int.TryParse(e.Args[1],out var pid)||pid==Environment.ProcessId)throw new InvalidOperationException();try{using var parent=System.Diagnostics.Process.GetProcessById(pid);if(!string.Equals(parent.MainModule?.FileName,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));await parent.WaitForExitAsync(deadline.Token);}catch(ArgumentException){} }
 catch{Shutdown(1);return;}
 ShutdownMode=ShutdownMode.OnLastWindowClose;
 }
 try { var window = new MainWindow(); MainWindow = window; window.Show(); } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { MessageBox.Show(ex.Message, "UNSC Relay · Cartella dati non disponibile", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); } }
}
