using System.Security.Cryptography;
using System.Text.Json.Nodes;
using UnscRelay.Core;

static class PasskeyChecks
{
    public static void Run(string directory, Action<bool, string> assert)
    {
        var file = Path.Combine(directory, "passkey-test.vault"); const string master = "Passkey fixture master password!";
        using var vault = new KeyVault(file); vault.Unlock(master); var key = KeyGeneration.Generate(vault, "passkey-fixture", "ed25519");
        var expected = vault.Read(key.Id); var prf = RandomNumberGenerator.GetBytes(32);
        var record = new VaultPasskey("test-credential", "Windows Hello fixture", "AA==", "AA==", 0, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), false, DateTime.UtcNow);
        vault.VerifyMasterPassword(master);
        vault.AddVerifiedPasskey(record, prf, vault.VaultSalt());
        assert(File.Exists(file + ".before-passkeys"), "Passkey enrollment preserves original encrypted archive");
        assert(!File.ReadAllText(file).Contains(Convert.ToBase64String(prf)), "PRF secret is never persisted");
        vault.Lock(); vault.UnlockVerifiedPasskey(record.Id, prf, 1);
        assert(vault.Read(key.Id).SequenceEqual(expected), "PRF unlock decrypts existing SSH key exactly");
        vault.Lock();
        try { vault.UnlockVerifiedPasskey(record.Id, RandomNumberGenerator.GetBytes(32), 2); throw new Exception("Wrong PRF accepted"); }
        catch (CryptographicException) { assert(!vault.IsUnlocked, "Wrong PRF fails closed and leaves vault locked"); }
        vault.Unlock(master); assert(vault.Read(key.Id).SequenceEqual(expected), "Master recovery still works after passkey enrollment");
        var fallback = record with { Id = "bitwarden-fixture", Name = "Bitwarden fixture" }; vault.AddVerifiedPasskey(fallback, null, vault.VaultSalt());
        vault.Lock();
        try { vault.UnlockVerifiedPasskey(fallback.Id, prf, 1); throw new Exception("Non-PRF passkey unlocked vault"); }
        catch (InvalidOperationException) { assert(!vault.IsUnlocked, "Non-PRF provider cannot bypass master password"); }
        vault.Unlock(master);
        try { vault.RemovePasskey(record.Id, "wrong password"); throw new Exception("Passkey removed without master"); }
        catch (CryptographicException) { assert(vault.Passkeys().Count == 2, "Removing a passkey requires correct master password"); }
        var saved = File.ReadAllBytes(file); vault.Lock();
        var node = JsonNode.Parse(saved)!; node["Passkeys"]![0]!["Name"] = "modified header"; File.WriteAllText(file, node.ToJsonString());
        try { vault.Unlock(master); throw new Exception("Modified metadata accepted"); }
        catch (CryptographicException) { assert(!vault.IsUnlocked, "Passkey metadata authenticated with encrypted payload"); }
        File.WriteAllBytes(file, saved); vault.Unlock(master); vault.RemovePasskey(record.Id, master); vault.RemovePasskey(fallback.Id, master); vault.Lock(); vault.Unlock(master);
        assert(vault.Passkeys().Count == 0 && vault.Read(key.Id).SequenceEqual(expected), "Removing final passkey preserves master access and SSH keys");
        CryptographicOperations.ZeroMemory(prf); CryptographicOperations.ZeroMemory(expected);
    }
}
