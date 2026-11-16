using System.Security.Cryptography;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using UnscRelay.Core;

namespace UnscRelay.App;

static class WindowsHelloService
{
    public static async Task<bool> IsAvailableAsync() => await KeyCredentialManager.IsSupportedAsync();

    public static async Task<(WindowsHelloChallenge Challenge, byte[] Signature)> CreateAsync(WindowsHelloChallenge challenge)
    {
        ValidateKeyName(challenge.KeyName);
        if (!await IsAvailableAsync()) throw new InvalidOperationException("Windows Hello non è configurato. Puoi usare la password principale oppure configurare il PIN nelle impostazioni di Windows.");
        var created = await KeyCredentialManager.RequestCreateAsync(challenge.KeyName, KeyCredentialCreationOption.FailIfExists);
        Check(created.Status);
        try
        {
            CryptographicBuffer.CopyToByteArray(created.Credential.RetrievePublicKey(CryptographicPublicKeyBlobType.X509SubjectPublicKeyInfo), out var publicKey);
            var signature = await SignAsync(created.Credential, challenge.Message);
            return (challenge with { PublicKey = Convert.ToBase64String(publicKey) }, signature);
        }
        catch { await KeyCredentialManager.DeleteAsync(challenge.KeyName); throw; }
    }
    public static async Task<byte[]> SignAsync(WindowsHelloChallenge challenge)
    {
        ValidateKeyName(challenge.KeyName);
        var opened = await KeyCredentialManager.OpenAsync(challenge.KeyName);
        if (opened.Status == KeyCredentialStatus.NotFound) throw new InvalidOperationException("La chiave Windows Hello non è presente su questo PC. Usa la password principale e abilita nuovamente Windows Hello.");
        Check(opened.Status); return await SignAsync(opened.Credential, challenge.Message);
    }
    static async Task<byte[]> SignAsync(KeyCredential credential, string message)
    {
        var result = await credential.RequestSignAsync(CryptographicBuffer.ConvertStringToBinary(message, BinaryStringEncoding.Utf8));
        Check(result.Status); CryptographicBuffer.CopyToByteArray(result.Result, out var signature); return signature;
    }
    public static async Task DeleteAsync(string name) { ValidateKeyName(name); await KeyCredentialManager.DeleteAsync(name); }
    static void ValidateKeyName(string name)
    {
        const string prefix = "UNSC-Relay-Vault-";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new InvalidOperationException("Identificatore Windows Hello non valido.");
    }
    static void Check(KeyCredentialStatus status)
    {
        if (status == KeyCredentialStatus.Success) return;
        if (status is KeyCredentialStatus.UserCanceled or KeyCredentialStatus.UserPrefersPassword) throw new OperationCanceledException("Windows Hello annullato. Puoi usare la password principale.");
        throw new InvalidOperationException("Windows Hello non ha completato la richiesta (" + status + "). Usa la password principale.");
    }
}
