using System.Security.Cryptography;
namespace UnscRelay.Core;

public sealed partial class KeyVault
{
    // Public identity is obtained by loading the private bytes through original ssh-add stdin into a temporary agent.
    public KeyInfo ImportNative(string name, byte[] content, string validatedPublicKey, string comment = "")
    {
        lock (_gate) { RequireUnlocked(); if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || comment.Length > 1000 || content.Length is < 1 or > 1048576) throw new ArgumentException("Chiave nativa non valida."); var parsed = NativeIdentity.Parse(validatedPublicKey); var info = new KeyInfo(Guid.NewGuid().ToString("N"), name.Trim(), comment.Trim(), DateTime.UtcNow, parsed.PublicKey, parsed.Fingerprint, Algorithm: parsed.Algorithm, Bits: 0, Kind: "ssh-native-key"); var record = new KeyRecord(info, content.ToArray()); _records.Add(record); try { Save(); } catch { _records.Remove(record); CryptographicOperations.ZeroMemory(record.Content); throw; } return info; }
    }
}
