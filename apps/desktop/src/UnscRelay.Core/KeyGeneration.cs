using System.Security.Cryptography;
using System.Text;

namespace UnscRelay.Core;

public static class KeyGeneration
{
    // Cryptographic primitives and OpenSSH encoding are provided by maintained libraries.
    // Private key bytes are passed straight to the encrypted vault, never a temporary file.
    public static KeyInfo Generate(KeyVault vault, string name, string algorithm)
    {
        if (!vault.IsUnlocked) throw new InvalidOperationException("Sblocca prima l'archivio chiavi.");
        byte[] bytes;
        if (algorithm == "ed25519")
        {
            var seed = RandomNumberGenerator.GetBytes(32);
            try { bytes = Encoding.UTF8.GetBytes(SshNet.Keygen.Extensions.PrivateKeyFileExtension.ToOpenSshFormat(SshNet.Keygen.SshKey.FromEd25519(seed))); }
            finally { CryptographicOperations.ZeroMemory(seed); }
        }
        else if (algorithm is "rsa3072" or "rsa4096" or "rsa8192")
        {
            using var key = RSA.Create(algorithm == "rsa8192" ? 8192 : algorithm == "rsa4096" ? 4096 : 3072); bytes = Encoding.UTF8.GetBytes(key.ExportRSAPrivateKeyPem());
        }
        else if (algorithm is "ecdsa256" or "ecdsa384" or "ecdsa521")
        {
            var curve = algorithm switch { "ecdsa384" => ECCurve.NamedCurves.nistP384, "ecdsa521" => ECCurve.NamedCurves.nistP521, _ => ECCurve.NamedCurves.nistP256 };
            using var key = ECDsa.Create(curve); bytes = Encoding.UTF8.GetBytes(key.ExportECPrivateKeyPem());
        }
        else throw new ArgumentException("Algoritmo non supportato.");
        try { return vault.Import(name, bytes, comment: "Generata localmente / " + algorithm); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
