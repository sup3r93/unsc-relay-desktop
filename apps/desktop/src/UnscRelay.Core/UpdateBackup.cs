using System.Security.Cryptography;
using System.Text.Json;

namespace UnscRelay.Core;

public static class UpdateBackup
{
    // Explicit allowlist: no WebView cache, downloads or plaintext private key exports.
    public static readonly string[] DataFiles = ["profiles.json", "keys.vault", "hostkeys.json", "github-token.dpapi", "transfers.json", "account-sync.dpapi"];
    public static string Create(string dataDirectory, string version)
    {
        var directory = Path.Combine(dataDirectory, "UpdateBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var hashes = new Dictionary<string, string>();
        foreach (var name in DataFiles)
        {
            var source = Path.Combine(dataDirectory, name); if (!File.Exists(source)) continue;
            var target = Path.Combine(directory, name); File.Copy(source, target, false);
            using var stream = File.OpenRead(target); hashes[name] = Convert.ToHexString(SHA256.HashData(stream));
        }
        AtomicFile.Write(Path.Combine(directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(new { version, createdUtc = DateTime.UtcNow, hashes }, ProfileStore.Json));
        return directory;
    }
}
