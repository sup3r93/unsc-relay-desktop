using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace UnscRelay.Core;
public sealed record UpdateDownloadProgress(long ReceivedBytes, long? TotalBytes, double BytesPerSecond, double? RemainingSeconds, string Stage);
public sealed record UpdateRelease(string Version, string Notes, long AssetId, string FileName, string Sha256);
public sealed class GitHubUpdates(HttpMessageHandler? handler = null)
{
    private const string Repo = "sup3r93/unsc-relay-desktop";
    private HttpClient Client(string token)
    {
        var client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("UNSC-Relay/0.1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    public async Task<UpdateRelease?> CheckAsync(string token, Version current, CancellationToken cancellation)
    {
        using var client = Client(token);
        using var response = await client.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest", cancellation);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new InvalidOperationException("Nessuna release accessibile. La repository è privata: occorre un token GitHub con accesso in lettura ai contenuti, oppure non è ancora pubblicata una release.");
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) throw new InvalidOperationException("GitHub ha rifiutato l’accesso. Verifica scadenza del token e Contents: Read-only sulla repository unsc-relay-desktop.");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation)); var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString()!;
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version.Build < 0) throw new InvalidDataException("Versione release non valida.");
        if ((root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) || (root.TryGetProperty("prerelease", out var preview) && preview.GetBoolean())) throw new InvalidDataException("La release non è stabile.");
        if (version <= current) return null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString()!;
            if (name != $"UNSC-Relay-Setup-{version}.exe") continue;
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            if (digest is not { Length: 71 } || !digest.StartsWith("sha256:", StringComparison.Ordinal) || !digest[7..].All(Uri.IsHexDigit)) throw new InvalidDataException("Release senza digest SHA-256 valido: aggiornamento rifiutato.");
            return new(tag, root.GetProperty("body").GetString() ?? "", asset.GetProperty("id").GetInt64(), name, digest[7..]);
        }
        throw new InvalidOperationException("La release non contiene un installer Windows compatibile.");
    }
    public async Task<string> DownloadAsync(UpdateRelease release, string token, string directory, CancellationToken cancellation, Action<UpdateDownloadProgress>? progress = null)
    {
        if (Path.GetFileName(release.FileName) != release.FileName || !release.FileName.EndsWith(".exe")) throw new InvalidDataException("Nome installer non valido.");
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + "-" + release.FileName);
        try
        {
            using var client = Client(token); client.DefaultRequestHeaders.Accept.Clear(); client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
            using var response = await client.GetAsync($"https://api.github.com/repos/{Repo}/releases/assets/{release.AssetId}", HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 600 * 1024 * 1024) throw new InvalidDataException("Installer troppo grande.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var target = new FileStream(path, FileMode.CreateNew))
            {
                var buffer = new byte[65536]; long total = 0; int read;
                var length = response.Content.Headers.ContentLength; var watch = System.Diagnostics.Stopwatch.StartNew(); var lastReport = TimeSpan.Zero;
                progress?.Invoke(new(0, length, 0, null, "download"));
                while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
                { total += read; if (total > 600 * 1024 * 1024) throw new InvalidDataException("Installer troppo grande."); await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    if (watch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(250)) {
                        var speed = total / Math.Max(.001, watch.Elapsed.TotalSeconds);
                        progress?.Invoke(new(total, length, speed, length.HasValue ? Math.Max(0, (length.Value-total)/speed) : null, "download")); lastReport=watch.Elapsed;
                    }
                }
                if (length.HasValue && total != length.Value) throw new InvalidDataException("Download incompleto.");
                progress?.Invoke(new(total, length ?? total, 0, null, "verify"));
            }
            await using var check = File.OpenRead(path); var digest = await SHA256.HashDataAsync(check, cancellation);
            if (!CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(release.Sha256))) throw new CryptographicException("Integrità installer non valida.");
            return path;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
}
