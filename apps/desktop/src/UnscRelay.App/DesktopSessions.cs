using System.IO;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using RemoteViewing.Vnc;
using RemoteViewing.Windows.Forms;
using UnscRelay.Core;
using Forms = System.Windows.Forms;

namespace UnscRelay.App;

internal sealed class RdpView : Forms.AxHost
{
    static readonly Guid EventsId = new("336D5562-EFA8-482E-8CB3-C5C0FC7A7DB6");
    readonly Action<string, bool> status;
    readonly List<(int Id, Delegate Handler)> handlers = [];
    public RdpView(Action<string, bool> status) : base("A0C63C30-F08D-4AB4-907C-34905D770C7D") { this.status = status; }
    object Component => GetOcx() ?? throw new InvalidOperationException("Controllo RDP non inizializzato.");
    public string ComponentVersion => (string)((dynamic)Component).Version;
    internal (string Host, int Port, bool Nla, int Authentication) EndpointSettings()
    {
        dynamic client = Component; dynamic settings = client.AdvancedSettings9;
        return ((string)client.Server, (int)settings.RDPPort, (bool)settings.EnableCredSspSupport, (int)settings.AuthenticationLevel);
    }
    internal object AdvancedSnapshot(){dynamic client=Component;dynamic settings=client.AdvancedSettings9;dynamic gateway=client.TransportSettings2;return new{gateway=(string)gateway.GatewayHostname,gatewayMode=(uint)gateway.GatewayUsageMethod,drives=(bool)settings.RedirectDrives,printers=(bool)settings.RedirectPrinters,smartCards=(bool)settings.RedirectSmartCards,ports=(bool)settings.RedirectPorts,audio=(uint)settings.AudioRedirectionMode};}
    protected override void CreateSink()
    {
        base.CreateSink();
        Hook(2, new Action(() => status("RDP connesso", true)));
        Hook(1, new Action(() => status("RDP: connessione in corso…", false)));
        Hook(3, new Action(() => status("Accesso RDP completato", true)));
        Hook(4, new Action<int>(reason => status("RDP disconnesso · codice " + reason, false)));
        Hook(10, new Action<int>(error => status("Errore RDP · codice " + error, false)));
    }
    void Hook(int id, Delegate handler) { ComEventsHelper.Combine(Component, EventsId, id, handler); handlers.Add((id, handler)); }
    protected override void DetachSink()
    {
        foreach (var (id, handler) in handlers) { try { ComEventsHelper.Remove(Component, EventsId, id, handler); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { } }
        handlers.Clear(); base.DetachSink();
    }
    public void Configure(ConnectionProfile profile, string password, int width, int height)
    {
        dynamic client = Component; client.Server = profile.Host; client.UserName = profile.Username; client.Domain = profile.Domain;
        client.DesktopWidth = Math.Clamp(width, 640, 7680); client.DesktopHeight = Math.Clamp(height, 480, 4320); client.ColorDepth = 32;
        dynamic settings = client.AdvancedSettings9;
        settings.RDPPort = profile.Port; settings.EnableCredSspSupport = true; settings.AuthenticationLevel = 2;
        settings.EnableAutoReconnect = false;
        settings.singleConnectionTimeout = profile.ConnectTimeoutSeconds; settings.overallConnectionTimeout = profile.ConnectTimeoutSeconds;
        settings.RedirectDrives = profile.Rdp.RedirectDrives; settings.RedirectPrinters = profile.Rdp.RedirectPrinters; settings.RedirectSmartCards=profile.Rdp.RedirectSmartCards; settings.RedirectPorts=profile.Rdp.RedirectPorts; settings.AudioRedirectionMode=(uint)profile.Rdp.AudioMode; settings.RedirectClipboard = profile.ShareClipboard; settings.SmartSizing = true;
        dynamic transport=client.TransportSettings2;
        transport.GatewayUsageMethod=profile.Rdp.Gateway.Length>0?1u:0u;
        transport.GatewayProfileUsageMethod=1u;
        if(profile.Rdp.Gateway.Length>0){transport.GatewayHostname=profile.Rdp.Gateway;transport.GatewayCredsSource=0u;transport.GatewayCredSharing=0u;transport.GatewayUsername=profile.Rdp.GatewayUsername;transport.GatewayDomain=profile.Rdp.GatewayDomain;}
        settings.ClearTextPassword = password;
    }
    public void Connect(ConnectionProfile profile, string password, int width, int height) { Configure(profile, password, width, height); ((dynamic)Component).Connect(); }
    public void Disconnect() { try { ((dynamic)Component).Disconnect(); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { } }
}
internal sealed class DesktopSession : IDisposable
{
    public required WindowsFormsHost Host { get; init; }
    public RdpView? Rdp { get; init; }
    public VncControl? Vnc { get; init; }
    public TcpClient? Socket { get; set; }
    public IDisposable? Route { get; set; }
    public System.Diagnostics.Process? ExternalProcess {get;set;}
    public string? ExternalFile {get;set;}
    public bool Disposed { get; private set; }
    public void Dispose()
    {
        if (Disposed) return; Disposed = true;
        if(ExternalProcess!=null){try{if(!ExternalProcess.HasExited)ExternalProcess.CloseMainWindow();}catch(InvalidOperationException){}ExternalProcess.Dispose();}if(ExternalFile!=null&&File.Exists(ExternalFile))File.Delete(ExternalFile);
        Socket?.Dispose(); Rdp?.Disconnect(); Vnc?.Client?.Close(); Host.Dispose(); Route?.Dispose();
    }
}
public partial class MainWindow
{
    readonly Dictionary<string, DesktopSession> desktopSessions = [];
    readonly Dictionary<string, Rect> desktopRects = [];
    readonly Dictionary<string,string> desktopWindows=[];
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    void DesktopLayout(JsonElement payload)
    {
        desktopRects.Clear();desktopWindows.Clear();
        if (!payload.TryGetProperty("rects", out var rects) || rects.GetArrayLength() > 30) return;
        foreach (var item in rects.EnumerateArray())
        {
            var id = S(item, "id");var windowId=S(item,"windowId");
            var targetBrowser=windowId.Length==0?Browser:detachedWindows.TryGetValue(windowId,out var floating)?floating.Browser:null;if(targetBrowser==null)continue;
            desktopWindows[id]=windowId;
            double Read(string name) => item.TryGetProperty(name, out var value) && value.TryGetDouble(out var n) && double.IsFinite(n) ? n : 0;
            var left = Math.Clamp(Read("left"), 0, targetBrowser.ActualWidth); var top = Math.Clamp(Read("top"), 0, targetBrowser.ActualHeight);
            var width = Math.Clamp(Read("width"), 0, Math.Max(0, targetBrowser.ActualWidth - left)); var height = Math.Clamp(Read("height"), 0, Math.Max(0, targetBrowser.ActualHeight - top));
            desktopRects[id] = new(left, top, width, height);
        }
        ApplyDesktopLayout();
    }
    void ApplyDesktopLayout()
    {
        foreach (var (id, session) in desktopSessions)
        {
            if (!desktopRects.TryGetValue(id, out var rect) || rect.Width < 10 || rect.Height < 10) { session.Host.Visibility = Visibility.Hidden; continue; }
            var windowId=desktopWindows.GetValueOrDefault(id,"");var layer=windowId.Length==0?DesktopLayer:detachedWindows.TryGetValue(windowId,out var floating)?floating.DesktopLayer:null;
            if(layer==null){session.Host.Visibility=Visibility.Hidden;continue;}
            if(session.Host.Parent is Canvas oldLayer&&oldLayer!=layer)oldLayer.Children.Remove(session.Host);if(session.Host.Parent==null)layer.Children.Add(session.Host);
            Canvas.SetLeft(session.Host, rect.Left); Canvas.SetTop(session.Host, rect.Top); session.Host.Width = rect.Width; session.Host.Height = rect.Height; session.Host.Visibility = Visibility.Visible;
            session.Host.Child?.BringToFront();
            if (session.Host.Handle != IntPtr.Zero) SetWindowPos(session.Host.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x13);
        }
    }
    async Task<object> ConnectDesktop(ConnectionProfile profile, string id)
    {
        DesktopRoutes.Validate(profile, data.Profiles);
        var password = profile.Protocol=="rdp"&&profile.Rdp.MultiMonitor?"":ConnectionPassword(profile);
        var route = await DesktopRoutes.OpenAsync(profile, data.Profiles, (p, host, targetPort) => CreateAuthenticatedSession(p, host, targetPort, Guid.NewGuid().ToString("N")));
        var endpoint = route.Host; var port = route.Port;
        var hostView = new WindowsFormsHost { Width = 800, Height = 500 }; DesktopSession? session = null;
        void Status(string text, bool connected) { if (session?.Disposed != true) Emit("desktopStatus", new { sessionId = id, text, connected, pending = text.StartsWith("RDP: connessione", StringComparison.Ordinal) }); }
        try
        {
            if(profile.Protocol=="rdp"&&profile.Rdp.MultiMonitor)
            {
                if(!Confirm("Aprire RDP su piu monitor nel client Microsoft?\nLe credenziali saranno richieste da Windows. La scheda Relay gestisce il collegamento e l'eventuale tunnel SSH.\nLa verifica dei certificati resta attiva."))throw new OperationCanceledException();
                var folder=Path.Combine(dataPath,"RdpLaunch");Directory.CreateDirectory(folder);var file=Path.Combine(folder,Guid.NewGuid().ToString("N")+".rdp");
                File.WriteAllText(file,RdpDocument.Create(profile,endpoint,port),System.Text.Encoding.Unicode);
                var info=new System.Diagnostics.ProcessStartInfo(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.System),"mstsc.exe")){UseShellExecute=false};info.ArgumentList.Add(file);info.ArgumentList.Add("/multimon");
                session=new DesktopSession{Host=hostView,Route=route,ExternalFile=file};session.ExternalProcess=System.Diagnostics.Process.Start(info)??throw new IOException("Client Microsoft non avviato.");desktopSessions.Add(id,session);var external=session;
                _=Task.Run(async()=>{try{await external.ExternalProcess.WaitForExitAsync();}catch(Exception ex)when(ex is InvalidOperationException or ObjectDisposedException){}await Dispatcher.InvokeAsync(()=>{if(!external.Disposed){Status("Client Microsoft terminato",false);DisconnectDesktop(id);}});});
                Status("RDP aperto nel client Microsoft / piu monitor",false);return new {sessionId=id,filesAvailable=false,desktop=true,pending=false,external=true};
            }
            else if (profile.Protocol == "rdp")
            {
                var control = new RdpView(Status) { Dock = Forms.DockStyle.Fill };
                ((ISupportInitialize)control).BeginInit(); hostView.Child = control;
                session = new DesktopSession { Host = hostView, Rdp = control, Route = route };
                DesktopLayer.Children.Add(hostView); desktopSessions.Add(id, session); ApplyDesktopLayout(); hostView.UpdateLayout();
                ((ISupportInitialize)control).EndInit(); control.CreateControl();
                control.Connect(profile with { Host = endpoint, Port = port }, password, (int)hostView.Width, (int)hostView.Height);
            }
            else
            {
                var control = new VncControl { Dock = Forms.DockStyle.Fill, AllowInput = !profile.ViewOnly, AllowClipboardSharingFromServer = profile.ShareClipboard, AllowClipboardSharingToServer = profile.ShareClipboard, SizeMode = VncControlSizeMode.Zoom };
                hostView.Child = control; session = new DesktopSession { Host = hostView, Vnc = control, Route = route };
                DesktopLayer.Children.Add(hostView); desktopSessions.Add(id, session); ApplyDesktopLayout(); hostView.UpdateLayout(); control.CreateControl();
                control.Connected += (_, _) => Status("VNC connesso", true); control.ConnectionFailed += (_, _) => Status("Connessione VNC non riuscita: verifica password e compatibilità del server.", false); control.Closed += (_, _) => Status("VNC disconnesso", false);
                var socket = new TcpClient { ReceiveTimeout = profile.ConnectTimeoutSeconds * 1000, SendTimeout = profile.ConnectTimeoutSeconds * 1000 }; session.Socket = socket;
                await socket.ConnectAsync(endpoint, port).WaitAsync(TimeSpan.FromSeconds(profile.ConnectTimeoutSeconds));
                var options = new VncClientConnectOptions { Password = password.ToCharArray(), AutoReconnect = false, ShareDesktop = true };
                try { await Task.Run(() => control.Client.Connect(socket.GetStream(), options)); }
                finally { Array.Clear(options.Password); password = ""; }
            }
            return new { sessionId = id, filesAvailable = false, desktop = true, pending = profile.Protocol == "rdp" };
        }
        catch
        {
            desktopSessions.Remove(id); DesktopLayer.Children.Remove(hostView);
            if (session != null) session.Dispose(); else { hostView.Dispose(); route?.Dispose(); }
            throw;
        }
    }
    void DisconnectDesktop(string id)
    {
        if (!desktopSessions.Remove(id, out var session)) return;
        if(session.Host.Parent is Canvas ownerLayer)ownerLayer.Children.Remove(session.Host); session.Dispose(); desktopRects.Remove(id);
    }
}
