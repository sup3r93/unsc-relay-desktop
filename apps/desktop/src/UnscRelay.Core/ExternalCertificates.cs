using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Renci.SshNet;
namespace UnscRelay.Core;
public sealed record CertificateProvider
{
    public string KeyId {get;set;}="";
    public string Executable {get;set;}="";
    public string ArgumentsJson {get;set;}="[\"ssh\",\"certificate\",\"{principal}\",\"{publicKey}\",\"--sign\",\"--principal\",\"{principal}\",\"--not-after\",\"{hours}h\",\"--no-agent\"]";
    public string AuthorityFingerprint {get;set;}="";
    public string Principal {get;set;}="";
    public int Hours {get;set;}=1;
    public bool AutoRenew {get;set;}
    public void Validate()
    {
        if(!Guid.TryParse(KeyId,out _)||Executable.Length>0&&!Path.IsPathFullyQualified(Executable)||Executable.Any(char.IsControl)||Hours is <1 or >8||string.IsNullOrWhiteSpace(Principal)||Principal.Length>100||Principal.Any(char.IsControl))throw new ArgumentException("Configurazione CA esterna non valida.");
        if(!System.Text.RegularExpressions.Regex.IsMatch(AuthorityFingerprint,@"^SHA256:[A-Za-z0-9+/]{43}$"))throw new ArgumentException("Inserisci l'impronta SHA256 della CA fidata.");
        if(ArgumentsJson.Length>16384)throw new ArgumentException("Argomenti troppo lunghi.");var args=JsonSerializer.Deserialize<string[]>(ArgumentsJson)??throw new ArgumentException("Inserisci gli argomenti come elenco JSON.");
        if(args.Length>64||args.Any(a=>a==null||a.Length>4096||a.Contains('\0'))||!args.Any(a=>a.Contains("{publicKey}"))||(!args.Any(a=>a.Contains("{certificate}"))&&Executable.Length>0&&!new[]{"ssh-keygen","step"}.Contains(Path.GetFileNameWithoutExtension(Executable),StringComparer.OrdinalIgnoreCase)))throw new ArgumentException("Usa i segnaposto {publicKey} e {certificate} negli argomenti.");
    }
}
public static class ExternalCertificates
{
    public static void ValidateCertificate(string text,CertificateProvider provider,DateTimeOffset now)
    {
        provider.Validate();var cert=CertificateRevocations.Parse(text);
        if("SHA256:"+cert.CertificateAuthorityKeyFingerPrint!=provider.AuthorityFingerprint)throw new CryptographicException("CA del certificato diversa dall'impronta configurata.");
        if(!cert.ValidPrincipals.Contains(provider.Principal)||cert.ValidAfterUnixSeconds>(ulong)now.ToUnixTimeSeconds()||cert.ValidBeforeUnixSeconds<=(ulong)now.AddMinutes(1).ToUnixTimeSeconds()||cert.ValidBeforeUnixSeconds>(ulong)now.AddHours(provider.Hours).AddMinutes(5).ToUnixTimeSeconds())throw new ArgumentException("Principal o validita del certificato non conformi alla richiesta.");
        CertificateSignatures.Verify(text,cert);
    }
    public static async Task<KeyInfo> Issue(KeyVault vault,CertificateProvider provider,string directory,string passphrase="")
    {
        provider.Validate();if(!vault.IsUnlocked)throw new InvalidOperationException("Sblocca l'archivio prima di richiedere un certificato.");
        var key=vault.List().Single(k=>k.Id==provider.KeyId);if(key.Certificate!=null&&CertificateRevocations.IsRevoked(key.Certificate.Text,vault.Revocations()))throw new InvalidOperationException("Rinnovo automatico vietato per un certificato revocato. Rimuovi esplicitamente l'associazione prima di richiedere un nuovo certificato.");
        var root=Path.Combine(directory,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var pub=Path.Combine(root,"identity.pub");var output=Path.Combine(root,"identity-cert.pub");
        try{
            await File.WriteAllTextAsync(pub,key.PublicKey+"\n",new UTF8Encoding(false));
            var args=JsonSerializer.Deserialize<string[]>(provider.ArgumentsJson)!.Select(a=>a.Replace("{publicKey}",pub).Replace("{certificate}",output).Replace("{principal}",provider.Principal).Replace("{hours}",provider.Hours.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
            var executable=provider.Executable.Length>0?provider.Executable:NativeClient.Find("step")??throw new IOException("Componente CA Step non presente nel pacchetto.");
            if(executable.Contains("Cygwin",StringComparison.OrdinalIgnoreCase))args=args.Select(a=>Path.IsPathFullyQualified(a)?NativeClient.CygwinPath(a):a).ToArray();
            await NativeClient.Capture(executable,args,60);
            if(!File.Exists(output)||new FileInfo(output).Length is <=0 or >1048576)throw new IOException("Il provider non ha prodotto un certificato valido.");
            var text=await File.ReadAllTextAsync(output);ValidateCertificate(text,provider,DateTimeOffset.UtcNow);return vault.AttachCertificate(provider.KeyId,text,passphrase);
        }finally{foreach(var file in new[]{pub,output})if(File.Exists(file))File.Delete(file);try{Directory.Delete(root);}catch(IOException){}}
    }
}
