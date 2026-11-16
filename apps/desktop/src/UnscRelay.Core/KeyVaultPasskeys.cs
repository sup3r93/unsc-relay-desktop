using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace UnscRelay.Core;

public sealed record PasskeyInfo(string Id, string Name, bool Passwordless, DateTime Created);
internal sealed record WrappedVaultKey(string Nonce, string Tag, string Ciphertext);
internal sealed record VaultPasskey(string Id, string Name, string PublicKey, string UserHandle, uint Counter,
    string PrfSalt, bool BackupEligible, DateTime Created, WrappedVaultKey? WrappedKey = null);

public sealed partial class KeyVault
{
    // The master-derived key remains the data key, so older archives remain recoverable with the same password.
    // PRF wraps that key; neither the PRF output nor a decrypted private SSH key is stored in a sidecar.
    static byte[] PayloadAad(int version, List<VaultPasskey>? passkeys, WindowsHelloBinding? hello = null) => version switch {
        1 => Aad,
        2 => [.. "UNSC-Relay-SSH-Vault-v2"u8, .. JsonSerializer.SerializeToUtf8Bytes(passkeys ?? [])],
        3 => [.. "UNSC-Relay-SSH-Vault-v3"u8, .. JsonSerializer.SerializeToUtf8Bytes(new { Passkeys = passkeys ?? [], WindowsHello = hello })],
        _ => throw new InvalidDataException("Versione archivio non valida.")
    };

    static void ValidateEnvelope(VaultEnvelope? envelope)
    {
        if (envelope == null) return;
        if (envelope.Version is not (1 or 2 or 3) || (envelope.Version == 1 && envelope.Passkeys?.Count > 0) || envelope.Passkeys?.Count > 20 || (envelope.WindowsHello != null && envelope.Version != 3) || (envelope.Version == 3 && envelope.WindowsHello == null))
            throw new InvalidDataException("Versione o metadati archivio non validi.");
        if (envelope.WindowsHello is {} hello && (hello.Challenge.KeyName.Length > 100 || hello.Challenge.Message.Length > 512 || hello.Challenge.PublicKey.Length > 8192)) throw new InvalidDataException("Metadati Windows Hello non validi.");
        if (envelope.Passkeys is { } items && (items.Select(p => p.Id).Distinct().Count() != items.Count || items.Any(p =>
            p.Id.Length > 2048 || p.Name.Length > 100 || p.PublicKey.Length > 4096 || p.UserHandle.Length > 128 || p.PrfSalt.Length > 100)))
            throw new InvalidDataException("Metadati passkey non validi.");
    }
    VaultEnvelope ReadEnvelope()
    {
        if (!Exists || new FileInfo(path).Length > 48 * 1024 * 1024) throw new InvalidDataException("Archivio non disponibile o troppo grande.");
        var envelope = JsonSerializer.Deserialize<VaultEnvelope>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Archivio non valido.");
        ValidateEnvelope(envelope); return envelope;
    }
    internal IReadOnlyList<VaultPasskey> PasskeyRecords() { lock (_gate) return Exists ? ReadEnvelope().Passkeys?.ToArray() ?? [] : []; }
    public IReadOnlyList<PasskeyInfo> Passkeys() => PasskeyRecords().Select(p => new PasskeyInfo(p.Id, p.Name, p.WrappedKey != null, p.Created)).ToArray();

