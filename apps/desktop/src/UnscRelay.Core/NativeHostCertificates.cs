using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Security;
namespace UnscRelay.Core;

public static class CertificateSignatures
{
    public static void Verify(string text, Certificate certificate)
    {
        var signature = certificate.Signature;
        if (signature.Length < 4) throw new CryptographicException("Firma certificato non valida.");
        var size = BinaryPrimitives.ReadInt32BigEndian(signature);
        if (size is < 1 or > 100 || size > signature.Length - 4) throw new CryptographicException("Firma certificato non valida.");
        var algorithm = Encoding.ASCII.GetString(signature, 4, size);
        if (algorithm == "ssh-rsa" || algorithm.Contains("-cert-", StringComparison.Ordinal)) throw new CryptographicException("Firma CA obsoleta o non valida.");
        var info = new ConnectionInfo("localhost", "verify", new NoneAuthenticationMethod("verify"));
        if (!info.HostKeyAlgorithms.TryGetValue(algorithm, out var factory)) throw new CryptographicException("Firma CA non supportata dal motore.");
        var bytes = Convert.FromBase64String(text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1]);
        var prefix = bytes.Length - signature.Length - 4;
        if (prefix < 0 || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(prefix, 4)) != signature.Length ||
            !bytes.AsSpan(prefix + 4).SequenceEqual(signature) || !factory(certificate.CertificateAuthorityKey).VerifySignature(bytes[..prefix], signature))
            throw new CryptographicException("Firma CA non valida.");
    }
}

public static class NativeHostCertificates
{
    public static HostKey Validate(string publicKey, string host, int port, DateTimeOffset now)
    {
        var identity = NativeIdentity.Parse(publicKey);
        var certificate = new Certificate(Convert.FromBase64String(identity.PublicKey.Split(' ')[1]));
        if (certificate.Name != identity.Algorithm || (uint)certificate.Type != 2) throw new IOException("Il server deve presentare un certificato host OpenSSH.");
        var instant = checked((ulong)now.ToUnixTimeSeconds());
        if (instant < certificate.ValidAfterUnixSeconds || instant >= certificate.ValidBeforeUnixSeconds) throw new IOException("Certificato host scaduto o non ancora valido.");
        if (!certificate.ValidPrincipals.Contains(host, StringComparer.OrdinalIgnoreCase)) throw new IOException("Il certificato host non corrisponde al nome del server.");
        if (certificate.CriticalOptions.Count != 0) throw new IOException("Opzioni critiche del certificato host non supportate.");
        CertificateSignatures.Verify(identity.PublicKey, certificate);
        var authority = certificate.CertificateAuthorityKey;
        if (authority.Length < 4) throw new IOException("Chiave CA non valida.");
        var size = BinaryPrimitives.ReadInt32BigEndian(authority);
        if (size is < 1 or > 120 || size > authority.Length - 4) throw new IOException("Chiave CA non valida.");
        var ca = NativeIdentity.Parse(Encoding.ASCII.GetString(authority, 4, size) + " " + Convert.ToBase64String(authority));
        return new HostKey(host, port, ca.Algorithm, ca.Fingerprint[7..], now.UtcDateTime, ca.PublicKey, true);
    }
    public static string KnownHostsLine(HostKey key, string lookup) => (key.IsCertificateAuthority ? "@cert-authority " : "") + lookup + " " + key.PublicKey;
}
