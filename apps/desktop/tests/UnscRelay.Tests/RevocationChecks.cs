using System.Text;
using Renci.SshNet;
using Renci.SshNet.Security;
using SshNet.Keygen;
using UnscRelay.Core;
internal static class RevocationChecks
{
    public static async Task<int> Run()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "revocation-fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "keys.vault"); const string master = "Revocation fixture master!"; var count = 0;
        void Check(bool yes, string label) { if (!yes) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
        void Reject<T>(Action action, string label) where T : Exception { try { action(); } catch (T) { Check(true, label); return; } throw new Exception(label); }
        using var vault = new KeyVault(path); vault.Unlock(master);
        var key = KeyGeneration.Generate(vault, "User fixture", "ed25519"); var ca = KeyGeneration.Generate(vault, "CA fixture", "ed25519"); var otherCa = KeyGeneration.Generate(vault, "Different CA", "ecdsa256");
        var certificate = vault.IssueCertificate(key.Id, ca.Id, "alice", 1).Certificate!;
        vault.ValidateCertificateAccess(certificate, "alice"); Check(vault.Revocations().Count == 0, "Existing archive has no revocations and valid certificate remains usable");
        var original = File.ReadAllBytes(path);
        Reject<ArgumentException>(() => vault.RevokeCertificate(key.Id, " "), "Revocation requires a meaningful reason");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "Invalid revocation leaves archive unchanged");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        { try { vault.RevokeCertificate(key.Id, "Write failure fixture"); throw new Exception("Blocked write succeeded"); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Check(true, "Write failure is reported before claiming success"); } }
        Check(vault.Revocations().Count == 0 && original.SequenceEqual(File.ReadAllBytes(path)), "Failed revocation rolls back memory and disk");
        var revoked = vault.RevokeCertificate(key.Id, "Retired credential fixture");
        Check(vault.List().Count == 3 && vault.Passwords().Count == 0 && vault.Revocations().Count == 1, "Revocations are separated from key and password lists");
        Reject<InvalidOperationException>(() => vault.ValidateCertificateAccess(certificate, "alice"), "Revoked certificate cannot authenticate from vault");
        Check(vault.RevokeCertificate(key.Id, "duplicate").Id == revoked.Id && vault.Revocations().Count == 1, "Repeated revocation does not duplicate records or rewrite reason");
        Check(!File.ReadAllText(path).Contains("Retired credential fixture"), "Revocation metadata is encrypted at rest");
        Reject<InvalidOperationException>(() => vault.Delete(revoked.Id), "Revocation record cannot be deleted through key deletion");
        Reject<ArgumentException>(() => vault.RemoveCertificate(revoked.Id), "Certificate removal cannot erase revocation records");
        Reject<ArgumentException>(() => vault.UpdateMetadata(revoked.Id, "Changed", "changed"), "Key metadata editing cannot rewrite revocation reason");
        var serial = CertificateRevocations.Parse(certificate.Text).Serial;
        string Sign(string authority)
        {
            var userBytes = vault.Read(key.Id); var caBytes = vault.Read(authority);
            try
            {
                using var user = new PrivateKeyFile(new MemoryStream(userBytes)); using var signer = new PrivateKeyFile(new MemoryStream(caBytes));
                return new SshCertificateBuilder(user).WithSerial(serial).WithKeyId("different-nonce-and-id").WithPrincipal("alice").WithValidity(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1)).SignWith((KeyHostAlgorithm)signer.HostKeyAlgorithms.First(a => a.Name != "ssh-rsa")).ToOpenSshPublicFormat();
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(userBytes); System.Security.Cryptography.CryptographicOperations.ZeroMemory(caBytes); }
        }
        var sameSerial = Sign(ca.Id); var differentAuthority = Sign(otherCa.Id);
        Check(sameSerial != certificate.Text && CertificateRevocations.IsRevoked(sameSerial, vault.Revocations()), "Same CA and serial stays revoked despite nonce or ID differences");
        Check(!CertificateRevocations.IsRevoked(differentAuthority, vault.Revocations()), "Identical serial under a different CA is not revoked");
        var renewed = vault.IssueCertificate(key.Id, ca.Id, "alice", 1).Certificate!; vault.ValidateCertificateAccess(renewed, "alice");
        Check(!CertificateRevocations.IsRevoked(renewed.Text, vault.Revocations()), "Explicit renewal creates a different usable certificate");
        var krl = await OpenSshKrl.GenerateAsync(vault.Revocations());
        Check(await OpenSshKrl.QueryAsync(krl, certificate.Text), "Installed OpenSSH independently confirms exported certificate revocation");
        Check(await OpenSshKrl.QueryAsync(krl, sameSerial), "OpenSSH KRL agrees with same CA and serial matching");
        Check(!await OpenSshKrl.QueryAsync(krl, differentAuthority) && !await OpenSshKrl.QueryAsync(krl, renewed.Text), "OpenSSH KRL leaves another CA and renewed certificate usable");
        try { await OpenSshKrl.QueryAsync("SSHKRL\n\0invalid"u8.ToArray(), certificate.Text); throw new Exception("Invalid KRL accepted"); }
        catch (IOException) { Check(true, "Corrupted KRL is an error, never a clean verdict"); }
        Reject<ArgumentException>(() => CertificateRevocations.Parse(key.PublicKey), "Plain public key is not accepted as a certificate revocation");
        var backup = Path.Combine(directory, "backup.vault"); vault.Backup(backup); using (var restored = new KeyVault(backup)) { restored.Unlock(master); Check(restored.Revocations().Count == 1, "Encrypted backup includes the revocation ledger"); }
        vault.RemoveCertificate(key.Id); vault.Delete(key.Id); vault.Delete(ca.Id); vault.Lock();
        Reject<InvalidOperationException>(() => vault.Revocations(), "Locked archive hides revocation metadata"); vault.Unlock(master);
        Check(vault.Revocations().Count == 1 && vault.List().Count == 1, "Revocation survives certificate removal, key and CA deletion, lock and reopen");
        var retainedKrl = await OpenSshKrl.GenerateAsync(vault.Revocations()); Check(await OpenSshKrl.QueryAsync(retainedKrl, certificate.Text), "KRL can still be exported after deleting original key and CA");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await OpenSshKrl.GenerateAsync(vault.Revocations(), cancelled.Token); throw new Exception("Cancelled export succeeded"); }
        catch (OperationCanceledException) { Check(true, "Cancelled export cannot report success"); }
        Console.WriteLine(count + " revocation checks passed"); return 0;
    }
}
