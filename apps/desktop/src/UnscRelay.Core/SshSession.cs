using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;

namespace UnscRelay.Core;
public sealed partial class SshSession : IDisposable
{
    private readonly SshClient _ssh;
    private readonly SftpClient _sftp;
    private ShellStream? _shell;
    private readonly object _shellReadGate=new();
    private readonly SemaphoreSlim _terminalInput=new(1,1);
    private readonly SemaphoreSlim _files = new(1, 1);
    private readonly Dictionary<string, ForwardedPort> _tunnels = [];
    private readonly List<IDisposable> _authResources = [];
    private readonly List<ForwardedPortLocal> _routePorts = [];
    private SshSession? _route;
    private bool _interactiveClosed;
    private bool _disposed;
    private bool _silentFileRecovery;
    public event Action<Exception>? TransportInterrupted;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ConnectionProfile Profile { get; }
    public event Action<byte[]>? Output;
    public event Action<string>? Status;
    bool RouteHealthy=>_route==null||_route.TransportHealthy;
    bool TransportHealthy=>!_disposed&&_ssh.IsConnected&&RouteHealthy;
    public bool Connected => !_disposed && RouteHealthy && (Profile.Protocol == "sftp" ? _sftp.IsConnected : _ssh.IsConnected && !_interactiveClosed);
    public Exception? LastFilesError {get;private set;}
    public bool FilesAvailable => !_disposed && RouteHealthy && _sftp.IsConnected;

