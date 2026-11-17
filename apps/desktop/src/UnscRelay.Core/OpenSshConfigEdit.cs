using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace UnscRelay.Core;

// Edits only an identified prefix. The original unmanaged bytes remain an exact suffix.
public sealed class OpenSshConfigEdit
{
    const string Begin="# BEGIN UNSC RELAY MANAGED ENDPOINTS";
    const string End="# END UNSC RELAY MANAGED ENDPOINTS";
    readonly byte[] original, replacement;
    readonly bool existed;
    public string PathName {get;}
    public string Before {get;}
    public string After {get;}
    public DateTime Expires {get;}=DateTime.UtcNow.AddMinutes(5);
    public OpenSshConfigEdit(string path,IEnumerable<ConnectionProfile> profiles)
    {
        PathName=Path.GetFullPath(path); CheckParents(); existed=File.Exists(PathName);
        if(Directory.Exists(PathName))throw new IOException("Seleziona un file config.");
        if(existed&&File.GetAttributes(PathName).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Il config non può essere un collegamento.");
        original=[];
        if(existed)
        {
            using var input=new FileStream(PathName,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length>1048576)throw new IOException("Config maggiore di 1 MiB.");
            original=new byte[(int)input.Length]; input.ReadExactly(original);
        }
        var bom=original.AsSpan().StartsWith(Encoding.UTF8.Preamble);var offset=bom?3:0;
        Before=new UTF8Encoding(false,true).GetString(original,offset,original.Length-offset);
        var unmanaged=Before;
        if(unmanaged.Contains(Begin,StringComparison.Ordinal)||unmanaged.Contains(End,StringComparison.Ordinal))
        {
            var marker=Regex.Match(unmanaged,@"\A"+Regex.Escape(Begin)+@"\r?\n.*?^"+Regex.Escape(End)+@"\r?\n",RegexOptions.Singleline|RegexOptions.Multiline);
            if(!marker.Success)throw new IOException("Sezione Relay ambigua: nessuna modifica applicata.");
            unmanaged=unmanaged[marker.Length..];
            if(unmanaged.Contains(Begin,StringComparison.Ordinal)||unmanaged.Contains(End,StringComparison.Ordinal))throw new IOException("Sezioni Relay duplicate.");
        }
        if(Regex.IsMatch(unmanaged,@"^\s*(Include|Match)(?:\s|=)",RegexOptions.Multiline|RegexOptions.IgnoreCase))throw new IOException("Include/Match richiedono gestione manuale: usa l'esportazione separata.");
        if(OpenSshConfig.Parse(unmanaged).Warnings.Any(w=>w.StartsWith("Sintassi",StringComparison.Ordinal)||w.StartsWith("Direttiva senza",StringComparison.Ordinal)))throw new IOException("Il file non sembra un config OpenSSH valido.");
        var generated=OpenSshConfig.Export(profiles);var aliases=Regex.Matches(generated,@"^Host (\S+)\s*$",RegexOptions.Multiline).Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(Match line in Regex.Matches(unmanaged,@"^\s*Host(?:\s+|\s*=\s*)([^\r\n#]+)",RegexOptions.Multiline|RegexOptions.IgnoreCase))
            foreach(var token in line.Groups[1].Value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))if(aliases.Contains(token.Trim('"')))throw new IOException("Alias già presente fuori dalla sezione Relay: "+token);
        var newline=Before.Contains("\r\n",StringComparison.Ordinal)?"\r\n":"\n";
        var prefix=Begin+"\n"+generated+"\nHost *\n"+End+"\n";
        After=prefix.Replace("\n",newline,StringComparison.Ordinal)+unmanaged;
        var content=Encoding.UTF8.GetBytes(After);replacement=bom?[..Encoding.UTF8.Preamble,..content]:content;
        if(replacement.Length>1048576)throw new IOException("Il risultato supera 1 MiB.");
    }
    void CheckParents() => FolderSync.CheckLocalParents(Path.GetDirectoryName(PathName)!);
    public string Apply()
    {
        CheckParents();
        if(DateTime.UtcNow>Expires)throw new IOException("Anteprima scaduta: rigenerala.");
        if(File.Exists(PathName)!=existed)throw new IOException("Il config è cambiato dopo l'anteprima.");
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!); CheckParents();
        var temp=PathName+".relay-"+Guid.NewGuid().ToString("N")+".tmp";
        string backup=""; var created=false;
        try
        {
            using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { created=true; output.Write(replacement); output.Flush(true); }
            CheckParents();
            if(existed)
            {
                if(File.GetAttributes(PathName).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Il config è diventato un collegamento.");
                // Deny writes while hashing/replacing; File.Replace needs delete sharing.
                using var current=new FileStream(PathName,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
                if(current.Length!=original.Length||!SHA256.HashData(current).AsSpan().SequenceEqual(SHA256.HashData(original)))throw new IOException("Il config è cambiato dopo l'anteprima: nessuna sovrascrittura.");
                backup=PathName+".relay-backup-"+Guid.NewGuid().ToString("N");File.Replace(temp,PathName,backup);
            }
            else File.Move(temp,PathName,false);
            return backup;
        }
        finally{try{if(created){CheckParents();if(File.Exists(temp))File.Delete(temp);}}catch(IOException){ /* Do not follow a redirected parent during cleanup. */ }}
    }
}
