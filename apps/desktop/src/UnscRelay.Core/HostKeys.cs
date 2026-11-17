using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnscRelay.Core;
public sealed record HostKey(string Host, int Port, string Algorithm, string Fingerprint, DateTime AcceptedUtc, string PublicKey = "", bool IsCertificateAuthority = false);
public enum HostKeyState { Unknown, Trusted, Changed }
public sealed class HostKeyStore(string path)
{
    private readonly object _gate = new();
    public List<HostKey> List() => File.Exists(path) ? JsonSerializer.Deserialize<List<HostKey>>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Archivio host non valido.") : [];
    public HostKeyState Check(string host, int port, string algorithm, string fingerprint, bool certificateAuthority = false)
    {
        lock (_gate)
        {
            var entries = List().Where(k => k.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && k.Port == port).ToList();
            if (entries.Count == 0) return HostKeyState.Unknown;
            return entries.Any(k => k.IsCertificateAuthority == certificateAuthority && NativeIdentity.CanonicalAlgorithm(k.Algorithm) == NativeIdentity.CanonicalAlgorithm(algorithm) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(k.Fingerprint.Replace("SHA256:","")), Encoding.UTF8.GetBytes(fingerprint.Replace("SHA256:","")))) ? HostKeyState.Trusted : HostKeyState.Changed;
        }
    }
    public void Accept(HostKey key) { lock (_gate) { var list = List();if(list.Any(k=>k.Host.Equals(key.Host,StringComparison.OrdinalIgnoreCase)&&k.Port==key.Port&&k.IsCertificateAuthority==key.IsCertificateAuthority&&NativeIdentity.CanonicalAlgorithm(k.Algorithm)==NativeIdentity.CanonicalAlgorithm(key.Algorithm)&&k.Fingerprint.Replace("SHA256:","")==key.Fingerprint.Replace("SHA256:","")&&k.PublicKey==key.PublicKey))return;list.RemoveAll(k=>k.Host.Equals(key.Host,StringComparison.OrdinalIgnoreCase)&&k.Port==key.Port&&k.IsCertificateAuthority==key.IsCertificateAuthority&&NativeIdentity.CanonicalAlgorithm(k.Algorithm)==NativeIdentity.CanonicalAlgorithm(key.Algorithm)&&k.Fingerprint.Replace("SHA256:","")==key.Fingerprint.Replace("SHA256:",""));list.Add(key with { Algorithm=NativeIdentity.CanonicalAlgorithm(key.Algorithm) }); AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(list)); } }
    public void Forget(string host, int port) { lock (_gate) { AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(List().Where(k => !(k.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && k.Port == port)))); } }
}
