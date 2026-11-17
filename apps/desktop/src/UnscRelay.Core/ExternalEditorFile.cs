using System.Diagnostics;
namespace UnscRelay.Core;

public static class ExternalEditorFile
{
    public static ProcessStartInfo Launch(string executable, string file)
    {
        if (!Path.IsPathFullyQualified(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Seleziona il percorso completo dell’editor EXE.");
        var full = Path.GetFullPath(executable);
        if (!File.Exists(full)) throw new IOException("Editor non trovato. Seleziona nuovamente il programma.");
        return new ProcessStartInfo(full) { UseShellExecute = false, ArgumentList = { Path.GetFullPath(file) } };
    }
    public static string CopyName(string remotePath)
    {
        var leaf = remotePath.Replace('\\', '/').Split('/').Last();
        // A remote name is data, never a Windows device, stream, path or editor option.
        if (leaf.Length is < 1 or > 180 || leaf is "." or ".." || leaf.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) || leaf.EndsWith('.') || leaf.EndsWith(' '))
            leaf = "remote.txt";
        return "relay-" + leaf;
    }
}
