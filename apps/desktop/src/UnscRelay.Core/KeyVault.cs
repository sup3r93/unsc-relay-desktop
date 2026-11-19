using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Renci.SshNet;

namespace UnscRelay.Core;

public sealed record KeyInfo(string Id, string Name, string Comment, DateTime Created, string PublicKey = "", string Fingerprint = "", SshCertificateInfo? Certificate = null, string Algorithm = "", int Bits = 0, string Kind = "ssh-key", [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<ItemComment>? Discussion = null);
internal sealed record KeyRecord(KeyInfo Info, byte[] Content);
internal sealed record VaultEnvelope(int Version, string Salt, string Nonce, string Tag, string Ciphertext, List<VaultPasskey>? Passkeys = null, WindowsHelloBinding? WindowsHello = null);

public sealed partial class KeyVault(string path) : IDisposable
{
    private static readonly byte[] Aad = "UNSC-Relay-SSH-Vault-v1"u8.ToArray();
    private byte[]? _key;
    private byte[]? _salt;
    private List<VaultPasskey> _passkeys = [];
    private WindowsHelloBinding? _windowsHello;
    private List<KeyRecord> _records = [];
    private readonly object _gate = new();
    public bool Exists => File.Exists(path);
    public bool IsUnlocked => _key != null;
    public DateTime LastAccessUtc { get; private set; } = DateTime.UtcNow;

    public void Unlock(string password)
    {
        lock (_gate)
        {
            Lock();
            if (password.Length < 12) throw new ArgumentException("La password principale deve avere almeno 12 caratteri.");
            if (Exists && new FileInfo(path).Length > 48 * 1024 * 1024) throw new InvalidDataException("Archivio troppo grande.");
            VaultEnvelope? envelope = Exists ? JsonSerializer.Deserialize<VaultEnvelope>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Archivio non valido.") : null;
            ValidateEnvelope(envelope);
            var salt = envelope == null ? RandomNumberGenerator.GetBytes(16) : Convert.FromBase64String(envelope.Salt);
            if (salt.Length != 16) throw new InvalidDataException("Archivio non valido.");
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[]? key = null;
            try
            {
                using var argon = new Argon2id(passwordBytes) { Salt = salt, MemorySize = 65536, Iterations = 3, DegreeOfParallelism = 2 };
                key = argon.GetBytes(32);
                if (envelope != null)
                {
                    var cipher = Convert.FromBase64String(envelope.Ciphertext);
                    if (cipher.Length > 32 * 1024 * 1024) throw new InvalidDataException("Archivio troppo grande.");
                    var plain = new byte[cipher.Length];
                    try
                    {
                        using var aes = new AesGcm(key, 16);
                        aes.Decrypt(Convert.FromBase64String(envelope.Nonce), cipher, Convert.FromBase64String(envelope.Tag), plain, PayloadAad(envelope.Version, envelope.Passkeys, envelope.WindowsHello));
                        _records = JsonSerializer.Deserialize<List<KeyRecord>>(plain) ?? [];
                    }
                    finally { CryptographicOperations.ZeroMemory(plain); }
                }
                _key = key; key = null; _salt = salt; _passkeys = envelope?.Passkeys?.ToList() ?? []; _windowsHello = envelope?.WindowsHello; LastAccessUtc = DateTime.UtcNow;
                if (envelope == null) Save();
            }
            catch { Lock(); throw; }
            finally { CryptographicOperations.ZeroMemory(passwordBytes); if (key != null) CryptographicOperations.ZeroMemory(key); }
        }
    }

    public IReadOnlyList<KeyInfo> List()
    {
        lock (_gate) { RequireUnlocked(); return _records.Where(k => k.Info.Kind is "ssh-key" or "ssh-native-key").Select(k => k.Info).ToList(); }
    }
    public KeyInfo Import(string name, byte[] content, string passphrase = "", string comment = "")
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ArgumentException("Indica un nome (massimo 100 caratteri).");
            if (content.Length > 1024 * 1024) throw new ArgumentException("La chiave supera 1 MiB.");
            using var stream = new MemoryStream(content, false);
            using var parsed = new PrivateKeyFile(stream, passphrase); // Existing parser validates the supplied key.
            var algorithm = parsed.HostKeyAlgorithms.First(a => a.Name.StartsWith("ssh-", StringComparison.Ordinal) || a.Name.StartsWith("ecdsa-", StringComparison.Ordinal));
            var publicKey = algorithm.Name + " " + Convert.ToBase64String(algorithm.Data);
            var fingerprint = "SHA256:" + Convert.ToBase64String(SHA256.HashData(algorithm.Data)).TrimEnd('=');
            if (comment.Length > 1000) throw new ArgumentException("Il commento supera 1000 caratteri.");
            var info = new KeyInfo(Guid.NewGuid().ToString("N"), name.Trim(), comment.Trim(), DateTime.UtcNow, publicKey, fingerprint, Algorithm: algorithm.Name, Bits: parsed.Key.KeyLength);
            var record = new KeyRecord(info, content.ToArray());
            _records.Add(record);
            try { Save(); }
            catch { _records.Remove(record); CryptographicOperations.ZeroMemory(record.Content); throw; }
            return info;
        }
    }
    public byte[] Read(string id)
    {
        lock (_gate) { RequireUnlocked(); return _records.Single(k => k.Info.Id == id && (k.Info.Kind is "ssh-key" or "ssh-native-key")).Content.ToArray(); }
    }
    public KeyInfo UpdateMetadata(string id, string name, string comment)
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ArgumentException("Indica un nome da 1 a 100 caratteri.");
            if (comment.Length > 1000) throw new ArgumentException("Il commento supera 1000 caratteri.");
            var index = _records.FindIndex(k => k.Info.Id == id && (k.Info.Kind is "ssh-key" or "ssh-native-key"));
            if (index < 0) throw new ArgumentException("Chiave non trovata nell'archivio.");
            var original = _records[index];
            var updated = original with { Info = original.Info with { Name = name.Trim(), Comment = comment.Trim() } };
            _records[index] = updated;
            try { Save(); } catch { _records[index] = original; throw; }
            return updated.Info;
        }
    }
    public void Delete(string id)
    {
        lock (_gate)
        {
            RequireUnlocked(); var record = _records.Single(k => k.Info.Id == id);
            if (record.Info.Kind == "ssh-certificate-revocation") throw new InvalidOperationException("Le revoche non possono essere eliminate dall’archivio.");
            _records.Remove(record);
            try { Save(); CryptographicOperations.ZeroMemory(record.Content); }
            catch { _records.Add(record); throw; }
        }
    }
    public void Backup(string destination) { lock (_gate) { if (!Exists) throw new InvalidOperationException("Archivio non ancora creato."); File.Copy(path, destination, true); } }
    public void Lock()
    {
        lock (_gate)
        {
            foreach (var record in _records) CryptographicOperations.ZeroMemory(record.Content);
            _records.Clear(); if (_key != null) CryptographicOperations.ZeroMemory(_key); _key = null; _salt = null; _passkeys.Clear(); _windowsHello = null;
        }
    }
    private void RequireUnlocked() { if (_key == null) throw new InvalidOperationException("Sblocca prima l'archivio chiavi."); LastAccessUtc = DateTime.UtcNow; }
    private void Save()
    {
        RequireUnlocked();
        var plain = JsonSerializer.SerializeToUtf8Bytes(_records);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var cipher = new byte[plain.Length];
            var version = _windowsHello != null ? 3 : _passkeys.Count > 0 ? 2 : 1;
            using var aes = new AesGcm(_key!, 16); aes.Encrypt(nonce, plain, cipher, tag, PayloadAad(version, _passkeys, _windowsHello));
            AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(new VaultEnvelope(version, Convert.ToBase64String(_salt!), Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher), version >= 2 ? _passkeys : null, _windowsHello)));
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void Dispose() => Lock();
}
