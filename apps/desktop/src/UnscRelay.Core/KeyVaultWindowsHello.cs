using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnscRelay.Core;

public sealed record WindowsHelloChallenge(string KeyName, string Message, string PublicKey = "");
internal sealed record WindowsHelloBinding(WindowsHelloChallenge Challenge, WrappedVaultKey WrappedKey);
public static class VaultPasswordPolicy
{
    public static void ValidateCreation(string password, string confirmation)
    {
        if (password.Length < 12) throw new ArgumentException("Usa una password di almeno 12 caratteri.");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal)) throw new ArgumentException("Le due password non coincidono.");
    }
}
public sealed partial class KeyVault
{
    const string HelloDomain = "UNSC Relay / local archive wrapping / v1 / ";
    public WindowsHelloChallenge? WindowsHello() { lock (_gate) return Exists ? ReadEnvelope().WindowsHello?.Challenge : null; }
    public WindowsHelloChallenge PrepareWindowsHello()
    {
        lock (_gate) { RequireUnlocked(); return new("UNSC-Relay-Vault-" + Guid.NewGuid().ToString("N"), HelloDomain + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))); }
    }
    static byte[] HelloAad(WindowsHelloChallenge challenge) => JsonSerializer.SerializeToUtf8Bytes(challenge);
    static byte[] HelloKey(WindowsHelloChallenge challenge, byte[] signature, byte[] salt)
    {
        if (!challenge.KeyName.StartsWith("UNSC-Relay-Vault-", StringComparison.Ordinal) || !challenge.Message.StartsWith(HelloDomain, StringComparison.Ordinal) || signature.Length is < 256 or > 1024) throw new CryptographicException("Risposta Windows Hello non valida.");
        using var rsa = RSA.Create(); var publicKey = Convert.FromBase64String(challenge.PublicKey);
        rsa.ImportSubjectPublicKeyInfo(publicKey, out var read);
        if (read != publicKey.Length || rsa.KeySize < 2048 || !rsa.VerifyData(Encoding.UTF8.GetBytes(challenge.Message), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new CryptographicException("La firma Windows Hello non corrisponde all'archivio.");
        // The signature is a local wrapping secret for this dedicated random message; never export or log it.
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, signature, 32, salt, "UNSC-Relay/Windows-Hello/wrapping/v1"u8.ToArray());
    }
    const string IdentityDomain = "UNSC Relay / local identity proof / v1 / ";
    public WindowsHelloChallenge PrepareWindowsHelloIdentity()
    {
        lock(_gate){var binding=ReadEnvelope().WindowsHello??throw new InvalidOperationException("Windows Hello non è abilitato.");return binding.Challenge with{Message=IdentityDomain+Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))};}
    }
    public void VerifyWindowsHelloIdentity(WindowsHelloChallenge expected,byte[] signature)
    {
        lock(_gate){
            var binding=ReadEnvelope().WindowsHello??throw new InvalidOperationException("Windows Hello non è abilitato.");
            if(expected.KeyName!=binding.Challenge.KeyName||expected.PublicKey!=binding.Challenge.PublicKey||!expected.Message.StartsWith(IdentityDomain,StringComparison.Ordinal)||signature.Length is <256 or >1024)throw new CryptographicException("Prova d’identità non valida.");
            var nonce=Convert.FromBase64String(expected.Message[IdentityDomain.Length..]);if(nonce.Length!=32)throw new CryptographicException("Prova d’identità non valida.");
            using var rsa=RSA.Create();var publicKey=Convert.FromBase64String(expected.PublicKey);rsa.ImportSubjectPublicKeyInfo(publicKey,out var read);
            if(read!=publicKey.Length||rsa.KeySize<2048||!rsa.VerifyData(Encoding.UTF8.GetBytes(expected.Message),signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new CryptographicException("Verifica Windows Hello non riuscita.");
            // A fresh domain-separated proof verifies identity without decrypting or unlocking the archive.
        }
    }
    public void EnableWindowsHello(WindowsHelloChallenge challenge, byte[] signature, string masterPassword)
    {
        lock (_gate)
        {
            VerifyMasterPassword(masterPassword); EnableWindowsHelloForUnlockedVault(challenge,signature);
        }
    }
    public void EnableWindowsHelloForUnlockedVault(WindowsHelloChallenge challenge, byte[] signature)
    {
        lock (_gate)
        {
            RequireUnlocked(); var kek = HelloKey(challenge, signature, _salt!);
            var previous = _windowsHello; var legacy = _passkeys.ToList();
            try
            {
                var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var cipher = new byte[32];
                using (var aes = new AesGcm(kek, 16)) aes.Encrypt(nonce, _key!, cipher, tag, HelloAad(challenge));
                if (!File.Exists(path + ".before-windows-hello")) File.Copy(path, path + ".before-windows-hello", false);
                _windowsHello = new(challenge, new(Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher)));
                _passkeys.Clear(); Save();
            }
            catch { _windowsHello = previous; _passkeys = legacy; throw; }
            finally { CryptographicOperations.ZeroMemory(kek); }
        }
    }
    public void UnlockWindowsHello(WindowsHelloChallenge expected, byte[] signature)
    {
        lock (_gate)
        {
            Lock(); var envelope = ReadEnvelope(); var hello = envelope.WindowsHello ?? throw new InvalidOperationException("Windows Hello non è abilitato per questo archivio.");
            if (hello.Challenge != expected) throw new CryptographicException("L'archivio è cambiato. Riprova l'accesso.");
            var salt = Convert.FromBase64String(envelope.Salt);
            if (salt.Length != 16) throw new InvalidDataException("Archivio non valido.");
            var kek = HelloKey(expected, signature, salt); var key = new byte[32]; byte[]? plain = null;
            try
            {
                var wrap = hello.WrappedKey;
                using (var aes = new AesGcm(kek, 16)) aes.Decrypt(Convert.FromBase64String(wrap.Nonce), Convert.FromBase64String(wrap.Ciphertext), Convert.FromBase64String(wrap.Tag), key, HelloAad(expected));
                var cipher = Convert.FromBase64String(envelope.Ciphertext);
                if (cipher.Length > 32 * 1024 * 1024) throw new InvalidDataException("Archivio troppo grande.");
                plain = new byte[cipher.Length];
                using (var aes = new AesGcm(key, 16)) aes.Decrypt(Convert.FromBase64String(envelope.Nonce), cipher, Convert.FromBase64String(envelope.Tag), plain, PayloadAad(envelope.Version, envelope.Passkeys, hello));
                _records = JsonSerializer.Deserialize<List<KeyRecord>>(plain) ?? [];
                _key = key; key = []; _salt = salt; _passkeys = envelope.Passkeys?.ToList() ?? []; _windowsHello = hello; LastAccessUtc = DateTime.UtcNow;
            }
            catch { Lock(); throw; }
            finally { CryptographicOperations.ZeroMemory(kek); CryptographicOperations.ZeroMemory(key); if (plain != null) CryptographicOperations.ZeroMemory(plain); }
        }
    }
    public void DisableWindowsHello(string password)
    {
        lock (_gate)
        {
            VerifyMasterPassword(password); DisableWindowsHelloForUnlockedVault();
        }
    }
    public void DisableWindowsHelloForUnlockedVault()
    {
        lock (_gate)
        {
            RequireUnlocked(); var previous = _windowsHello; _windowsHello = null;
            try { Save(); } catch { _windowsHello = previous; throw; }
        }
    }
}
