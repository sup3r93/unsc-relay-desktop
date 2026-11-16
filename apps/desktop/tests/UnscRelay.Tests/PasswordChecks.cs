using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using UnscRelay.Core;

internal static class PasswordChecks
{
    public static int Run()
    {
        var count = 0; void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "password-fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "keys.vault"); const string master = "Fixture archive master password!"; const string secret = "Fixture connection secret 123!";
        using var vault = new KeyVault(path); vault.Unlock(master); var key = KeyGeneration.Generate(vault, "SSH fixture", "ed25519"); var privateBytes = vault.Read(key.Id);
        var id = vault.StorePassword("Password fixture", secret);
        Check(vault.List().Count == 1 && vault.Passwords().Single().Id == id, "Passwords are separated from SSH key selection");
        Check(!File.ReadAllText(path).Contains(secret) && !File.ReadAllText(path).Contains("Password fixture"), "Password content and label encrypted at rest");
        try { vault.Read(id); throw new Exception("Password exposed as SSH key"); } catch (InvalidOperationException) { Check(true, "SSH private-key accessor cannot read password records"); }
        Check(vault.Read(key.Id).SequenceEqual(privateBytes), "Adding a password preserves existing SSH private bytes");
        vault.Lock(); vault.Unlock(master); Check(vault.ReadPassword(id) == secret, "Saved connection password survives reopen");
        vault.StorePassword("Updated fixture", "Replacement fixture", id); Check(vault.ReadPassword(id) == "Replacement fixture", "Password can be replaced without changing record identity");
        var profile = JsonSerializer.Deserialize<ConnectionProfile>("{\"name\":\"Fixture\",\"host\":\"localhost\",\"username\":\"fixture\",\"authentication\":\"password\",\"password\":\"" + secret + "\",\"passwordId\":\"" + id + "\"}", ProfileStore.Json)!;
        new ProfileStore(directory).Save(new AppData { Profiles = [profile] }); Check(!File.ReadAllText(Path.Combine(directory, "profiles.json")).Contains(secret), "Password input cannot become a plaintext profile property");
        vault.Delete(id); Check(vault.Passwords().Count == 0 && vault.List().Single().Id == key.Id, "Deleting a connection password preserves SSH keys");
        // Recreate an older envelope whose key metadata had no Kind field.
        var oldRecord = new[] { new { Info = new { key.Id, key.Name, key.Comment, key.Created, key.PublicKey, key.Fingerprint, key.Algorithm, key.Bits }, Content = privateBytes } };
        var salt = RandomNumberGenerator.GetBytes(16); using var argon = new Argon2id(Encoding.UTF8.GetBytes(master)) { Salt = salt, MemorySize = 65536, Iterations = 3, DegreeOfParallelism = 2 }; var derived = argon.GetBytes(32);
        var plain = JsonSerializer.SerializeToUtf8Bytes(oldRecord); var cipher = new byte[plain.Length]; var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16];
        using (var aes = new AesGcm(derived, 16)) aes.Encrypt(nonce, plain, cipher, tag, "UNSC-Relay-SSH-Vault-v1"u8);
        vault.Lock(); File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, Salt = Convert.ToBase64String(salt), Nonce = Convert.ToBase64String(nonce), Tag = Convert.ToBase64String(tag), Ciphertext = Convert.ToBase64String(cipher) }));
        vault.Unlock(master); Check(vault.List().Single().Kind == "ssh-key" && vault.Read(key.Id).SequenceEqual(privateBytes), "Legacy vault without record kinds opens with original key unchanged");
        CryptographicOperations.ZeroMemory(derived); CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(privateBytes);
        Console.WriteLine($"{count} password/archive checks passed"); return 0;
    }
}
