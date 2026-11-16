using System.Security.Cryptography;
using UnscRelay.Core;

internal static class PublishedUpdateChecks
{
    public static async Task<int> Run(bool useSavedToken = false)
    {
        // Designated update credential only; never SSH keys, command-line arguments or diagnostic output.
        var token = useSavedToken ? new UpdateTokenStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UNSC Relay")).Read()
            : Console.ReadLine() ?? throw new InvalidOperationException("Missing credential input");
        if (token.Length == 0) throw new InvalidOperationException("No saved update credential");
        var current = useSavedToken ? Version.Parse(System.Diagnostics.FileVersionInfo.GetVersionInfo(@"A:\UNSC Relay\Librerie\UNSC Relay.exe").ProductVersion!.Split('+')[0]) : new Version(0, 1, 3);
        var updates = new GitHubUpdates();
        var release = await updates.CheckAsync(token, current, CancellationToken.None) ?? throw new Exception("No installable release");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "published-update-" + Guid.NewGuid().ToString("N"));
        var path = await updates.DownloadAsync(release, token, directory, CancellationToken.None); token = "";
        InstallerSignature.Verify(path, Path.Combine(Directory.GetCurrentDirectory(), "src", "UnscRelay.App", "Assets", "Publisher.cer"));
        var bytes = File.ReadAllBytes(path); bytes[0x500] ^= 1; var altered = Path.Combine(directory, "altered.exe"); File.WriteAllBytes(altered, bytes);
        try { InstallerSignature.Verify(altered, Path.Combine(Directory.GetCurrentDirectory(), "src", "UnscRelay.App", "Assets", "Publisher.cer")); throw new Exception("Altered package accepted"); }
        catch (CryptographicException) { File.Delete(altered); }
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { release.Version, release.FileName, DigestVerified = true, SignatureVerified = true, AlteredRejected = true, Installed = false }));
        return 0;
    }
}
