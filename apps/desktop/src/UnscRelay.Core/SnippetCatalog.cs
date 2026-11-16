using System.Text.Json;

namespace UnscRelay.Core;

public static class SnippetCatalog
{
    public static void Validate(Snippet snippet)
    {
        if(snippet?.Shortcut==null||snippet.Shortcut.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(snippet.Shortcut,@"\ACtrl\+Alt\+Shift\+[A-Z0-9]\z"))throw new ArgumentException("Scorciatoia non valida: usa Ctrl+Alt+Shift e una lettera o cifra.");
        if (snippet == null || string.IsNullOrWhiteSpace(snippet.Id) || snippet.Id.Length > 100 || string.IsNullOrWhiteSpace(snippet.Name) || snippet.Name.Length > 100 || snippet.Folder == null || snippet.Folder.Length > 200 || snippet.Text == null || snippet.Text.Length is < 1 or > 65536 || snippet.Text.Contains('\0')) throw new ArgumentException("Snippet non valido: nome obbligatorio (100 caratteri), cartella fino a 200 e testo fino a 65536 caratteri.");
    }
    public static void ValidateShortcut(Snippet snippet,IEnumerable<Snippet> existing)
    {
        Validate(snippet);
        if(snippet.Shortcut.Length>0&&existing.Any(s=>s.Id!=snippet.Id&&s.Shortcut==snippet.Shortcut))throw new ArgumentException("Scorciatoia già assegnata a un altro snippet.");
    }
    public static List<Snippet> Import(byte[] json)
    {
        if (json.Length > 1024 * 1024) throw new ArgumentException("La libreria snippet supera 1 MiB.");
        var snippets = JsonSerializer.Deserialize<List<Snippet>>(json, ProfileStore.Json) ?? throw new InvalidDataException("Libreria snippet non valida.");
        if (snippets.Count > 1000) throw new ArgumentException("Massimo 1000 snippet per importazione.");
        foreach (var snippet in snippets) Validate(snippet);
        return snippets.Select(s => s with { Id = Guid.NewGuid().ToString("N"), Shortcut="" }).ToList();
    }
}