    public void VerifyMasterPassword(string password)
    {
        lock (_gate)
        {
            RequireUnlocked(); var bytes = Encoding.UTF8.GetBytes(password); byte[]? candidate = null;
            try
            {
                using var argon = new Argon2id(bytes) { Salt = _salt!, MemorySize = 65536, Iterations = 3, DegreeOfParallelism = 2 };
                candidate = argon.GetBytes(32);
                if (!CryptographicOperations.FixedTimeEquals(candidate, _key!)) throw new CryptographicException("Password principale errata.");
            }
            finally { CryptographicOperations.ZeroMemory(bytes); if (candidate != null) CryptographicOperations.ZeroMemory(candidate); }
        }
    }
    static byte[] WrapAad(VaultPasskey passkey) => Encoding.UTF8.GetBytes("UNSC-Relay/passkey/localhost/" + passkey.Id);
    static byte[] DerivePrfKey(byte[] prf, byte[] salt)
    {
        if (prf.Length != 32) throw new CryptographicException("Il provider non ha restituito un risultato PRF valido.");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, prf, 32, salt, "UNSC-Relay/vault-key-wrap/v1"u8.ToArray());
    }
    internal void AddVerifiedPasskey(VaultPasskey passkey, byte[]? prf, string expectedSalt)
    {
        lock (_gate)
        {
            RequireUnlocked();
            if (Convert.ToBase64String(_salt!) != expectedSalt) throw new InvalidOperationException("L'archivio è cambiato durante la registrazione.");
            if (_passkeys.Count >= 20 || _passkeys.Any(p => p.Id == passkey.Id)) throw new InvalidOperationException("Passkey già presente o limite raggiunto.");
            if (prf != null)
            {
                var kek = DerivePrfKey(prf, _salt!);
                try
                {
                    var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var cipher = new byte[32];
                    using var aes = new AesGcm(kek, 16); aes.Encrypt(nonce, _key!, cipher, tag, WrapAad(passkey));
                    passkey = passkey with { WrappedKey = new(Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher)) };
                }
                finally { CryptographicOperations.ZeroMemory(kek); }
            }
            // Preserve the original encrypted archive before the first format upgrade. Never overwrite an existing backup.
            if (_passkeys.Count == 0 && !File.Exists(path + ".before-passkeys")) File.Copy(path, path + ".before-passkeys", false);
            _passkeys.Add(passkey);
            try { Save(); } catch { _passkeys.Remove(passkey); throw; }
        }
    }
    internal string VaultSalt() { lock (_gate) { RequireUnlocked(); return Convert.ToBase64String(_salt!); } }
    internal void UnlockVerifiedPasskey(string id, byte[] prf, uint counter)
    {
        lock (_gate)
        {
            Lock(); var envelope = ReadEnvelope(); var passkey = envelope.Passkeys?.Single(p => p.Id == id) ?? throw new CryptographicException();
            var wrap = passkey.WrappedKey ?? throw new InvalidOperationException("Questa passkey richiede anche la password principale.");
            var salt = Convert.FromBase64String(envelope.Salt);
            if (salt.Length != 16) throw new InvalidDataException("Archivio non valido.");
            var kek = DerivePrfKey(prf, salt); var dataKey = new byte[32]; byte[]? plain = null;
            try
            {
                using (var aes = new AesGcm(kek, 16)) aes.Decrypt(Convert.FromBase64String(wrap.Nonce), Convert.FromBase64String(wrap.Ciphertext), Convert.FromBase64String(wrap.Tag), dataKey, WrapAad(passkey));
                var cipher = Convert.FromBase64String(envelope.Ciphertext);
                if (cipher.Length > 32 * 1024 * 1024) throw new InvalidDataException("Archivio troppo grande.");
                plain = new byte[cipher.Length];
                using (var aes = new AesGcm(dataKey, 16)) aes.Decrypt(Convert.FromBase64String(envelope.Nonce), cipher, Convert.FromBase64String(envelope.Tag), plain, PayloadAad(envelope.Version, envelope.Passkeys, envelope.WindowsHello));
                _records = JsonSerializer.Deserialize<List<KeyRecord>>(plain) ?? [];
                _key = dataKey; dataKey = []; _salt = salt; _passkeys = envelope.Passkeys!.ToList(); _windowsHello = envelope.WindowsHello;
                AdvancePasskey(id, counter);
            }
            catch { Lock(); throw; }
            finally { CryptographicOperations.ZeroMemory(kek); CryptographicOperations.ZeroMemory(dataKey); if (plain != null) CryptographicOperations.ZeroMemory(plain); }
        }
    }
    public void AdvancePasskey(string id, uint counter)
    {
        lock (_gate)
        {
            RequireUnlocked(); var index = _passkeys.FindIndex(p => p.Id == id);
            if (index < 0) throw new CryptographicException(); var original = _passkeys[index];
            _passkeys[index] = original with { Counter = counter };
            try { Save(); } catch { _passkeys[index] = original; throw; }
        }
    }
    public void RemovePasskey(string id, string masterPassword)
    {
        lock (_gate)
        {
            VerifyMasterPassword(masterPassword); var original = _passkeys.ToList();
            if (_passkeys.RemoveAll(p => p.Id == id) != 1) throw new ArgumentException("Passkey non trovata.");
            try { Save(); } catch { _passkeys = original; throw; }
        }
    }
}
