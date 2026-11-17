using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UnscRelay.Core;

/// <summary>Bounds application metadata to a fully qualified, unlinked local directory.</summary>
public static class LocalDataFiles
{
    public static string Root(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new IOException("La cartella dati locale non è disponibile. Verifica il profilo Windows e riapri Relay.");
        if (directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "." or "..") || directory.Any(char.IsControl))
            throw new IOException("Percorso della cartella dati non valido.");
        var full = Path.GetFullPath(directory);
        FolderSync.CheckLocalParents(full);
        return full;
    }
    public static string ApplicationRoot(string localApplicationData) => Child(Root(localApplicationData), "UNSC Relay");
    public static string Child(string directory, string relative)
    {
        var path = FolderSync.LocalChild(Root(directory), relative.Replace(Path.DirectorySeparatorChar, '/'));
        FolderSync.CheckLocalParents(path);
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("I dati locali non possono essere collegamenti.");
        return path;
    }
    public static FileStream OpenRead(string directory, string relative, long maximumBytes)
    {
        var path = Child(directory, relative);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            // Check the opened object as well as its name: a parent/file replaced between
            // validation and opening must not redirect the read. Windows denies replacement
            // of this handle while it is held because FileShare.Delete is not granted.
            if (OperatingSystem.IsWindows())
            {
                var resolved = new StringBuilder(32768);
                var length = GetFinalPathNameByHandle(stream.SafeFileHandle, resolved, (uint)resolved.Capacity, 0);
                if (length == 0 || length >= resolved.Capacity) throw new IOException("Impossibile verificare il file locale.", new Win32Exception(Marshal.GetLastWin32Error()));
                var final = resolved.ToString();
                if (final.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) final = @"\\" + final[8..];
                else if (final.StartsWith(@"\\?\", StringComparison.Ordinal)) final = final[4..];
                if (!string.Equals(final, path, StringComparison.OrdinalIgnoreCase)) throw new IOException("Il file locale è stato reindirizzato.");
            }
            Child(directory, relative);
            if (stream.Length > maximumBytes) throw new InvalidDataException("File locale troppo grande.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }
    public static byte[] Read(string directory, string relative, int maximumBytes)
    {
        using var stream = OpenRead(directory, relative, maximumBytes);
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
    }
    public static void Write(string directory, string relative, byte[] bytes)
    {
        var path = Child(directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var created = false;
        try
        {
            Child(directory, relative);
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { created = true; file.Write(bytes); file.Flush(true); }
            Child(directory, relative);
            File.Move(temporary, path, true);
        }
        finally { if (created) { FolderSync.CheckLocalParents(temporary); if (File.Exists(temporary)) File.Delete(temporary); } }
    }
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint size, uint flags);
}
