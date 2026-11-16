using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class MainWindow
{
    readonly Dictionary<string, NativeBridge> nativeBridges = [];
    readonly Dictionary<string, ConnectionProfile> nativeSessionProfiles = [];
    readonly Dictionary<string, string> nativePublicFolders = [];
    string NativeHelper => Environment.ProcessPath ?? throw new IOException("EXE dell'app non disponibile.");
    void StopBundledAgent() { try { if (bundledAgent is { HasExited: false }) bundledAgent.Kill(); } catch (InvalidOperationException) { } bundledAgent?.Dispose(); bundledAgent = null; var socket = NativeAgent.SocketForHome(Path.Combine(dataPath, "NativeHome")); if (File.Exists(socket)) File.Delete(socket); }
    NativeBridge CreateNativeBridge(ConnectionProfile profile, string? phrase = null) => new(req => Dispatcher.InvokeAsync(() =>
    {
        if (req.Action == "host") return new NativeReply(true, NativeIdentity.KnownHost(req, hosts, Trust));
        if (req.Action == "ask" && req.Arguments.Length <= 3 && req.Arguments.All(x => x.Length <= 8192))
        {
            // ssh-askpass supplies its prompt as argv[0], not an action flag.
            if (phrase != null) return new NativeReply(true, phrase);
            if (profile.Authentication == "password") { if (profile.PasswordId.Length > 0 && !vault.IsUnlocked) return new NativeReply(false); return new NativeReply(true, ConnectionPassword(profile)); }
            return new NativeReply(false);
        }
        return new NativeReply(false);
    }).Task);
    async Task<LocalLaunch> PrepareNative(ConnectionProfile profile, string id,string? startupMacro=null)
    {
        var bridge = CreateNativeBridge(profile); nativeBridges[id] = bridge; nativeSessionProfiles[id] = profile;
        var folder = Path.Combine(dataPath, "NativeSessions", id); Directory.CreateDirectory(folder); nativePublicFolders[id] = folder; var options = profile.Native with { }; SshCertificateInfo? nativeCertificate=null;string? certificateUser=null;string? certificateAlgorithms=null;
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
            var identities = await NativeAgent.Run(NativeClient.Find("ssh-add")!, ["-L"], NativeAgent.SocketForHome(Path.Combine(dataPath, "NativeHome"))); if (!identities.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Any(x => nativeCertificate == null ? NativeIdentity.Parse(x).Fingerprint == key.Fingerprint : NativeIdentity.Parse(x).PublicKey == NativeIdentity.Parse(nativeCertificate.Text).PublicKey)) throw new IOException("L’identità nell’agent non corrisponde alla chiave selezionata.");
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
        NativeClient.AddStartupMacro(launch,profile,startupMacro);
        return launch;
    }
    async Task EndNative(string id) { StopRecordingSession(id); nativeSessionProfiles.Remove(id); if (nativeBridges.Remove(id, out var bridge)) await bridge.DisposeAsync(); if (nativePublicFolders.Remove(id, out var folder)) { foreach (var name in new[] { "config", "identity.pub", "identity-cert.pub", "(stdin)-cert.pub" }) { var file = Path.Combine(folder, name); if (File.Exists(file)) File.Delete(file); } try { Directory.Delete(folder); } catch (IOException) { } } }
    async Task<object?> ImportNativeVault(JsonElement p) { if (!vault.IsUnlocked) throw new IOException("Sblocca prima l'archivio."); var selection = pendingKeyImport; if (selection == null || selection.Value.Id != S(p, "selectionId") || selection.Value.Expires < DateTime.UtcNow) throw new IOException("Seleziona di nuovo il file della chiave."); pendingKeyImport = null; var path = selection.Value.Path; if (new FileInfo(path).Length > 1048576) throw new IOException("Chiave troppo grande."); var phrase = Secret("Passphrase chiave OpenSSH", "Vuoto se assente. La chiave viene passata solo in memoria all'agent incluso."); var bytes = await File.ReadAllBytesAsync(path); await using var bridge = CreateNativeBridge(new ConnectionProfile(), phrase); try { var pub = await NativeAgent.ImportIdentity(Path.GetDirectoryName(NativeClient.Find("ssh-add")!)!, bytes, bridge, NativeHelper); vault.ImportNative(S(p, "name"), bytes, pub, S(p, "comment")); return Snapshot(); } finally { CryptographicOperations.ZeroMemory(bytes); } }
}
