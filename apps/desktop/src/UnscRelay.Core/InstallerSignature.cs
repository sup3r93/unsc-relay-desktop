using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace UnscRelay.Core;

/// <summary>Uses the Windows Authenticode verifier; never changes certificate trust.</summary>
public static class InstallerSignature
{
    public static void Verify(string path, string expectedCertificatePath)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var expected = X509CertificateLoader.LoadCertificateFromFile(expectedCertificatePath);
        // Signed PE extraction has no X509CertificateLoader replacement. Loading the
        // extracted DER certificate uses the modern loader instead of legacy constructors.
#pragma warning disable SYSLIB0057
        using var extracted = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
        using var embedded = X509CertificateLoader.LoadCertificate(extracted.GetRawCertData());
        if (!CryptographicOperations.FixedTimeEquals(expected.GetCertHash(HashAlgorithmName.SHA256), embedded.GetCertHash(HashAlgorithmName.SHA256)))
            throw new CryptographicException("Aggiornamento firmato da un certificato diverso: installazione rifiutata.");

        var file = new FileInfoData { Size = (uint)Marshal.SizeOf<FileInfoData>(), Path = Path.GetFullPath(path) };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoData>());
        try
        {
            Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, File = pointer, ProviderFlags = 0x1000 };
            var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            var result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            // A personal certificate is pinned to this app. An untrusted root is expected;
            // unsigned files, a changed digest, expiration and all other errors are rejected.
            if (result != 0 && result != unchecked((int)0x800B0109))
                throw new CryptographicException($"Firma Authenticode non valida (0x{result:X8}).");
            if (DateTime.Now < embedded.NotBefore || DateTime.Now > embedded.NotAfter)
                throw new CryptographicException("Certificato di firma scaduto o non ancora valido.");
        }
        finally { Marshal.DestroyStructure<FileInfoData>(pointer); Marshal.FreeHGlobal(pointer); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfoData
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr Handle, KnownSubject;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback, SipClient;
        public uint UiChoice, RevocationChecks, UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData, UrlReference;
        public uint ProviderFlags, UiContext;
        public IntPtr SignatureSettings;
    }
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
