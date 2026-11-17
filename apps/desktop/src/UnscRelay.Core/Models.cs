using System.Text.Json;

namespace UnscRelay.Core;

public sealed record ConnectionProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nuova connessione";
    public string Folder { get; set; } = "Connessioni";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string Protocol { get; set; } = "ssh";
    public NativeOptions Native { get; set; } = new();
    public RdpOptions Rdp { get; set; } = new();
    public StorageOptions Storage {get;set;} = new();
    public string LocalShell { get; set; } = "cmd";
    public string StartupDirectory { get; set; } = "";
    public string StartupCommands { get; set; } = "";
    public StartupMacro? StartupMacro { get; set; }
    public string WslDistribution { get; set; } = "";
    public string Environment { get; set; } = "sviluppo";
    public string KeyId { get; set; } = "";
    public string Authentication { get; set; } = "auto";
    public string PasswordId { get; set; } = "";
    public bool KeyboardFallback { get; set; }
    public string Domain { get; set; } = "";
    public bool ViewOnly { get; set; }
    public bool ShareClipboard { get; set; }
    public int KeepAliveSeconds { get; set; } = 25;
    public int ConnectTimeoutSeconds { get; set; } = 20;
    public string AgentFingerprint { get; set; } = "";
    public bool RequirePostQuantumKex { get; set; }
    public string RemoteDirectory { get; set; } = ".";
    public string LocalDirectory { get; set; } = "";
    public bool RememberFileDirectories { get; set; } = true;
    public string Notes { get; set; } = "";
    public string Tags { get; set; } = "";
    public string JumpHost { get; set; } = "";
    public string Subsystem { get; set; } = "";
    public string TmuxSession { get; set; } = "";
    public bool Favorite { get; set; }
    public List<ConnectionHook> Hooks { get; set; } = [];
    public List<TunnelRule> Tunnels { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100) throw new ArgumentException("Indica un nome (massimo 100 caratteri).");
        if (Protocol is not ("local" or "native") && (string.IsNullOrWhiteSpace(Host) || Host.Any(char.IsWhiteSpace) || Host.Any(char.IsControl) || Host.StartsWith('-'))) throw new ArgumentException("Indica un host valido.");
        if (Port is < 1 or > 65535) throw new ArgumentException("Porta non valida.");
        if (KeepAliveSeconds is <0 or >3600) throw new ArgumentException("Keep-alive: da 0 (disattivato) a 3600 secondi.");
        if (ConnectTimeoutSeconds is < 5 or > 120) throw new ArgumentException("Timeout connessione: da 5 a 120 secondi.");
        if (string.IsNullOrWhiteSpace(Username) && Protocol is "ssh" or "sftp") throw new ArgumentException("Indica l'utente SSH.");
        if (Username.Any(char.IsControl)) throw new ArgumentException("Utente non valido.");
        if (Protocol is not ("ssh" or "sftp" or "rdp" or "vnc" or "local" or "native")) throw new ArgumentException("Protocollo non supportato.");
        if (Native == null || Rdp == null || Storage == null || Hooks == null || Tunnels == null) throw new ArgumentException("Opzioni sessione mancanti.");
        if (Protocol == "native") {Native.Validate();Storage.Validate(Native.Kind);}
        if(Storage.Browser&&(Protocol!="native"||string.IsNullOrWhiteSpace(Username)||JumpHost.Length>0||KeyId.Length>0||Hooks.Count>0||Tunnels.Count>0))throw new ArgumentException("Browser storage: utente/access key richiesto, senza chiavi SSH, hook o tunnel.");
        if(Storage.Browser){
            if(Authentication!="password"||Native.Executable.Length>0||Native.ConfigFile.Length>0||Native.IdentityFile.Length>0||Native.ArgumentsJson!="[]"||Native.Target.Length>0||RequirePostQuantumKex||Subsystem.Length>0||StartupMacro!=null)throw new ArgumentException("Browser storage: usa i campi dedicati e il motore incluso, senza argomenti o config esterni.");
            if(Native.Kind=="ftp"&&(string.IsNullOrWhiteSpace(Host)||Host.Length>255||Host.Any(char.IsWhiteSpace)||Host.Any(char.IsControl)||Host.StartsWith('-')||Host.Contains('/')||Host.Contains('\\')))throw new ArgumentException("Host FTP non valido.");
            StorageFiles.Normalize(RemoteDirectory);
        }
        if (Protocol == "rdp") Rdp.Validate(JumpHost);
        if (Authentication is not ("auto" or "password" or "key" or "keyboard-interactive" or "openssh-agent" or "pageant")) throw new ArgumentException("Metodo di autenticazione non supportato.");
        if(LocalShell is not("cmd" or "powershell" or "pwsh" or "wsl")||StartupDirectory==null||StartupDirectory.Length>4096||StartupDirectory.Any(char.IsControl)||StartupCommands==null||StartupCommands.Length>8192||StartupCommands.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t'))||WslDistribution==null||WslDistribution.Length>256||WslDistribution.Any(char.IsControl)||WslDistribution.StartsWith('-'))throw new ArgumentException("Configurazione shell locale non valida.");
        StartupMacros.Validate(this);
        if(Hooks==null||Tunnels==null)throw new ArgumentException("Regole del profilo non valide.");
        if((Protocol=="local"||Protocol=="native"&&Native.Kind is not("ssh" or "sftp")&&!Storage.Browser)&&(KeyId.Length>0||PasswordId.Length>0||JumpHost.Length>0||Tunnels.Count>0||Hooks.Any(h=>h.Enabled)))throw new ArgumentException("Una sessione locale non usa credenziali SSH, tunnel o hook remoti.");
        if(Protocol=="native"&&(JumpHost.Length>0||Tunnels.Count>0))throw new ArgumentException("Per i client nativi usa jump, tunnel e comandi nel config OpenSSH.");
        if (Protocol=="native" && Authentication is not("auto" or "key" or "password")) throw new ArgumentException("Per OpenSSH nativo scegli file config, chiave o password.");
        if (Authentication == "key" && string.IsNullOrWhiteSpace(KeyId)) throw new ArgumentException("Seleziona una chiave SSH.");
        if (Protocol is "rdp" or "vnc" && Authentication is not ("auto" or "password")) throw new ArgumentException("RDP e VNC richiedono il metodo Password.");
        if (Domain.Any(char.IsControl)) throw new ArgumentException("Dominio non valido.");
        if (TmuxSession == null || TmuxSession.Length > 50 || TmuxSession.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))) throw new ArgumentException("Nome tmux: massimo 50 lettere ASCII, numeri, _ o -.");
        if (Subsystem.Length > 4096 || Subsystem.Any(char.IsControl)) throw new ArgumentException("Comando subsystem: massimo 4096 caratteri, su una sola riga.");
        if(Hooks==null||Hooks.Count>6)throw new ArgumentException("Massimo 6 hook per connessione.");
        foreach(var hook in Hooks)hook.Validate();
        if(Hooks.Select(h=>h.Phase+":"+h.Kind).Distinct().Count()!=Hooks.Count)throw new ArgumentException("Un solo hook per evento e destinazione locale/remota.");
        if(Hooks.Any(h=>h.Enabled)&&Protocol is not("ssh" or "sftp")&&!(Protocol=="native"&&Native.Kind is "ssh" or "sftp"))throw new ArgumentException("Hook disponibili per SSH e SFTP.");
        if(Protocol=="native"&&Hooks.Any(h=>h.Enabled)&&Native.Executable.Length>0)throw new ArgumentException("Gli hook OpenSSH richiedono il client incluso.");
        foreach (var tunnel in Tunnels) tunnel.Validate();
        if (Tunnels.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != Tunnels.Count) throw new ArgumentException("Le regole tunnel devono avere identificatori distinti.");
    }
}
public sealed record TunnelRule(string Id, string Kind, uint LocalPort, string Destination, uint DestinationPort, bool Automatic = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 100) throw new ArgumentException("Identificatore tunnel non valido.");
        if (Kind is not ("local" or "remote" or "socks") || LocalPort is < 1 or > 65535 || DestinationPort > 65535) throw new ArgumentException("Tipo o porta tunnel non validi.");
        if (Kind != "socks" && (DestinationPort == 0 || string.IsNullOrWhiteSpace(Destination) || Destination.Any(char.IsWhiteSpace) || Destination.Any(char.IsControl))) throw new ArgumentException("Destinazione tunnel non valida.");
    }
}
public sealed record Snippet(string Id, string Name, string Text, string Folder = "Personali", string Shortcut = "");
public sealed record MachineAddress(string Id, string Name, string Host, int Port)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 100 || string.IsNullOrWhiteSpace(Name) || Name.Length > 100) throw new ArgumentException("Indica un nome per la macchina (massimo 100 caratteri).");
        new ConnectionProfile { Name = Name, Host = Host, Port = Port, Username = "check" }.Validate();
    }
}
public sealed record Workspace(string Name, string Layout, List<string> ProfileIds, List<string>? TabNames = null, List<string>? PaneKinds = null, List<WorkspaceWindow?>? WindowPlacements = null, int? ActivePaneIndex = null, List<int>? SplitPaneIndices = null, List<double>? SplitRatios = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100 || Layout is not ("tabs" or "columns" or "rows" or "grid") || ProfileIds == null || ProfileIds.Count > 30 || ProfileIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Workspace non valido.");
        if (PaneKinds != null && (PaneKinds.Count != ProfileIds.Count || PaneKinds.Any(k => k is not ("sftp" or "session")))) throw new ArgumentException("Tipi schede workspace non validi.");
        if (TabNames != null && (TabNames.Count != ProfileIds.Count || TabNames.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 100))) throw new ArgumentException("I nomi delle schede non corrispondono al workspace.");
        if(WindowPlacements!=null){if(WindowPlacements.Count!=ProfileIds.Count||WindowPlacements.Count(p=>p!=null)>10)throw new ArgumentException("Le finestre non corrispondono alle schede del workspace (massimo dieci separate).");foreach(var placement in WindowPlacements)placement?.Validate();}
        if(SplitPaneIndices!=null&&(SplitPaneIndices.Count>(Layout=="grid"?4:2)||SplitPaneIndices.Any(i=>i< -1||i>=ProfileIds.Count)||SplitPaneIndices.Where(i=>i>=0).Distinct().Count()!=SplitPaneIndices.Count(i=>i>=0)))throw new ArgumentException("Riquadri workspace non validi.");
        if(SplitRatios!=null&&(SplitRatios.Count!=2||SplitRatios.Any(v=>!double.IsFinite(v)||v<0.1||v>0.9)))throw new ArgumentException("Proporzioni workspace non valide.");
        if(ActivePaneIndex!=null&&(ActivePaneIndex<0||ActivePaneIndex>=ProfileIds.Count))throw new ArgumentException("Scheda attiva del workspace non valida.");
    }
}
public sealed record UserSettings(bool StartupVideo = true, bool ShutdownVideo = true, double StartupVolume = .8, double ShutdownVolume = 1, int AutoLockMinutes = 10, int RecordingRetentionDays = 0, bool RequireAppAuthentication = false, bool AssistantApiEnabled = false, bool AppMusicEnabled = true, double AppMusicVolume = .35, string ExternalEditor = "", string EditorPreference = "", bool CloseToTray = false, bool CheckUpdatesOnStartup = true, bool CheckUpdatesDaily = true);
public sealed class AppData
{
    public Dictionary<string, FileLocation> FileLocations { get; set; } = [];
    public List<CertificateProvider> CertificateProviders { get; set; } = [];
    public List<MachineAddress> Machines { get; set; } = [];
    public List<string> Folders { get; set; } = ["Connessioni"];
    public List<ConnectionProfile> Profiles { get; set; } = [];
    public List<Snippet> Snippets { get; set; } = [];
    public List<Workspace> Workspaces { get; set; } = [];
    public UserSettings Settings { get; set; } = new();
}

