using System.Text.Json;
namespace UnscRelay.Core;

// A deliberately separate, narrow surface: never forward arbitrary WebView RPC calls.
public static class AssistantPolicy
{
    public const int MaximumRequestBytes = 512 * 1024;
    public static readonly string[] Tools = ["relay_status", "relay_profiles", "relay_sessions", "relay_send_input", "relay_disconnect", "relay_terminal_read", "relay_sftp_list", "relay_sftp_read", "relay_sftp_write", "relay_transfer"];
    public static void Validate(string tool, JsonElement args)
    {
        if (!Tools.Contains(tool, StringComparer.Ordinal) || args.ValueKind != JsonValueKind.Object) throw new ArgumentException("Operazione API non consentita.");
        var fields = tool switch {
            "relay_send_input" => new[]{"sessionId","text"},
            "relay_disconnect" or "relay_terminal_read" => new[]{"sessionId"},
            "relay_sftp_list" or "relay_sftp_read" => new[]{"sessionId","path"},
            "relay_sftp_write" => new[]{"sessionId","path","text","expectedHash"},
            "relay_transfer" => new[]{"sessionId","local","remote","direction"}, _ => Array.Empty<string>() };
        if(args.EnumerateObject().Any(p=>!fields.Contains(p.Name,StringComparer.Ordinal)))throw new ArgumentException("Parametro API non consentito.");
        foreach(var name in fields)if(!args.TryGetProperty(name,out var value)||value.ValueKind!=JsonValueKind.String||value.GetString()!.Length==0)throw new ArgumentException("Parametro obbligatorio: "+name);
        if(fields.Contains("sessionId")&&!Guid.TryParse(args.GetProperty("sessionId").GetString(),out _))throw new ArgumentException("Sessione non valida.");
        if(tool=="relay_send_input"&&args.GetProperty("text").GetString()!.Length>8192)throw new ArgumentException("Input troppo lungo.");
        if(tool=="relay_sftp_write"&&!System.Text.RegularExpressions.Regex.IsMatch(args.GetProperty("expectedHash").GetString()!,"^[A-Fa-f0-9]{64}$"))throw new ArgumentException("Serve l'hash SHA-256 ottenuto dalla lettura del file.");
        if(tool=="relay_transfer"&&args.GetProperty("direction").GetString() is not("upload" or "download"))throw new ArgumentException("Direzione non valida.");
        foreach(var name in new[]{"path","local","remote"})if(args.TryGetProperty(name,out var path)&&(path.GetString()!.Length>4096||path.GetString()!.Any(char.IsControl)))throw new ArgumentException("Percorso non valido.");
        if(System.Text.Encoding.UTF8.GetByteCount(args.GetRawText())>MaximumRequestBytes)throw new ArgumentException("Richiesta troppo grande.");
    }
}
