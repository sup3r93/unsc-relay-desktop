using System.Diagnostics;
using System.Text;
namespace UnscRelay.Core;

/// <summary>Uses the bundled OpenSSH tool; never exports or reads private keys.</summary>
public static class OpenSshKrl
{
    static string Tool => NativeClient.Find("windows-keygen") ?? "";
    public static bool Available => OperatingSystem.IsWindows() && File.Exists(Tool);
    public static async Task<byte[]> GenerateAsync(IReadOnlyList<CertificateRevocation> revocations, CancellationToken cancellation = default)
    {
        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellation); total.CancelAfter(TimeSpan.FromSeconds(60)); cancellation = total.Token;
        if (revocations.Count is < 1 or > 1000) throw new ArgumentException("Servono da 1 a 1000 certificati revocati.");
        var texts = revocations.Select(r => r.Certificate.Text).ToArray();
        foreach (var text in texts) CertificateRevocations.Parse(text);
        if (texts.Sum(t => (long)t.Length) > 8 * 1024 * 1024) throw new ArgumentException("Elenco revoche troppo grande.");
        var directory = Path.Combine(Path.GetTempPath(), "Relay-KRL-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "certificates.pub"); var output = Path.Combine(directory, "revoked.krl");
            await File.WriteAllTextAsync(input, string.Join("\n", texts.Select(t => string.Join(" ", t.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(2)))) + "\n", new UTF8Encoding(false), cancellation);
            var exit = await RunAsync(["-k", "-f", output, input], cancellation);
            if (exit != 0 || !File.Exists(output)) throw new IOException("OpenSSH non ha generato la lista KRL.");
            var bytes = await File.ReadAllBytesAsync(output, cancellation);
            if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual("SSHKRL\n\0"u8)) throw new InvalidDataException("OpenSSH ha prodotto una KRL non valida.");
            // Independently ask OpenSSH to confirm that all requested certificates are revoked.
            foreach (var text in texts)
            {
                await File.WriteAllTextAsync(input, text + "\n", new UTF8Encoding(false), cancellation);
                if (await RunAsync(["-Q", "-f", output, input], cancellation) != 1) throw new IOException("La KRL non conferma una delle revoche richieste.");
            }
            return bytes;
        }
        finally { Directory.Delete(directory, true); }
    }
    public static async Task<bool> QueryAsync(byte[] krl, string certificate, CancellationToken cancellation = default)
    {
        CertificateRevocations.Parse(certificate);
        if (krl.Length is < 8 or > 8 * 1024 * 1024 || !krl.AsSpan(0,8).SequenceEqual("SSHKRL\n\0"u8)) throw new ArgumentException("Lista KRL non valida o troppo grande.");
        var directory = Path.Combine(Path.GetTempPath(), "Relay-KRL-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var list = Path.Combine(directory, "list.krl"); var key = Path.Combine(directory, "certificate.pub");
            await File.WriteAllBytesAsync(list, krl, cancellation); await File.WriteAllTextAsync(key, certificate + "\n", new UTF8Encoding(false), cancellation);
            return await RunAsync(["-Q", "-f", list, key], cancellation) switch { 0 => false, 1 => true, _ => throw new IOException("OpenSSH non ha potuto verificare la KRL.") };
        }
        finally { Directory.Delete(directory, true); }
    }
    static async Task<int> RunAsync(string[] arguments, CancellationToken cancellation)
    {
        if (!Available) throw new InvalidOperationException("Componente OpenSSH incluso mancante: ripara l’installazione per esportare o verificare KRL.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var start = new ProcessStartInfo(Tool) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Impossibile avviare OpenSSH."); process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token); var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(stdout, stderr); return process.ExitCode; }
        catch { try { if (!process.HasExited) process.Kill(entireProcessTree: true); process.WaitForExit(5000); } catch (InvalidOperationException) { } throw; }
    }
}
