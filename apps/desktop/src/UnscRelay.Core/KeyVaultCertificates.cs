using System.Text;
using Renci.SshNet;
using SshNet.Keygen;
using Renci.SshNet.Security;
using System.Security.Cryptography;

namespace UnscRelay.Core;
public sealed record SshCertificateInfo(string Text, string KeyId, string[] Principals, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, string AuthorityFingerprint)
{
    public string RevocationIdentity => CertificateRevocations.Identity(Text);
}
public sealed partial class KeyVault
{
    public KeyInfo AttachCertificate(string id, string text, string passphrase = "")
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (text.Length > 1024 * 1024) throw new ArgumentException("Certificato troppo grande.");
            var index = _records.FindIndex(k => k.Info.Id == id && k.Info.Kind == "ssh-key");
            if (index < 0) throw new ArgumentException("Chiave non trovata.");
            var record = _records[index];
            using var keyStream = new MemoryStream(record.Content, false);
            using var certificateStream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            // SSH.NET verifies that this certificate certifies this exact private key.
            using var parsed = new PrivateKeyFile(keyStream, passphrase, certificateStream);
            var certificate = parsed.Certificate!;
            CertificateSignatures.Verify(text, certificate);
            if ((uint)certificate.Type != 1) throw new ArgumentException("Per l'accesso serve un certificato utente, non host.");
            var maxUnix = (ulong)DateTimeOffset.MaxValue.ToUnixTimeSeconds();
            if (certificate.ValidAfterUnixSeconds > maxUnix) throw new ArgumentException("Validita del certificato non valida.");
            var metadata = new SshCertificateInfo(text.Trim(), certificate.KeyId, certificate.ValidPrincipals.ToArray(),
                DateTimeOffset.FromUnixTimeSeconds((long)certificate.ValidAfterUnixSeconds),
                certificate.ValidBeforeUnixSeconds > maxUnix ? null : DateTimeOffset.FromUnixTimeSeconds((long)certificate.ValidBeforeUnixSeconds),
                "SHA256:" + certificate.CertificateAuthorityKeyFingerPrint);
            var replacement = record with { Info = record.Info with { Certificate = metadata } };
            _records[index] = replacement;
            try { Save(); } catch { _records[index] = record; throw; }
            return replacement.Info;
        }
    }
    public KeyInfo RemoveCertificate(string id)
    {
        lock (_gate)
        {
            RequireUnlocked(); var index = _records.FindIndex(k => k.Info.Id == id && k.Info.Kind == "ssh-key");
            if (index < 0) throw new ArgumentException("Chiave non trovata.");
            var old = _records[index]; var next = old with { Info = old.Info with { Certificate = null } }; _records[index] = next;
            try { Save(); } catch { _records[index] = old; throw; } return next.Info;
        }
    }
    public KeyInfo IssueCertificate(string targetId, string caId, string principal, int hours, string keyPassphrase = "", string caPassphrase = "")
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (targetId == caId) throw new ArgumentException("La CA deve usare una chiave separata.");
            if (hours is < 1 or > 8) throw new ArgumentException("Durata consentita: da 1 a 8 ore.");
            if (string.IsNullOrWhiteSpace(principal) || principal.Any(char.IsControl) || principal.Length > 100) throw new ArgumentException("Principal non valido.");
            using var targetStream = new MemoryStream(_records.Single(k => k.Info.Id == targetId && k.Info.Kind == "ssh-key").Content, false);
            using var caStream = new MemoryStream(_records.Single(k => k.Info.Id == caId && k.Info.Kind == "ssh-key").Content, false);
            using var target = new PrivateKeyFile(targetStream, keyPassphrase); using var ca = new PrivateKeyFile(caStream, caPassphrase);
            var now = DateTime.UtcNow;
            var certificate = new SshCertificateBuilder(target).WithSerial(BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)))
                .WithKeyId("relay-" + Guid.NewGuid().ToString("N")).WithPrincipal(principal)
                .WithValidity(now.AddMinutes(-1), now.AddHours(hours)).SignWith((KeyHostAlgorithm)ca.HostKeyAlgorithms.First(a => a.Name != "ssh-rsa"));
            return AttachCertificate(targetId, certificate.ToOpenSshPublicFormat(), keyPassphrase);
        }
    }
    public static void ValidateCertificateForLogin(SshCertificateInfo certificate, string username)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < certificate.ValidFrom || certificate.ValidUntil is { } end && now >= end) throw new InvalidOperationException("Certificato SSH scaduto o non ancora valido. Rinnovalo prima di collegarti.");
        if (certificate.Principals.Length > 0 && !certificate.Principals.Contains(username, StringComparer.Ordinal)) throw new InvalidOperationException("Lo username non e tra i principal del certificato SSH.");
    }
}
