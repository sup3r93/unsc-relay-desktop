using System.Security.Cryptography;
using System.Text;
using Renci.SshNet.Security;
namespace UnscRelay.Core;

public sealed record CertificateRevocation(string Id, string Name, string Reason, DateTime RevokedAt, SshCertificateInfo Certificate);
public static class CertificateRevocations
{
    public static Certificate Parse(string text)
    {
        if (text.Length > 1024 * 1024) throw new ArgumentException("Certificato troppo grande.");
        var parts = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].EndsWith("-cert-v01@openssh.com", StringComparison.Ordinal)) throw new ArgumentException("Seleziona un certificato OpenSSH.");
        var parsed = new Certificate(Convert.FromBase64String(parts[1]));
        if (parsed.Name != parts[0] || (uint)parsed.Type != 1) throw new ArgumentException("Serve un certificato utente OpenSSH valido.");
        return parsed;
    }
    // Match OpenSSH KRL semantics: serial + CA, or key ID + CA for serial zero.
    public static string Identity(string text)
    {
        var cert = Parse(text);
        return cert.CertificateAuthorityKeyFingerPrint + ":" + (cert.Serial == 0 ? "id:" + cert.KeyId : "serial:" + cert.Serial.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    public static bool IsRevoked(string text, IEnumerable<CertificateRevocation> revoked)
    {
        var identity = Identity(text);
        return revoked.Any(r => Identity(r.Certificate.Text) == identity);
    }
}
public sealed partial class KeyVault
{
    public IReadOnlyList<CertificateRevocation> Revocations()
    {
        lock (_gate)
        {
            RequireUnlocked();
            return _records.Where(r => r.Info.Kind == "ssh-certificate-revocation" && r.Info.Certificate != null)
                .Select(r => new CertificateRevocation(r.Info.Id, r.Info.Name, r.Info.Comment, r.Info.Created, r.Info.Certificate!)).OrderByDescending(r => r.RevokedAt).ToArray();
        }
    }
    public CertificateRevocation RevokeCertificate(string keyId, string reason)
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || reason.Any(char.IsControl)) throw new ArgumentException("Indica un motivo da 1 a 500 caratteri, su una sola riga.");
            var info = List().Single(k => k.Id == keyId);
            var certificate = info.Certificate ?? throw new InvalidOperationException("Nessun certificato associato.");
            var previous = Revocations(); var identity = CertificateRevocations.Identity(certificate.Text);
            var existing = previous.FirstOrDefault(r => CertificateRevocations.Identity(r.Certificate.Text) == identity);
            if (existing != null) return existing;
            if (previous.Count >= 1000) throw new InvalidOperationException("Raggiunto il limite di 1000 revoche nell'archivio.");
            var record = new KeyRecord(new KeyInfo(Guid.NewGuid().ToString("N"), info.Name, reason.Trim(), DateTime.UtcNow, Certificate: certificate, Kind: "ssh-certificate-revocation"), []);
            _records.Add(record);
            try { Save(); } catch { _records.Remove(record); throw; }
            return Revocations().Single(r => r.Id == record.Info.Id);
        }
    }
    public void ValidateCertificateAccess(SshCertificateInfo certificate, string username)
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (CertificateRevocations.IsRevoked(certificate.Text, Revocations())) throw new InvalidOperationException("Certificato SSH revocato. Emetti un nuovo certificato oppure modifica esplicitamente la connessione. Nessun fallback automatico.");
            ValidateCertificateForLogin(certificate, username);
        }
    }
}
