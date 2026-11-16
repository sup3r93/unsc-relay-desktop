using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using UnscRelay.Core;

static class WindowsHelloChecks
{
    // Cryptographic fixture only: never invokes Windows Hello or a real user's credentials.
    public static void Run(string directory, Action<bool, string> assert)
    {
        const string master = "Native Hello fixture master!";
        try { VaultPasswordPolicy.ValidateCreation(master, master + " "); throw new Exception("Password mismatch accepted"); }
        catch (ArgumentException) { assert(true, "Archive creation rejects mismatched password confirmation"); }
        try { VaultPasswordPolicy.ValidateCreation("short", "short"); throw new Exception("Short creation password accepted"); }
        catch (ArgumentException) { assert(true, "Archive creation rejects matching but short password"); }
        VaultPasswordPolicy.ValidateCreation(master, master); assert(true, "Matching strong creation password accepted");
        var file = Path.Combine(directory, "native-hello.vault"); using var vault = new KeyVault(file); vault.Unlock(master);
        var info = KeyGeneration.Generate(vault, "hello-ssh-fixture", "ed25519"); var expected = vault.Read(info.Id);
        using var rsa = RSA.Create(2048); var challenge = vault.PrepareWindowsHello() with { PublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()) };
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(challenge.Message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        vault.EnableWindowsHello(challenge, signature, master); var persisted = File.ReadAllBytes(file);
        assert(!File.ReadAllText(file).Contains(Convert.ToBase64String(signature)), "Windows Hello wrapping secret is never saved");
        vault.Lock(); vault.UnlockWindowsHello(challenge, signature);
        assert(vault.Read(info.Id).SequenceEqual(expected), "Native Hello proof decrypts existing SSH keys");
        vault.Lock(); var bad = signature.ToArray(); bad[0] ^= 1;
        try { vault.UnlockWindowsHello(challenge, bad); throw new Exception("Invalid Hello signature accepted"); }
        catch (CryptographicException) { assert(!vault.IsUnlocked, "Invalid Hello proof leaves archive locked"); }
        var json = JsonNode.Parse(persisted)!; json["WindowsHello"]!["WrappedKey"]!["Tag"] = Convert.ToBase64String(new byte[16]); File.WriteAllText(file, json.ToJsonString());
        try { vault.UnlockWindowsHello(challenge, signature); throw new Exception("Modified Hello wrapper accepted"); }
        catch (CryptographicException) { assert(!vault.IsUnlocked, "Modified Hello envelope fails closed"); }
        File.WriteAllBytes(file, persisted); vault.Unlock(master);
        assert(vault.Read(info.Id).SequenceEqual(expected), "Master password recovers Hello-enabled archive");
        vault.DisableWindowsHello(master); vault.Lock(); vault.Unlock(master);
        assert(vault.WindowsHello() == null && vault.Read(info.Id).SequenceEqual(expected), "Disabling Hello preserves password access and keys");
        CryptographicOperations.ZeroMemory(signature); CryptographicOperations.ZeroMemory(bad); CryptographicOperations.ZeroMemory(expected);
    }
}