    public SshSession(ConnectionProfile profile, string password, byte[]? key, string passphrase, HostKeyStore hosts, Func<HostKey, bool> trust, Func<string, string> challenge, string certificate = "", string? endpointHost = null, int endpointPort = 0)
    {
        Profile = profile; profile.Validate();
        if (!string.IsNullOrWhiteSpace(profile.JumpHost) && endpointHost == null) throw new InvalidOperationException("Il profilo richiede il percorso jump host configurato.");
        if (endpointHost != null && (endpointHost != "127.0.0.1" || endpointPort is < 1 or > 65535)) throw new ArgumentException("Endpoint del tunnel SSH non valido.");
        IPrivateKeySource[]? agentKeys = null;
        if (profile.Authentication is "openssh-agent" or "pageant")
        {
            var agent = AgentKeys.Open(profile.Authentication);
            agentKeys = agent.RequestIdentities().Where(k => profile.AgentFingerprint.Length == 0 || AgentKeys.Fingerprint(k) == profile.AgentFingerprint).ToArray();
            if (agentKeys.Length == 0) throw new InvalidOperationException("Nessuna chiave disponibile nell'agent selezionato. Controlla agent, token e impronta.");
        }
        IPrivateKeySource? loginKey = null;
        if (key != null && profile.Authentication is "auto" or "key")
        {
            var source = new PrivateKeyFile(new MemoryStream(key, false), passphrase, certificate.Length > 0 ? new MemoryStream(Encoding.UTF8.GetBytes(certificate)) : null);
            _authResources.Add(source); loginKey = new ModernKeySource(source, certificate.Length > 0);
        }
        ConnectionInfo Info(bool silent=false)
        {
            var methods = new List<AuthenticationMethod>();
            if (agentKeys != null) methods.Add(new PrivateKeyAuthenticationMethod(profile.Username, agentKeys));
            if (loginKey != null) methods.Add(new PrivateKeyAuthenticationMethod(profile.Username, loginKey));
            if (profile.Authentication == "password" || (profile.Authentication == "auto" && password.Length > 0)) methods.Add(new PasswordAuthenticationMethod(profile.Username, password));
            var keyboard = new KeyboardInteractiveAuthenticationMethod(profile.Username);
            keyboard.AuthenticationPrompt += (_, e) => { foreach (var prompt in e.Prompts) prompt.Response = (silent||_silentFileRecovery) ? throw new InvalidOperationException("Autenticazione interattiva richiesta: riconnetti manualmente.") : challenge(prompt.Request); };
            if (profile.Authentication is "auto" or "keyboard-interactive" || profile.KeyboardFallback) methods.Add(keyboard);
            if (methods.Count == 0) throw new ArgumentException("Il metodo di autenticazione selezionato non dispone di credenziali.");
            var info = new ConnectionInfo(endpointHost ?? profile.Host, endpointHost == null ? profile.Port : endpointPort, profile.Username, methods.ToArray()) { Timeout = TimeSpan.FromSeconds(profile.ConnectTimeoutSeconds), Encoding = Encoding.UTF8 };
            info.HostKeyAlgorithms.Remove("ssh-rsa");
            if (profile.RequirePostQuantumKex)
            {
                foreach (var algorithm in info.KeyExchangeAlgorithms.Keys.Where(a => !a.StartsWith("mlkem", StringComparison.Ordinal) && !a.StartsWith("sntrup", StringComparison.Ordinal)).ToArray()) info.KeyExchangeAlgorithms.Remove(algorithm);
                if (info.KeyExchangeAlgorithms.Count == 0) throw new NotSupportedException("Il motore non offre scambio chiavi post-quantistico.");
            }
            return info;
        }
        _fileChannel = silent => new SftpClient(Info(silent)) { SubsystemCommand = profile.Subsystem, KeepAliveInterval = TimeSpan.FromSeconds(profile.KeepAliveSeconds), OperationTimeout = TimeSpan.FromSeconds(45) };
        _ssh = new SshClient(Info()) { KeepAliveInterval = TimeSpan.FromSeconds(profile.KeepAliveSeconds) };
        _sftp = new SftpClient(Info()) { SubsystemCommand = profile.Subsystem, KeepAliveInterval = TimeSpan.FromSeconds(profile.KeepAliveSeconds), OperationTimeout = TimeSpan.FromSeconds(45) };
        _fileTrust = (e,silent) =>
        {
            var entry = new HostKey(profile.Host, profile.Port, e.HostKeyName, e.FingerPrintSHA256, DateTime.UtcNow, NativeIdentity.CanonicalAlgorithm(e.HostKeyName)+" "+Convert.ToBase64String(e.HostKey));
            var state = hosts.Check(entry.Host, entry.Port, entry.Algorithm, entry.Fingerprint);
            e.CanTrust = state == HostKeyState.Trusted; if(e.CanTrust)hosts.Accept(entry);
            if (state == HostKeyState.Unknown && !silent && !_silentFileRecovery && trust(entry)) { hosts.Accept(entry); e.CanTrust = true; }
        };
        foreach (var client in new BaseClient[] { _ssh, _sftp })
        {
            client.HostKeyReceived += (_, e) =>
            {
                var entry = new HostKey(profile.Host, profile.Port, e.HostKeyName, e.FingerPrintSHA256, DateTime.UtcNow, NativeIdentity.CanonicalAlgorithm(e.HostKeyName)+" "+Convert.ToBase64String(e.HostKey));
                var state = hosts.Check(entry.Host, entry.Port, entry.Algorithm, entry.Fingerprint);
                e.CanTrust = state == HostKeyState.Trusted; if(e.CanTrust)hosts.Accept(entry);
                if (state == HostKeyState.Unknown && !_silentFileRecovery && trust(entry)) { hosts.Accept(entry); e.CanTrust = true; }
                if (state == HostKeyState.Changed) Status?.Invoke("CHIAVE HOST CAMBIATA: connessione rifiutata. Verifica l'impronta nel gestore host.");
            };
            client.ErrorOccurred += (_, e) => { Status?.Invoke(e.Exception.Message); if(NetworkReconnect.IsTransient(e.Exception)) TransportInterrupted?.Invoke(e.Exception); };
        }
    }
    public async Task ConnectAsync(int columns, int rows)
    {
        try
        {
            if (Profile.Protocol != "sftp")
            {
            await _ssh.ConnectAsync(CancellationToken.None);
            _shell = _ssh.CreateShellStream("xterm-256color", (uint)Math.Clamp(columns, 20, 500), (uint)Math.Clamp(rows, 5, 300), 0, 0, 65536);
            _shell.DataReceived += (_, _) => DrainShellOutput();
            _shell.Closed += (_, _) => { _interactiveClosed = true; Status?.Invoke("Sessione terminata"); };
            DrainShellOutput();
            if (Profile.TmuxSession.Length > 0) _shell.WriteLine("tmux new-session -A -s " + Profile.TmuxSession);
            }
            try { await _sftp.ConnectAsync(CancellationToken.None); }
            catch (Exception ex) when (Profile.Protocol != "sftp") { LastFilesError=ex; Status?.Invoke("SSH connesso; SFTP non disponibile: " + ex.Message); }
            foreach (var tunnel in Profile.Tunnels.Where(t => t.Automatic && Profile.Protocol != "sftp")) SetTunnel(tunnel, true);
        }
        catch { Dispose(); throw; }
    }
    public async Task ReconnectFilesAsync(bool automatic = true)
    {
        await _files.WaitAsync();
        try
        {
            if (_disposed) throw new InvalidOperationException("Sessione chiusa.");
            _silentFileRecovery = automatic;
            try { if(_sftp.IsConnected)_sftp.Disconnect(); await _sftp.ConnectAsync(CancellationToken.None); }
            finally { _silentFileRecovery = false; }
        }
        finally { _files.Release(); }
    }
    void DrainShellOutput()
    {
        lock(_shellReadGate){var shell=_shell;if(shell==null)return;try{while(shell.DataAvailable){var bytes=new byte[(int)Math.Min(shell.Length,65536)];if(bytes.Length==0)break;var read=shell.Read(bytes,0,bytes.Length);if(read==0)break;if(read!=bytes.Length)Array.Resize(ref bytes,read);Output?.Invoke(bytes);}}catch(ObjectDisposedException){}}
    }
    public async Task WriteInputAsync(string value)
    {
        await _terminalInput.WaitAsync();try{await Task.Run(()=>Write(value));}finally{_terminalInput.Release();}
    }
    public async Task ResizeTerminalAsync(int columns,int rows)
    {
        await _terminalInput.WaitAsync();try{await Task.Run(()=>Resize(columns,rows));}finally{_terminalInput.Release();}
    }
    public bool CanWrite => Connected && _shell is { CanWrite: true };
    public void Write(string value) { if (_shell is { CanWrite: true }) _shell.Write(value); }
    internal Task ConnectTransportAsync() => _ssh.ConnectAsync(CancellationToken.None);
    internal void OwnRoute(SshSession? route) => _route = route;
    internal int ForwardTo(string host, int port)
    {
        var forward = new ForwardedPortLocal("127.0.0.1", 0, host, (uint)port);
        try { _ssh.AddForwardedPort(forward); forward.Start(); _routePorts.Add(forward); return (int)forward.BoundPort; }
        catch { _ssh.RemoveForwardedPort(forward); forward.Dispose(); throw; }
    }
    public void Resize(int columns, int rows) => _shell?.ChangeWindowSize((uint)Math.Clamp(columns, 20, 500), (uint)Math.Clamp(rows, 5, 300), 0, 0);
    public async Task<object[]> ListAsync(string path)
    {
        await _files.WaitAsync();
        try
        {
            return await Task.Run(() => _sftp.ListDirectory(path).Where(f => f.Name != ".").OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name)
                .Select(f => (object)new { name = f.Name, path = f.FullName, directory = f.IsDirectory, link = f.IsSymbolicLink, size = f.Length, modified = f.LastWriteTimeUtc, permissions = FilePresentation.Permissions(f.Attributes), owner = f.Attributes.UserId, group = f.Attributes.GroupId }).ToArray());
        }
        finally { _files.Release(); }
    }
    public async Task TransferAsync(string local, string remote, bool upload, bool resume, Action<ulong, long> progress, CancellationToken cancellation)
    {
        var request = new TransferRequest(Guid.NewGuid().ToString("N"), local, remote, upload, resume ? "resume" : "replace", resume);
        try { await TransferAdvancedAsync(request, progress, cancellation); }
        catch { try { await DiscardPartialAsync(request); } catch { } throw; }
    }
    public void SetTunnel(TunnelRule rule, bool enabled)
    {
        rule.Validate();
        if (_tunnels.Remove(rule.Id, out var existing)) { existing.Stop(); _ssh.RemoveForwardedPort(existing); existing.Dispose(); }
        if (!enabled) return;
        ForwardedPort port = rule.Kind switch
        {
            "local" => new ForwardedPortLocal("127.0.0.1", rule.LocalPort, rule.Destination, rule.DestinationPort),
            "remote" => new ForwardedPortRemote("127.0.0.1", rule.LocalPort, rule.Destination, rule.DestinationPort),
            "socks" => new ForwardedPortDynamic("127.0.0.1", rule.LocalPort),
            _ => throw new ArgumentException("Tipo tunnel non valido.")
        };
        try { _ssh.AddForwardedPort(port); port.Start(); _tunnels[rule.Id] = port; }
        catch { _ssh.RemoveForwardedPort(port); port.Dispose(); throw; }
    }
    public object[] TunnelStatus() => Profile.Tunnels.Select(t => (object)new { rule = t, active = _tunnels.TryGetValue(t.Id, out var p) && p.IsStarted }).ToArray();
    public bool TunnelActive(string id) => _tunnels.TryGetValue(id, out var port) && port.IsStarted;
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (var port in _tunnels.Values) port.Dispose(); _tunnels.Clear();
        foreach (var port in _routePorts) port.Dispose(); _routePorts.Clear();
        _shell?.Dispose(); _sftp.Dispose(); _ssh.Dispose(); foreach (var resource in _authResources) resource.Dispose();
        _route?.Dispose(); _route = null;
    }
}
