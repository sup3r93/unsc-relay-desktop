using System.Security.Cryptography;

namespace UnscRelay.Core;

public sealed record SyncEntry(string Relative, bool Directory, long Size, DateTime Modified, string? Hash = null, bool Link = false);
public sealed record SyncDifference(string Relative, SyncEntry? Local, SyncEntry? Remote, string Status, bool Transferable);
public static class FolderSync
{
    public const int MaxEntries = 1000;
    public const long MaxHashBytes = 512L * 1024 * 1024;
    public static string LocalChild(string root, string relative)
    {
        ValidateRelative(relative);
        foreach (var part in relative.Split('/'))
            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.EndsWith('.') || part.EndsWith(' ') ||
                System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new IOException("Nome non compatibile con Windows: " + relative);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException("Percorso esterno alla cartella selezionata.");
        return full;
    }
    public static void ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Any(char.IsControl) || path.Split('/').Any(p => p is "" or "." or "..")) throw new IOException("Percorso relativo non valido.");
    }
    public static void CheckLocalParents(string path)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(path)); dir != null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("La cartella locale contiene un collegamento: " + dir.FullName);
    }
    public static List<SyncEntry> ScanLocal(string root, out int skipped)
    {
        CheckLocalParents(root); var result = new List<SyncEntry>(); var links = 0;
        void Walk(string directory, string prefix, int depth)
        {
            if (depth > 32) throw new IOException("Massimo 32 livelli di cartelle.");
            foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if (result.Count >= MaxEntries) throw new IOException("Confronta al massimo 1000 elementi per lato.");
                if (item.Attributes.HasFlag(FileAttributes.ReparsePoint)) { links++; result.Add(new(prefix + item.Name, false, 0, item.LastWriteTimeUtc, Link: true)); continue; }
                var relative = prefix + item.Name; ValidateRelative(relative); LocalChild(root, relative);
                var folder = item.Attributes.HasFlag(FileAttributes.Directory);
                result.Add(new(relative, folder, folder ? 0 : ((FileInfo)item).Length, item.LastWriteTimeUtc));
                if (folder) Walk(item.FullName, relative + "/", depth + 1);
            }
        }
        Walk(root, "", 0); skipped = links; return result;
    }
    public static List<SyncDifference> Compare(IReadOnlyList<SyncEntry> local, IReadOnlyList<SyncEntry> remote)
    {
        foreach (var entry in local.Concat(remote)) ValidateRelative(entry.Relative);
        if (local.Count > MaxEntries || remote.Count > MaxEntries) throw new IOException("Troppi elementi nel confronto.");
        var l = local.ToDictionary(e => e.Relative, StringComparer.Ordinal); var r = remote.ToDictionary(e => e.Relative, StringComparer.Ordinal);
        var names = l.Keys.Union(r.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var blocked = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).SelectMany(g => g).ToList();
        blocked.AddRange(local.Concat(remote).Where(e => e.Link).Select(e => e.Relative));
        blocked.AddRange(names.Where(n => l.ContainsKey(n) && r.ContainsKey(n) && l[n].Directory != r[n].Directory));
        return names.Select(name =>
        {
            l.TryGetValue(name, out var left); r.TryGetValue(name, out var right);
            var conflict = blocked.Any(b => name.Equals(b, StringComparison.OrdinalIgnoreCase) || name.StartsWith(b + "/", StringComparison.OrdinalIgnoreCase));
            if (conflict) return new SyncDifference(name, left, right, "Conflitto nome/tipo", false);
            if (left == null) return new SyncDifference(name, left, right, "Solo remoto", true);
            if (right == null) return new SyncDifference(name, left, right, "Solo locale", true);
            if (left.Directory) return new SyncDifference(name, left, right, "Cartella presente", false);
            if (left.Size != right.Size) return new SyncDifference(name, left, right, "Contenuto diverso", true);
            if (left.Hash != null && right.Hash != null) return new SyncDifference(name, left, right, left.Hash == right.Hash ? "Identico" : "Contenuto diverso", left.Hash != right.Hash);
            return new SyncDifference(name, left, right, "Contenuto non verificato", false);
        }).ToList();
    }
    public static string HashLocal(string path)
    {
        CheckLocalParents(Path.GetDirectoryName(path)!);
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("File locale collegato non ammesso.");
        using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream));
    }
}
