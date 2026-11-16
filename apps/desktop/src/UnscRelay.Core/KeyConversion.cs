using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;

namespace UnscRelay.Core;
public sealed record ConversionKey(string Format,string Algorithm,int Bits,string Fingerprint,string PublicKey);
public sealed class KeyConversion : IDisposable
{
    public const string ToolVersion="0.85";
    public const string ToolHash="CAA8870591712C44DCC76A639AE6723C62167EAB6C4F1CB436B3A05D61741302";
    readonly FileStream source; readonly string sourceHash;
    public string SourcePath {get;}
    public ConversionKey Original {get;}
    public string Target {get;}
    public DateTime Expires {get;}=DateTime.UtcNow.AddMinutes(30);
    public KeyConversion(string path,string target,string passphrase)
    {
        if(target is not("ppk" or "openssh"))throw new ArgumentException("Formato di destinazione non supportato.");
        SourcePath=Path.GetFullPath(path);Target=target;
        if(File.GetAttributes(SourcePath).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Seleziona un file locale, non un collegamento.");
        source=new(SourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        try
        {
            if(source.Length is <=0 or >1048576)throw new IOException("La chiave deve essere un file da 1 byte a 1 MiB.");
            var bytes=new byte[(int)source.Length];source.ReadExactly(bytes);source.Position=0;
            try{Original=Inspect(bytes,passphrase);sourceHash=Convert.ToHexString(SHA256.HashData(bytes));}finally{CryptographicOperations.ZeroMemory(bytes);}
        }
        catch{source.Dispose();throw;}
    }
    public static ConversionKey Inspect(byte[] bytes,string passphrase)
    {
        if(bytes.Length is <=0 or >1048576)throw new IOException("La chiave supera 1 MiB o è vuota.");
        var heading=Encoding.ASCII.GetString(bytes.AsSpan(0,Math.Min(bytes.Length,100)));
        var format=heading.StartsWith("PuTTY-User-Key-File-3:",StringComparison.Ordinal)?"ppk3":heading.StartsWith("PuTTY-User-Key-File-2:",StringComparison.Ordinal)?"ppk2":heading.StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----",StringComparison.Ordinal)?"openssh":heading.StartsWith("-----BEGIN ",StringComparison.Ordinal)?"pem":"unknown";
        if(format=="unknown")throw new IOException("Seleziona una chiave privata PPK o OpenSSH/PEM.");
        using var memory=new MemoryStream(bytes,false);using var key=Parse(memory,passphrase);
        var algorithm=key.HostKeyAlgorithms.First(a=>a.Name.StartsWith("ssh-",StringComparison.Ordinal)||a.Name.StartsWith("ecdsa-",StringComparison.Ordinal));
        if(algorithm.Name is not("ssh-ed25519" or "ssh-rsa")&&!algorithm.Name.StartsWith("ecdsa-sha2-nistp",StringComparison.Ordinal))throw new IOException("Conversione verificata disponibile per Ed25519, RSA ed ECDSA.");
        return new(format,algorithm.Name,key.Key.KeyLength,"SHA256:"+Convert.ToBase64String(SHA256.HashData(algorithm.Data)).TrimEnd('='),algorithm.Name+" "+Convert.ToBase64String(algorithm.Data));
    }
    static PrivateKeyFile Parse(Stream stream,string passphrase)
    {
        try{return new PrivateKeyFile(stream,passphrase);}
        catch(Renci.SshNet.Common.SshPassPhraseNullOrEmptyException){throw;}
        catch(Exception ex)when(ex is Renci.SshNet.Common.SshException or CryptographicException){throw new IOException("Impossibile leggere la chiave: passphrase errata oppure formato non supportato.",ex);}
    }
    public static string VerifyTool(string path)
    {
        using var stream=File.OpenRead(path);if(Convert.ToHexString(SHA256.HashData(stream))!=ToolHash)throw new IOException("PuTTYgen non corrisponde alla copia ufficiale prevista. Reinstalla UNSC Relay.");return Path.GetFullPath(path);
    }
    public ConversionKey VerifyResult(string output,string passphrase,out byte[] bytes)
    {
        bytes=[];if(DateTime.UtcNow>Expires)throw new IOException("La conversione è scaduta. Riparti dall’originale.");
        output=Path.GetFullPath(output);if(output.Equals(SourcePath,StringComparison.OrdinalIgnoreCase))throw new IOException("Salva il risultato in un file diverso dall’originale.");
        if(File.GetAttributes(output).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Il risultato non può essere un collegamento.");
        source.Position=0;if(Convert.ToHexString(SHA256.HashData(source))!=sourceHash)throw new IOException("L’originale è cambiato: conversione rifiutata.");source.Position=0;
        using var file=new FileStream(output,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length is <=0 or >1048576)throw new IOException("Risultato vuoto o maggiore di 1 MiB.");var content=new byte[(int)file.Length];file.ReadExactly(content);
        try
        {
            var info=Inspect(content,passphrase);
            if(Target=="ppk"?info.Format!="ppk3":info.Format!="openssh")throw new IOException(Target=="ppk"?"Salva la chiave privata in PPK versione 3.":"Usa Conversions > Export OpenSSH key (force new file format).");
            if(info.Algorithm!=Original.Algorithm||info.PublicKey!=Original.PublicKey)throw new IOException("Il risultato appartiene a una chiave diversa. Nessuna chiave importata.");
            bytes=content;content=[];return info;
        }
        finally{CryptographicOperations.ZeroMemory(content);}
    }
    public void Dispose()=>source.Dispose();
}
