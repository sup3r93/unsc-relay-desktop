using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class MainWindow
{
    readonly HashSet<string> nativeAuthenticated=[];
    readonly Dictionary<string,string> nativeRecoveryProfiles=[];
    readonly Dictionary<string, NativeBridge> nativeBridges = [];
    readonly Dictionary<string, NativeControlSession> nativeControls = [];
    readonly Dictionary<string, NativeHookSession> nativeHookSessions = [];
    readonly Dictionary<string, ConnectionProfile> nativeSessionProfiles = [];
    readonly Dictionary<string, string> nativePublicFolders = [];
    string NativeHelper => Environment.ProcessPath ?? throw new IOException("EXE dell'app non disponibile.");
    void StopBundledAgent() { try { if (bundledAgent is { HasExited: false }) bundledAgent.Kill(); } catch (InvalidOperationException) { } bundledAgent?.Dispose(); bundledAgent = null; try{var socket = NativeAgent.SocketForHome(Path.Combine(dataPath, "NativeHome")); if (File.Exists(socket)) File.Delete(socket);}catch(Exception error)when(error is IOException or UnauthorizedAccessException){/* Agent is stopped; a busy stale socket is cleaned on a later attempt. */} }
    NativeBridge CreateNativeBridge(ConnectionProfile profile, string? phrase = null, string? sessionId = null,bool recovering=false) => new(req => Dispatcher.InvokeAsync(async () =>
    {
        if(req.Action == "authenticated" && req.Arguments.Length == 0 && sessionId != null && nativeHookSessions.TryGetValue(sessionId,out var hooks)) { _ = CompleteNativeAuthentication(sessionId,hooks); return new NativeReply(true); }
        if (req.Action == "host") return new NativeReply(true, NativeIdentity.KnownHost(req, hosts, recovering ? _=>false : Trust));
        if (req.Action == "ask" && req.Arguments.Length <= 3 && req.Arguments.All(x => x.Length <= 8192))
        {
            // ssh-askpass supplies its prompt as argv[0], not an action flag.
            if (phrase != null) return new NativeReply(true, phrase);
            if (profile.Authentication == "password") { if (profile.PasswordId.Length > 0 && !vault.IsUnlocked) return new NativeReply(false); return new NativeReply(true, ConnectionPassword(profile)); }
            return new NativeReply(false);
        }
        return new NativeReply(false);
    }).Task.Unwrap());
    async Task<LocalLaunch> PrepareNative(ConnectionProfile profile, string id,string? startupMacro=null,bool recovering=false)
    {
        var bridge = CreateNativeBridge(profile, sessionId:id,recovering:recovering); nativeBridges[id] = bridge; nativeSessionProfiles[id] = profile;
        var folder = Path.Combine(dataPath, "NativeSessions", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); nativePublicFolders[id] = folder; var options = profile.Native with { }; SshCertificateInfo? nativeCertificate=null;string? certificateUser=null;string? certificateAlgorithms=null;
        if (options.Kind is "ssh" or "sftp" && options.Executable.Length > 0 && (options.SharedHostKeys || profile.Authentication != "auto")) throw new IOException("Archivio e gestore host condiviso richiedono il client OpenSSH incluso. Lascia vuoto il percorso eseguibile.");
        if (profile.Authentication == "key")
        {
            if (!vault.IsUnlocked) throw new InvalidOperationException("Sblocca prima l'archivio chiavi."); var key = vault.List().Single(k => k.Id == profile.KeyId);
            if(key.Certificate is {} certificate)
            {
                certificateAlgorithms=NativeCertificates.Algorithms(certificate,options.Signature);
                var queryOptions=options with{Kind="ssh",IdentityFile=""};
                if(queryOptions.ConfigFile.Length==0){var fallback=Path.Combine(dataPath,"NativeHome",".ssh","config");if(File.Exists(fallback))queryOptions.ConfigFile=fallback;}
                using var query=NativeClient.Launch(profile with{Native=queryOptions});query.SetHome(Path.Combine(dataPath,"NativeHome"));
                var effective=await NativeClient.Capture(query.Executable,new[]{"-G"}.Concat(query.Arguments),environment:query.EnvironmentVariables);
                certificateUser=NativeCertificates.LoginUser(effective);vault.ValidateCertificateAccess(certificate,certificateUser);nativeCertificate=certificate;
            }
            await EnsureBundledAgent(); var bytes = vault.Read(key.Id);
            if (key.Kind == "ssh-key") { try { var converted = NativeAgent.CompatibleIdentity(bytes, connectionPassphrases.GetValueOrDefault(profile.Id, "")); CryptographicOperations.ZeroMemory(bytes); bytes = converted; } catch { CryptographicOperations.ZeroMemory(bytes); throw; } }
            // Original ssh-add names its stdin identity "(stdin)" and looks for this public sidecar.
            // Private material travels exclusively over stdin; the client below accepts certificate algorithms only.
            var sidecar = Path.Combine(folder, "(stdin)-cert.pub");
            await using var loadBridge = CreateNativeBridge(profile, connectionPassphrases.GetValueOrDefault(profile.Id, ""));
            try
            {
                if (nativeCertificate != null) await File.WriteAllTextAsync(sidecar, nativeCertificate.Text + "\n", new UTF8Encoding(false));
                await NativeAgent.Run(NativeClient.Find("ssh-add")!, nativeCertificate == null ? ["-q", "-k", "-t", "3600", "-"] : ["-q", "-t", "3600", "-"], NativeAgent.SocketForHome(Path.Combine(dataPath, "NativeHome")), loadBridge, NativeHelper, bytes, folder);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); if (File.Exists(sidecar)) File.Delete(sidecar); }
            if (!vault.IsUnlocked) { StopBundledAgent(); throw new IOException("Archivio bloccato durante il caricamento."); }
            var identities = await NativeAgent.Run(NativeClient.Find("ssh-add")!, ["-L"], NativeAgent.SocketForHome(Path.Combine(dataPath, "NativeHome"))); if (!identities.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Any(x => nativeCertificate == null ? NativeIdentity.Parse(x).Fingerprint == key.Fingerprint : NativeIdentity.Parse(x).PublicKey == NativeIdentity.Parse(nativeCertificate.Text).PublicKey)) throw new IOException("L’identita nell’agent non corrisponde alla chiave selezionata.");
            var pub = Path.Combine(folder, "identity.pub"); await File.WriteAllTextAsync(pub, key.PublicKey + "\n", new UTF8Encoding(false)); await NativeClient.Capture(Path.Combine(Path.GetDirectoryName(NativeClient.Find("ssh")!)!, "chmod.exe"), ["600", NativeClient.CygwinPath(pub)]); options.IdentityFile = pub;
            if(nativeCertificate!=null)
            {
                vault.ValidateCertificateAccess(nativeCertificate,certificateUser!);
                var certificatePath=Path.Combine(folder,"identity-cert.pub");await File.WriteAllTextAsync(certificatePath,nativeCertificate.Text+"\n",new UTF8Encoding(false));
                await NativeClient.Capture(Path.Combine(Path.GetDirectoryName(NativeClient.Find("ssh")!)!,"chmod.exe"),["600",NativeClient.CygwinPath(certificatePath)]); options.IdentityFile = certificatePath;
            }
        }
        if (options.SharedHostKeys && options.Kind is "ssh" or "sftp")
        {
            var original = options.ConfigFile; if (original.Length == 0) { var fallback = Path.Combine(dataPath, "NativeHome", ".ssh", "config"); if (File.Exists(fallback)) original = fallback; }
            var config = NativePolicy.Configuration(NativeHelper, original);
            var path = Path.Combine(folder, "config"); await File.WriteAllTextAsync(path, config, new UTF8Encoding(false)); await NativeClient.Capture(Path.Combine(Path.GetDirectoryName(NativeClient.Find("ssh")!)!, "chmod.exe"), ["600", NativeClient.CygwinPath(path)]); options.ConfigFile = path;
        }
        var launch = NativeClient.Launch(profile with { Native = options }); launch.SetHome(Path.Combine(dataPath, "NativeHome")); bridge.Apply(launch.EnvironmentVariables);
        if (profile.Authentication == "password") { launch.Arguments.InsertRange(0, ["-o", "PreferredAuthentications=password,keyboard-interactive", "-o", "PubkeyAuthentication=no"]); launch.EnvironmentVariables["SSH_ASKPASS"] = NativeClient.CygwinPath(NativeHelper); launch.EnvironmentVariables["SSH_ASKPASS_REQUIRE"] = "force"; launch.EnvironmentVariables["DISPLAY"] = "relay"; launch.EnvironmentVariables["RELAY_ASKPASS_HELPER"] = "1"; }
        if (profile.Authentication == "key") { launch.Arguments.InsertRange(0, ["-o", "IdentityAgent=" + NativeClient.CygwinPath(NativeAgent.SocketForHome(Path.Combine(dataPath, "NativeHome"))), "-o", "ForwardAgent=no", "-o", "PreferredAuthentications=publickey", "-o", "PasswordAuthentication=no", "-o", "KbdInteractiveAuthentication=no"]); }
        if(nativeCertificate!=null)
        {
            vault.ValidateCertificateAccess(nativeCertificate,certificateUser!);
            NativeCertificates.Apply(launch,Path.Combine(folder,"identity-cert.pub"),certificateUser!,certificateAlgorithms!);
        }
        if(nativeControls.TryGetValue(id,out var control))
        {
            NativeHookSession.Configure(launch,NativeHelper);
            if(options.Kind=="sftp")NativeHookSession.ConfigureSftpTransport(launch,NativeHelper);
            control.Configure(launch);
        }
        if(recovering)launch.Arguments.InsertRange(0,["-o","RemoteCommand=none","-o","RequestTTY=auto"]);
        NativeClient.AddStartupMacro(launch,profile,startupMacro);
        return launch;
    }
    async Task CompleteNativeAuthentication(string id,NativeHookSession hooks)
    {
        try
        {
            if(!nativeControls.TryGetValue(id,out var control))return;
            // LocalCommand must return before the master starts serving the mux socket.
            await control.WaitReady();
            if(!nativeHookSessions.TryGetValue(id,out var current)||!ReferenceEquals(current,hooks))return;
            if(!localSessions.TryGetValue(id,out var terminal)||!terminal.Running)return;
            nativeAuthenticated.Add(id);
            if(nativeSessionProfiles.TryGetValue(id,out var authenticatedProfile))nativeRecoveryProfiles.TryAdd(id,NativeRecovery.Snapshot(authenticatedProfile));
            Emit("nativeAuthenticated",new{sessionId=id,connected=true,text="OpenSSH autenticato"});
            if(!OperationsLocked)await hooks.Authenticated();
        }
        catch(Exception ex)when(ex is IOException or TimeoutException or ObjectDisposedException){Emit("notice",new{text=ex.Message});}
    }
    async Task EndNative(string id,bool runHooks=true,bool preserveChildren=false)
    {
        // Detach every owned resource before awaiting: a late exit cannot dispose a replacement.
        nativeHookSessions.Remove(id,out var hooks);nativeControls.Remove(id,out var control);
        nativeBridges.Remove(id,out var bridge);nativePublicFolders.Remove(id,out var folder);
        nativeSessionProfiles.Remove(id);nativeAuthenticated.Remove(id);StopRecordingSession(id);
        var children=nativeStorageParents.Where(x=>x.Value==id).Select(x=>x.Key).ToArray();
        var advancedFiles=new List<SshSession>();
        foreach(var child in children)if(advancedNativeChildren.Contains(child)){
            if(sessions.Remove(child,out var advanced)){PauseRecoveryJobs(advanced.Profile.Id);advancedFiles.Add(advanced);}
            if(!preserveChildren)advancedNativeChildren.Remove(child);
        }
        foreach(var advanced in advancedFiles)await Task.Run(advanced.Dispose);
        var files=new List<StorageFiles>();
        foreach(var child in children){if(storageSessions.Remove(child,out var file))files.Add(file);if(!preserveChildren)nativeStorageParents.Remove(child);Emit("storageStatus",new{sessionId=child,connected=false,text="Connessione OpenSSH terminata. Browser SFTP in attesa di riconnessione."});}
        foreach(var file in files)await file.DisposeAsync();
        if(hooks!=null)await hooks.Finished(!OperationsLocked&&runHooks&&!updating&&!updateClosing);
        if(control!=null)await control.DisposeAsync();if(bridge!=null)await bridge.DisposeAsync();
        if(folder!=null){foreach(var name in new[]{"config","identity.pub","identity-cert.pub","(stdin)-cert.pub"}){var file=Path.Combine(folder,name);if(File.Exists(file))File.Delete(file);}try{Directory.Delete(folder);}catch(IOException){}}
    }
    async Task<object?> ImportNativeVault(JsonElement p) { if (!vault.IsUnlocked) throw new IOException("Sblocca prima l'archivio."); var selection = pendingKeyImport; if (selection == null || selection.Value.Id != S(p, "selectionId") || selection.Value.Expires < DateTime.UtcNow) throw new IOException("Seleziona di nuovo il file della chiave."); pendingKeyImport = null; var path = selection.Value.Path; if (new FileInfo(path).Length > 1048576) throw new IOException("Chiave troppo grande."); var phrase = Secret("Passphrase chiave OpenSSH", "Vuoto se assente. La chiave viene passata solo in memoria all'agent incluso."); var bytes = await File.ReadAllBytesAsync(path); await using var bridge = CreateNativeBridge(new ConnectionProfile(), phrase); try { var pub = await NativeAgent.ImportIdentity(Path.GetDirectoryName(NativeClient.Find("ssh-add")!)!, bytes, bridge, NativeHelper); vault.ImportNative(S(p, "name"), bytes, pub, S(p, "comment")); return Snapshot(); } finally { CryptographicOperations.ZeroMemory(bytes); } }
}
