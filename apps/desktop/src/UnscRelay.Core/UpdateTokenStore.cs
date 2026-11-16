using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace UnscRelay.Core;

/// <summary>GitHub-only credential, protected for the current Windows user; never part of profile exports.</summary>
public sealed class UpdateTokenStore(string directory)
{
    private string PathName => Path.Combine(directory, "github-token.dpapi");
    public bool Exists => File.Exists(PathName);
    public void Save(string token)
    {
        token = token.Trim();
        if (token.Length is < 10 or > 4096 || token.Any(char.IsWhiteSpace) || token.Any(char.IsControl))
            throw new ArgumentException("Token GitHub non valido.");
        var bytes = Encoding.UTF8.GetBytes(token);
        try { AtomicFile.Write(PathName, Transform(bytes, false)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public string Read()
    {
        if (!Exists) return "";
        if (new FileInfo(PathName).Length > 16384) throw new InvalidDataException("Token salvato non valido. Rimuovilo e inseriscilo nuovamente.");
        var clear = Transform(File.ReadAllBytes(PathName), true);
        try { return Encoding.UTF8.GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Delete() { if (Exists) File.Delete(PathName); }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    internal static byte[] Transform(byte[] bytes, bool decrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La protezione del token richiede Windows.");
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }; Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var ok = decrypt ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptProtectData(ref input, "UNSC Relay / GitHub update token", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new InvalidOperationException(decrypt ? "Impossibile leggere il token su questo account Windows. Inseriscilo nuovamente." : "Impossibile proteggere il token GitHub.", new Win32Exception(Marshal.GetLastWin32Error()));
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) { for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
    }
}
