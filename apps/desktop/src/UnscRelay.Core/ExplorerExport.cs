using System.Text.RegularExpressions;
namespace UnscRelay.Core;
public sealed record DragExportEntry(string Local,string Remote,bool Directory,long Size);
public static class ExplorerExport
{
    public const long MaxBytes=512L*1024*1024;
    public static DragExportEntry[] Validate(string root,IEnumerable<DragExportEntry> source)
    {
        root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;var entries=source.ToArray();
        if(entries.Length is <1 or >1000)throw new IOException("Trascina da 1 a 1000 elementi.");long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in entries)
        {
            var full=Path.GetFullPath(entry.Local);if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!seen.Add(full))throw new IOException("Percorsi di esportazione non validi o duplicati.");
            if(!entry.Local.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Percorso locale non canonico.");var parts=entry.Local[root.Length..].Split(Path.DirectorySeparatorChar);if(parts.Length>33)throw new IOException("Massimo 32 livelli di cartelle.");
            foreach(var name in parts)if(name is "." or ".."||name.Length==0||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith('.')||name.EndsWith(' ')||Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))throw new IOException("Nome remoto non compatibile con Windows: "+name);
            if(entry.Size<0||entry.Remote.Length==0||entry.Remote.Any(char.IsControl))throw new IOException("Elemento remoto non valido.");
            if(!entry.Directory){total=checked(total+entry.Size);if(total>MaxBytes)throw new IOException("Massimo 512 MiB per preparazione del trascinamento.");}
        }
        if(entries.Any(e=>!e.Directory&&entries.Any(other=>!ReferenceEquals(e,other)&&other.Local.StartsWith(e.Local+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))))throw new IOException("Conflitto tra file e cartella.");return entries;
    }
    public static long CacheBytes(string root)
    {
        if(!Directory.Exists(root))return 0;return Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}).Sum(path=>new FileInfo(path).Length);
    }
    public static void Prune(string root,DateTime now)
    {
        if(!Directory.Exists(root)||File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))return;
        foreach(var folder in Directory.EnumerateDirectories(root))
        {
            if(!Guid.TryParseExact(Path.GetFileName(folder),"N",out _)||File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint))continue;
            var marker=Path.Combine(folder,".relay-export");if(!File.Exists(marker)||File.GetLastWriteTimeUtc(marker)>now.AddDays(-7))continue;
            try{Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
