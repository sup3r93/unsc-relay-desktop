using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed record UpdatePolicy(string Version,string InstallerSha256,string[] Categories,string Security,string MinimumSupportedVersion)
{
    public bool Blocks(Version installed)=>Security=="critical" && installed<System.Version.Parse(MinimumSupportedVersion);
    public static UpdatePolicy Verify(string bundle,X509Certificate2 publisher,string? expectedVersion=null,string? expectedDigest=null)
    {
        try{return VerifyCore(bundle,publisher,expectedVersion,expectedDigest);}
        catch(Exception ex)when(ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException or ArgumentException){throw new InvalidDataException("Metadati aggiornamento non validi.",ex);}
    }
    static UpdatePolicy VerifyCore(string bundle,X509Certificate2 publisher,string? expectedVersion,string? expectedDigest)
    {
        if(bundle.Length>32768)throw new InvalidDataException("Metadati aggiornamento troppo grandi.");
        using var document=JsonDocument.Parse(bundle);var root=document.RootElement;
        var payload=Convert.FromBase64String(root.GetProperty("payload").GetString()!);
        var signature=Convert.FromBase64String(root.GetProperty("signature").GetString()!);
        using var rsa=publisher.GetRSAPublicKey()??throw new CryptographicException("Firmatario aggiornamenti non compatibile.");
        if(!rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new CryptographicException("Firma dei metadati aggiornamento non valida.");
        var policy=JsonSerializer.Deserialize<UpdatePolicy>(payload,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Metadati aggiornamento mancanti.");
        if(!System.Version.TryParse(policy.Version?.TrimStart('v'),out var version)||version.Build<0||
           !System.Version.TryParse(policy.MinimumSupportedVersion,out var minimum)||minimum.Build<0||minimum>version||
           policy.Security is not("normal" or "important" or "critical")||policy.Categories is not{Length:>0 and <=3}||
           policy.Categories.Distinct().Count()!=policy.Categories.Length||policy.Categories.Any(c=>c is not("interface" or "features" or "security"))||
           policy.Security!="normal"&&!policy.Categories.Contains("security")||
           policy.InstallerSha256 is not{Length:64}||!policy.InstallerSha256.All(Uri.IsHexDigit)||
           expectedVersion!=null&&version!=System.Version.Parse(expectedVersion.TrimStart('v'))||
           expectedDigest!=null&&!string.Equals(expectedDigest,policy.InstallerSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Metadati aggiornamento incoerenti.");
        return policy;
    }
}
public static class UpdateSchedule
{
    public static bool Due(bool startup,bool onStartup,bool daily,DateTimeOffset? lastAttempt,DateTimeOffset now)=>
        startup?onStartup:daily&&(!lastAttempt.HasValue||now-lastAttempt.Value>=TimeSpan.FromDays(1));
}
