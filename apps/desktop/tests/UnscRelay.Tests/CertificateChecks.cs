using System.Diagnostics;
using UnscRelay.Core;

internal static class CertificateChecks
{
    public static void Run(KeyVault vault, string userId, string caId, string directory, Action<bool, string> assert)
    {
        foreach (var algorithm in new[] { "rsa4096", "ecdsa256", "ecdsa384", "ecdsa521" })
        {
            var key = KeyGeneration.Generate(vault, "fixture-" + algorithm, algorithm);
            assert(key.PublicKey.Length > 0 && key.Fingerprint.StartsWith("SHA256:"), algorithm + " generation and SSH parser validation");
        }
        var issued = vault.IssueCertificate(userId, caId, "alice", 1);
        assert(issued.Certificate?.Principals.SequenceEqual(["alice"]) == true, "SSH certificate binds principal and matching key");
        var certificate = issued.Certificate!;
        KeyVault.ValidateCertificateForLogin(certificate, "alice");
        assert(certificate.ValidUntil > DateTimeOffset.UtcNow && certificate.ValidUntil <= DateTimeOffset.UtcNow.AddHours(1), "SSH certificate has bounded one-hour validity");
        try { KeyVault.ValidateCertificateForLogin(certificate, "bob"); throw new Exception("Principal mismatch accepted"); }
        catch (InvalidOperationException) { assert(true, "SSH certificate rejects wrong principal"); }
        try { KeyVault.ValidateCertificateForLogin(certificate with { ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-1) }, "alice"); throw new Exception("Expired certificate accepted"); }
        catch (InvalidOperationException) { assert(true, "Expired SSH certificate blocks login"); }
        try { vault.AttachCertificate(caId, certificate.Text); throw new Exception("Unrelated certificate accepted"); }
        catch (ArgumentException) { assert(true, "Certificate for a different private key rejected"); }
        try { vault.IssueCertificate(userId, caId, "alice", 24); throw new Exception("Long-lived certificate accepted"); }
        catch (ArgumentException) { assert(true, "Certificate issuer enforces 1-8 hour lifetime"); }

        if (OperatingSystem.IsWindows())
        {
            var file = Path.Combine(directory, "fixture-cert.pub"); File.WriteAllText(file, certificate.Text);
            var processInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh-keygen.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            processInfo.ArgumentList.Add("-L"); processInfo.ArgumentList.Add("-f"); processInfo.ArgumentList.Add(file);
            using var parser = Process.Start(processInfo)!; var output = parser.StandardOutput.ReadToEnd(); parser.WaitForExit();
            assert(parser.ExitCode == 0 && output.Contains("alice") && output.Contains("rsa-sha2-512"), "OpenSSH independently parses certificate and RSA SHA-2 CA signature");
        }
        vault.RemoveCertificate(userId);
        assert(vault.List().Single(k => k.Id == userId).Certificate == null, "Removing certificate preserves underlying key");
    }
}
