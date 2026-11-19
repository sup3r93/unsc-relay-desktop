using System.Diagnostics;
using System.Security.Cryptography;
namespace UnscRelay.Core;

public sealed class TemporaryNativeAgent : IAsyncDisposable
{
    public string Socket { get; } = Path.Combine(Path.GetTempPath(), "ur-" + Guid.NewGuid().ToString("N")[..12] + ".sock");
    Process? process;
    public async Task Start(string executable) { var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }; start.ArgumentList.Add("-D"); start.ArgumentList.Add("-a"); start.ArgumentList.Add(NativeClient.CygwinPath(Socket)); process = Process.Start(start) ?? throw new IOException("Agent temporaneo non avviato."); _ = process.StandardOutput.ReadToEndAsync(); _ = process.StandardError.ReadToEndAsync(); for (int i = 0; i < 60 && !File.Exists(Socket) && !process.HasExited; i++) await Task.Delay(50); if (!File.Exists(Socket) || process.HasExited) throw new IOException("Agent temporaneo non disponibile."); }
    public ValueTask DisposeAsync() { if (process != null) { try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(3000); } } catch (InvalidOperationException) { } process.Dispose(); } if (File.Exists(Socket)) File.Delete(Socket); return ValueTask.CompletedTask; }
}
public static class NativeAgent
{
    public static string SocketForHome(string home) => LocalDataFiles.Child(Path.GetTempPath(), "ur-" + Environment.ProcessId + "-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(home).ToUpperInvariant())))[..12] + ".sock");
    public static byte[] CompatibleIdentity(byte[] content, string passphrase = "")
    {
        using var input = new MemoryStream(content, false);
        using var key = new Renci.SshNet.PrivateKeyFile(input, passphrase);
        return System.Text.Encoding.UTF8.GetBytes(SshNet.Keygen.Extensions.PrivateKeyFileExtension.ToOpenSshFormat(key));
    }
    public static async Task<string> Run(string executable, string[] arguments, string socket, NativeBridge? bridge = null, string? helper = null, byte[]? privateInput = null, string? workingDirectory = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }; if (workingDirectory != null) info.WorkingDirectory = workingDirectory; foreach (var arg in arguments) info.ArgumentList.Add(arg); info.Environment["SSH_AUTH_SOCK"] = NativeClient.CygwinPath(socket); info.Environment["SSH_ASKPASS_REQUIRE"] = "force"; info.Environment["RELAY_ASKPASS_HELPER"] = "1"; info.Environment["DISPLAY"] = "relay"; if (bridge != null) { var env = new Dictionary<string, string>(); bridge.Apply(env); foreach (var pair in env) info.Environment[pair.Key] = pair.Value; if (helper != null) info.Environment["SSH_ASKPASS"] = NativeClient.CygwinPath(helper); }
        using var p = Process.Start(info) ?? throw new IOException("Componente agent non avviato."); var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync(); try { if (privateInput != null) await p.StandardInput.BaseStream.WriteAsync(privateInput); p.StandardInput.Close(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120)); await p.WaitForExitAsync(timeout.Token); var text = await output; await error; if (p.ExitCode != 0) throw new IOException("La chiave non è stata caricata: controlla formato e passphrase. Il file originale è invariato."); if (text.Length > 1048576) throw new IOException("Risposta dell'agent troppo grande."); return text; } catch { try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { } throw; }
    }
    public static async Task<string> ImportIdentity(string keygenDirectory, byte[] content, NativeBridge bridge, string helper) { await using var agent = new TemporaryNativeAgent(); await agent.Start(Path.Combine(keygenDirectory, "ssh-agent.exe")); await Run(Path.Combine(keygenDirectory, "ssh-add.exe"), ["-q", "-k", "-t", "60", "-"], agent.Socket, bridge, helper, content); var identities = (await Run(Path.Combine(keygenDirectory, "ssh-add.exe"), ["-L"], agent.Socket)).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries); if (identities.Length != 1) throw new IOException("Identità importata non univoca."); return NativeIdentity.Parse(identities[0]).PublicKey; }
}
