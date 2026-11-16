using System.Security.Cryptography;
using UnscRelay.Core;

static class KeyMetadataChecks
{
    public static int RunIsolated()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "key-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var count = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); count++; }
        const string password = "Metadata fixture master only!";
        using var vault = new KeyVault(Path.Combine(directory, "keys.vault")); vault.Unlock(password);
        var original = KeyGeneration.Generate(vault, "original fixture", "ed25519"); var privateKey = vault.Read(original.Id);
        var renamed = vault.UpdateMetadata(original.Id, "  Production fixture  ", "  imported key comment  ");
        Check(renamed.Id == original.Id && renamed.Name == "Production fixture" && renamed.Comment == "imported key comment", "Custom key name and comment updated without changing identity");
        Check(renamed.Algorithm == "ssh-ed25519" && renamed.Bits > 0, "Import supplies algorithm and key size");
        Check(renamed.PublicKey == original.PublicKey && renamed.Fingerprint == original.Fingerprint && vault.Read(original.Id).SequenceEqual(privateKey), "Rename preserves public key, fingerprint and private bytes");
        Check(!File.ReadAllText(Path.Combine(directory, "keys.vault")).Contains("Production fixture"), "Key name and comment remain encrypted at rest");
        var saved = File.ReadAllBytes(Path.Combine(directory, "keys.vault"));
        try { vault.UpdateMetadata(original.Id, " ", "invalid"); throw new Exception("Blank key name accepted"); }
        catch (ArgumentException) { Check(saved.SequenceEqual(File.ReadAllBytes(Path.Combine(directory, "keys.vault"))), "Invalid rename leaves archive unchanged"); }
        vault.Lock(); vault.Unlock(password); Check(vault.List().Single().Name == "Production fixture", "Key metadata survives lock and reopen");
        var imported = vault.Import("Custom imported fixture", privateKey, comment: "import naming");
        Check(vault.List().Any(k => k.Id == imported.Id && k.Name == "Custom imported fixture"), "Imported key accepts custom display name");
        vault.Delete(imported.Id); vault.Lock(); vault.Unlock(password);
        Check(vault.List().Count == 1 && vault.List().Single().Id == original.Id && vault.Read(original.Id).SequenceEqual(privateKey), "Deleting selected key preserves other keys and persists");
        CryptographicOperations.ZeroMemory(privateKey); Console.WriteLine(count + " metadata checks passed."); return 0;
    }
}
