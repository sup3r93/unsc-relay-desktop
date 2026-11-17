using System.Security.Cryptography;
using System.Text;

namespace UnscRelay.Core;

public sealed record ArchiveKeyGrant(int Version, Guid ArchiveId, string ArchiveOwner, string AccountOwner, Guid DeviceId, string WrappedKey);

// Key transport only. Membership, single ownership and fresh 2FA belong to the authenticated API.
public static class ArchiveKeyCrypto
{
    const int PayloadSize=199;
    public static (byte[] PrivateKey,string PublicKey) Generate()
    {
        using var rsa=RSA.Create(3072);
        return (rsa.ExportPkcs8PrivateKey(),Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
    }
    static void Context(Guid archive,string archiveOwner,string accountOwner,Guid device)
    {
        if(archive==Guid.Empty||device==Guid.Empty||!Owner(archiveOwner)||!Owner(accountOwner))throw new CryptographicException("Destinatario o archivio non valido.");
    }
    static bool Owner(string value)=>value is {Length:64}&&value.All(char.IsAsciiHexDigit);
    static void KeySize(RSA rsa)
    {
        if(rsa.KeySize!=3072||rsa.ExportParameters(false).Exponent is not { } exponent||!exponent.AsSpan().SequenceEqual(new byte[]{1,0,1}))throw new CryptographicException("Chiave di condivisione non compatibile.");
    }
    public static ArchiveKeyGrant Wrap(Guid archive,string archiveOwner,string accountOwner,Guid device,string publicKey,ReadOnlySpan<byte> key)
    {
        Context(archive,archiveOwner,accountOwner,device);
        if(key.Length!=32||publicKey is null||publicKey.Length>2048)throw new CryptographicException("Chiave di archivio o dispositivo non valida.");
        using var rsa=RSA.Create();var encoded=Convert.FromBase64String(publicKey);
        rsa.ImportSubjectPublicKeyInfo(encoded,out var used);KeySize(rsa);
        if(used!=encoded.Length)throw new CryptographicException("Dati aggiuntivi nella chiave del dispositivo.");
        var payload=new byte[PayloadSize];
        try
        {
            "URKEY01"u8.CopyTo(payload);archive.TryWriteBytes(payload.AsSpan(7,16),true,out _);
            Encoding.ASCII.GetBytes(archiveOwner,payload.AsSpan(23,64));Encoding.ASCII.GetBytes(accountOwner,payload.AsSpan(87,64));
            device.TryWriteBytes(payload.AsSpan(151,16),true,out _);key.CopyTo(payload.AsSpan(167));
            return new(1,archive,archiveOwner,accountOwner,device,Convert.ToBase64String(rsa.Encrypt(payload,RSAEncryptionPadding.OaepSHA256)));
        }
        finally{CryptographicOperations.ZeroMemory(payload);}
    }
    public static byte[] Unwrap(ArchiveKeyGrant grant,Guid archive,string archiveOwner,string accountOwner,Guid device,ReadOnlySpan<byte> privateKey)
    {
        Context(archive,archiveOwner,accountOwner,device);
        if(grant is null||grant.Version!=1||grant.ArchiveId!=archive||grant.ArchiveOwner!=archiveOwner||grant.AccountOwner!=accountOwner||grant.DeviceId!=device||grant.WrappedKey is null||grant.WrappedKey.Length!=512||privateKey.Length>4096)throw new CryptographicException("Condivisione non valida per questo account, archivio o dispositivo.");
        using var rsa=RSA.Create();rsa.ImportPkcs8PrivateKey(privateKey,out var used);KeySize(rsa);
        if(used!=privateKey.Length)throw new CryptographicException("Chiave privata del dispositivo non valida.");
        var cipher=Convert.FromBase64String(grant.WrappedKey);var payload=rsa.Decrypt(cipher,RSAEncryptionPadding.OaepSHA256);
        try
        {
            if(payload.Length!=PayloadSize||!payload.AsSpan(0,7).SequenceEqual("URKEY01"u8)||new Guid(payload.AsSpan(7,16),true)!=archive||Encoding.ASCII.GetString(payload.AsSpan(23,64))!=archiveOwner||Encoding.ASCII.GetString(payload.AsSpan(87,64))!=accountOwner||new Guid(payload.AsSpan(151,16),true)!=device)throw new CryptographicException("Il collegamento cifrato non appartiene alla condivisione richiesta.");
            return payload.AsSpan(167,32).ToArray();
        }
        finally{CryptographicOperations.ZeroMemory(payload);}
    }
}
