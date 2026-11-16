using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace UnscRelay.Core;

public sealed record StartupMacro(string SnippetId,string Name,string Text,Dictionary<string,string> Variables,bool Enabled=false,bool ConfirmEachLaunch=true);

public static class StartupMacros
{
    static readonly Regex Placeholders=new(@"\{\{([a-zA-Z0-9_]+)\}\}",RegexOptions.CultureInvariant);
    public static void Validate(ConnectionProfile profile)
    {
        var macro=profile.StartupMacro;if(macro==null)return;
        if(string.IsNullOrWhiteSpace(macro.SnippetId)||macro.SnippetId.Length>100||string.IsNullOrWhiteSpace(macro.Name)||macro.Name.Length>100||macro.Text==null||macro.Text.Length is <1 or >8192||macro.Text.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t'))||macro.Variables==null||macro.Variables.Count>50)throw new ArgumentException("Macro non valida: massimo 8192 caratteri e 50 variabili.");
        if(macro.Enabled&&profile.Protocol is not("ssh" or "local")&&!(profile.Protocol=="native"&&profile.Native.Kind=="ssh"&&profile.Native.Executable.Length==0))throw new ArgumentException("Le macro di avvio richiedono SSH integrato, OpenSSH incluso o una shell locale.");
        var names=Placeholders.Matches(macro.Text).Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if(macro.Variables.Any(v=>!names.Contains(v.Key)||v.Value==null||v.Value.Length>2048||v.Value.Any(char.IsControl)))throw new ArgumentException("Valori delle variabili della macro non validi.");
        if(macro.Enabled&&names.Any(n=>!macro.Variables.TryGetValue(n,out var value)||string.IsNullOrWhiteSpace(value)))throw new ArgumentException("Compila tutte le variabili della macro prima di abilitarla.");
        if(Expand(macro).Length>8192)throw new ArgumentException("Il comando risultante supera 8192 caratteri.");
    }
    public static string Expand(StartupMacro macro)=>Placeholders.Replace(macro.Text,m=>macro.Variables.GetValueOrDefault(m.Groups[1].Value,m.Value)).Replace("\r\n","\n").Replace('\r','\n');
    public static string? Prepare(ConnectionProfile profile,bool automatic=false)
    {
        if(automatic||profile.StartupMacro?.Enabled!=true)return null;
        Validate(profile);return Expand(profile.StartupMacro);
    }
    public static string ApprovalKey(ConnectionProfile profile)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{profile.Protocol,profile.Host,profile.Port,profile.Username,profile.LocalShell,profile.WslDistribution,profile.StartupDirectory,profile.StartupCommands,profile.Native,profile.StartupMacro},ProfileStore.Json))));
    public static ConnectionProfile LocalProfile(ConnectionProfile profile,string? macro)
    {
        if(macro==null)return profile;
        if(profile.Protocol!="local")throw new ArgumentException("La destinazione non è una shell locale.");
        var commands=(profile.StartupCommands.Length>0?profile.StartupCommands.TrimEnd('\r','\n')+"\n":"")+macro;
        if(commands.Length>8192)throw new ArgumentException("Comandi iniziali e macro superano insieme 8192 caratteri.");
        return profile with{StartupCommands=commands,StartupMacro=null};
    }
}
