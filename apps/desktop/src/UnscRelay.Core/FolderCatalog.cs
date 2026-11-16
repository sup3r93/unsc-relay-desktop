namespace UnscRelay.Core;
public static class FolderCatalog
{
    public static string Normalize(string path)
    {
        var parts = path.Split('/').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        if (parts.Length == 0 || parts.Length > 12 || parts.Any(p => p is "." or ".." || p.Length > 80 || p.Any(char.IsControl)) || path.Length > 500)
            throw new ArgumentException("Nome cartella non valido: usa nomi da 1 a 80 caratteri, senza . o .., fino a 12 livelli.");
        return string.Join('/', parts);
    }
    public static string Add(AppData data, string path)
    {
        var normalized = Normalize(path); var current = "";
        foreach (var part in normalized.Split('/'))
        {
            var candidate = current.Length == 0 ? part : current + "/" + part;
            current = data.Folders.FirstOrDefault(p => p.Equals(candidate, StringComparison.OrdinalIgnoreCase)) ?? candidate;
            if (!data.Folders.Contains(current)) data.Folders.Add(current);
        }
        return current;
    }
    public static void Rename(AppData data, string original, string replacement)
    {
        original = Normalize(original); replacement = Normalize(replacement);
        if (!data.Folders.Contains(original)) throw new ArgumentException("Cartella non trovata.");
        if (replacement.StartsWith(original + "/", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Una cartella non puo diventare figlia di se stessa.");
        if (data.Folders.Any(f => f.Equals(replacement, StringComparison.OrdinalIgnoreCase) && f != original)) throw new ArgumentException("Esiste gia una cartella con questo nome.");
        static bool IsInside(string path, string parent) => path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
        var changed = data.Folders.Select(f => IsInside(f, original) ? replacement + f[original.Length..] : f).ToList();
        if (changed.Distinct(StringComparer.OrdinalIgnoreCase).Count() != changed.Count) throw new ArgumentException("Il nuovo percorso crea un conflitto tra cartelle.");
        data.Folders = changed;
        foreach (var profile in data.Profiles.Where(p => IsInside(p.Folder, original))) profile.Folder = replacement + profile.Folder[original.Length..];
        Add(data, replacement);
    }
    public static void RemoveEmpty(AppData data, string path)
    {
        path = Normalize(path);
        if (data.Profiles.Any(p => p.Folder.Equals(path, StringComparison.OrdinalIgnoreCase) || p.Folder.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)) || data.Folders.Any(f => f.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("La cartella contiene connessioni o sottocartelle. Spostale prima di eliminarla.");
        data.Folders.RemoveAll(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));
    }
}