public sealed class ProfileStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public static JsonSerializerOptions Json { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = true };
    public AppData Load()
    {
        var path = Path.Combine(DirectoryPath, "profiles.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Archivio profili non valido.") : new();
    }
    public void Save(AppData data) => AtomicFile.Write(Path.Combine(DirectoryPath, "profiles.json"), JsonSerializer.SerializeToUtf8Bytes(data, Json));
    public void Import(string path, AppData data)
    {
        var imported = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Formato non valido.");
        foreach (var profile in imported.Profiles) profile.Validate();
        var merged = new AppData { CertificateProviders = [.. data.CertificateProviders], FileLocations = new(data.FileLocations), Machines = [.. data.Machines], Profiles = [.. data.Profiles, .. imported.Profiles.Select(p => p with { Id = Guid.NewGuid().ToString("N"), PasswordId = "", Subsystem="", StartupCommands="", StartupMacro=null, Native=p.Native with { Executable="", ConfigFile="", IdentityFile="", ArgumentsJson="[]" }, Hooks=p.Hooks.Select(h=>h with {Enabled=false}).ToList() })], Folders = [.. data.Folders], Snippets = data.Snippets, Workspaces = data.Workspaces, Settings = data.Settings };
        foreach (var folder in imported.Folders.Concat(imported.Profiles.Select(p => p.Folder))) FolderCatalog.Add(merged, folder);
        Save(merged);
        data.Profiles = merged.Profiles;
        data.Folders = merged.Folders;
    }
}
public sealed record FileLocation(string Host, int Port, string Username, string Local, string Remote);
public static class AtomicFile
{
    public static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
