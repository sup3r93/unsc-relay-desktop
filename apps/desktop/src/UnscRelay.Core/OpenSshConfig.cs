using System.Text;
using System.Text.RegularExpressions;

namespace UnscRelay.Core;

public sealed record SshConfigCandidate(string Alias, ConnectionProfile? Profile, string[] Warnings);
public sealed record SshConfigPreview(SshConfigCandidate[] Candidates, string[] Warnings);
public static class OpenSshConfig
{
    sealed record Block(string[] Patterns, List<(string Key, string Value)> Values);
    static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase) { "hostname", "user", "port", "connecttimeout", "identityfile", "serveraliveinterval", "localforward", "remoteforward", "dynamicforward", "proxyjump" };
    static bool Literal(string value) => value.Length is > 0 and <= 100 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
    public static SshConfigPreview Parse(string text)
    {
        if (text.Length > 1048576) throw new ArgumentException("Configurazione troppo grande (massimo 1 MiB).");
        var blocks = new List<Block>(); var current = new Block(["*"], []); blocks.Add(current);
        var warnings = new List<string>(); bool unsafeSyntax = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim(); if (line.Length == 0 || line.StartsWith('#')) continue;
            var match = Regex.Match(line, @"^(?<key>[A-Za-z][A-Za-z0-9]*)\s*(?:=\s*|\s+)(?<value>.*)$", RegexOptions.CultureInvariant);
            if (!match.Success) { unsafeSyntax = true; warnings.Add("Sintassi non riconosciuta: importazione sospesa."); continue; }
            var key = match.Groups["key"].Value.ToLowerInvariant();
            var tokens = Tokens(match.Groups["value"].Value);
            if (tokens.Length == 0) { unsafeSyntax = true; warnings.Add("Direttiva senza valore: " + key); continue; }
            if (key is "match" or "include") { unsafeSyntax = true; warnings.Add(key + ": richiede valutazione OpenSSH; nessun comando o file incluso viene eseguito o letto."); continue; }
            if (key == "host") { current = new(tokens, []); blocks.Add(current); continue; }
            if(key is "localforward" or "remoteforward") { current.Values.Add((key,string.Join(" ",tokens))); continue; }
            if (tokens.Length != 1) { unsafeSyntax = true; warnings.Add("Direttiva multivalore non supportata: " + key); continue; }
            current.Values.Add((key, tokens[0]));
        }
        var aliases = blocks.SelectMany(b => b.Patterns).Where(Literal).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (aliases.Length > 1000) throw new ArgumentException("Troppi alias (massimo 1000).");
        var result = new List<SshConfigCandidate>();
        foreach (var alias in aliases)
        {
            var notes = new List<string>(); var tunnels = new List<TunnelRule>(); var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var block in blocks.Where(b => Applies(b.Patterns, alias))) foreach (var (key, value) in block.Values)
            {
                if (!Supported.Contains(key)) { notes.Add("Direttiva non rappresentabile: " + key + ". Profilo escluso per evitare impostazioni diverse dall'originale."); continue; }
                if(key is "localforward" or "remoteforward" or "dynamicforward")
                {
                    try{tunnels.Add(ParseForward(key,value));}catch(ArgumentException ex){notes.Add("Direttiva non rappresentabile: "+key+". "+ex.Message);}continue;
                }
                if (key == "identityfile") { notes.Add("IdentityFile: " + value + ". La chiave non viene letta: importala nel Client Key Manager e assegnala al profilo."); continue; }
                values.TryAdd(key, value); // OpenSSH: first obtained value wins.
            }
            ConnectionProfile? profile = null;
            if (!unsafeSyntax && !notes.Any(n => n.StartsWith("Direttiva non rappresentabile")))
            {
                try
                {
                    var host = values.GetValueOrDefault("hostname", alias).Replace("%h", alias, StringComparison.Ordinal);
                    var user = values.GetValueOrDefault("user", "");
                    if (host.Contains('%') || host.Contains('$') || user.Contains('%') || user.Contains('$')) throw new ArgumentException("Token o variabili non supportati.");
                    profile = new() { Name = alias, Folder = "OpenSSH", Host = host, Username = user,
                        Port = int.Parse(values.GetValueOrDefault("port", "22"), System.Globalization.CultureInfo.InvariantCulture),
                        ConnectTimeoutSeconds = int.Parse(values.GetValueOrDefault("connecttimeout", "20"), System.Globalization.CultureInfo.InvariantCulture),
                        KeepAliveSeconds = int.Parse(values.GetValueOrDefault("serveraliveinterval", "0"),System.Globalization.CultureInfo.InvariantCulture),
                        Tunnels=tunnels, Authentication = "auto", Notes = "Importato da OpenSSH. Verificare autenticazione prima della connessione.\n" + string.Join("\n", notes) };
                    profile.Validate();
                    if(tunnels.Count>0)notes.Add("Tunnel importati disattivati: rivedili e attivali esplicitamente nel gestore tunnel.");
                }
                catch (Exception e) when (e is ArgumentException or FormatException or OverflowException) { notes.Add(e.Message); profile = null; }
            }
            result.Add(new(alias, profile, notes.Distinct().ToArray()));
        }
        if (unsafeSyntax) warnings.Add("Nessun profilo importabile finche il file contiene direttive o sintassi non valutabili in sicurezza.");
        if (aliases.Length == 0) warnings.Add("Nessun alias Host esplicito trovato; i pattern non creano macchine automaticamente.");
        var jumpAliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in result.Where(x=>x.Profile!=null).ToArray())
        {
            var jump=blocks.Where(b=>Applies(b.Patterns,item.Alias)).SelectMany(b=>b.Values).Where(v=>v.Key=="proxyjump").Select(v=>v.Value).FirstOrDefault();
            if(jump!=null&&!jump.Equals("none",StringComparison.OrdinalIgnoreCase))jumpAliases[item.Alias]=jump;
        }
        foreach(var (alias,jump) in jumpAliases)
        {
            var index=result.FindIndex(c=>c.Alias==alias);var target=result.SingleOrDefault(c=>c.Alias.Equals(jump,StringComparison.OrdinalIgnoreCase));
            if(!Literal(jump)||target?.Profile==null)result[index]=result[index] with {Profile=null,Warnings=[..result[index].Warnings,"ProxyJump richiede un singolo alias Host esplicito importabile; configura catene tramite gli alias."]};
            else result[index]=result[index] with {Profile=result[index].Profile! with {JumpHost=target.Profile.Id},Warnings=[..result[index].Warnings,"Jump host: "+target.Alias+". Anche i profili dipendenti saranno inclusi nell'importazione."]};
        }
        bool invalidated;
        do
        {
            invalidated=false;foreach(var item in result.Where(c=>c.Profile!=null&&c.Profile.JumpHost.Length>0).ToArray())
            {
                try{SshRoutes.Resolve(item.Profile!,result.Where(c=>c.Profile!=null).Select(c=>c.Profile!).ToArray());}
                catch(ArgumentException ex){var index=result.IndexOf(item);result[index]=item with {Profile=null,Warnings=[..item.Warnings,ex.Message]};invalidated=true;}
            }
        }while(invalidated);
        return new(result.ToArray(), warnings.Distinct().ToArray());
    }
    public static ConnectionProfile[] SelectForImport(SshConfigPreview preview,string[] aliases)
    {
        if(aliases.Length==0||aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=aliases.Length)throw new ArgumentException("Seleziona alias distinti.");
        var chosen=new Dictionary<string,ConnectionProfile>();var available=preview.Candidates.Where(c=>c.Profile!=null).Select(c=>c.Profile!).ToArray();
        foreach(var alias in aliases)
        {
            var profile=preview.Candidates.SingleOrDefault(c=>c.Alias.Equals(alias,StringComparison.OrdinalIgnoreCase))?.Profile??throw new ArgumentException("Alias non importabile: "+alias);
            foreach(var hop in SshRoutes.Resolve(profile,available))chosen.TryAdd(hop.Id,hop);
        }
        var ids=chosen.Keys.ToDictionary(id=>id,_=>Guid.NewGuid().ToString("N"));
        return chosen.Values.Select(p=>p with {Id=ids[p.Id],JumpHost=p.JumpHost.Length==0?"":ids[p.JumpHost]}).ToArray();
    }
    static TunnelRule ParseForward(string key,string value)
    {
        var fields=value.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(fields.Length!=(key=="dynamicforward"?1:2))throw new ArgumentException("Usa porte TCP e una destinazione host:porta.");
        var bind=fields[0];if(bind.StartsWith("127.0.0.1:",StringComparison.Ordinal))bind=bind[10..];
        if(!uint.TryParse(bind,out var port)||port is <1 or >65535)throw new ArgumentException("Bind supportato: porta oppure 127.0.0.1:porta; nessun ascolto pubblico.");
        var host="";uint target=0;
        if(fields.Length==2){var match=Regex.Match(fields[1],@"^(?:\[(?<v6>[0-9a-fA-F:]+)\]|(?<host>[a-zA-Z0-9._-]+)):(?<port>[0-9]+)$");if(!match.Success||!uint.TryParse(match.Groups["port"].Value,out target))throw new ArgumentException("Destinazione TCP non rappresentabile.");host=match.Groups["v6"].Success?match.Groups["v6"].Value:match.Groups["host"].Value;}
        var rule=new TunnelRule(Guid.NewGuid().ToString("N"),key=="localforward"?"local":key=="remoteforward"?"remote":"socks",port,host,target,false);rule.Validate();return rule;
    }
    static bool Applies(string[] patterns, string alias)
    {
        bool Match(string pattern) => Regex.IsMatch(alias, "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return patterns.Any(p => !p.StartsWith('!') && Match(p)) && !patterns.Any(p => p.StartsWith('!') && Match(p[1..]));
    }
    static string[] Tokens(string value)
    {
        var tokens = new List<string>(); var word = new StringBuilder(); bool quote = false, escaped = false;
        foreach (char c in value)
        {
            if (escaped) { if (c is '"' or '\\') word.Append(c); else word.Append('\\').Append(c); escaped = false; continue; }
            if (c == '\\') { escaped = true; continue; }
            if (c == '"') { quote = !quote; continue; }
            if (c == '#' && !quote) break;
            if (char.IsWhiteSpace(c) && !quote) { if (word.Length > 0) { tokens.Add(word.ToString()); word.Clear(); } }
            else word.Append(c);
        }
        if (quote || escaped) throw new ArgumentException("Virgolette o escape incompleti nella configurazione.");
        if (word.Length > 0) tokens.Add(word.ToString()); return tokens.ToArray();
    }
    public static string Export(IEnumerable<ConnectionProfile> profiles)
    {
        var result = new StringBuilder("# UNSC Relay - endpoint SSH, senza credenziali o chiavi private.\n# Configurazione separata: autenticazione e opzioni avanzate da configurare in OpenSSH.\n");
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            profile.Validate();
            if (profile.Protocol is not ("ssh" or "sftp")) throw new ArgumentException("Seleziona soltanto profili SSH/SFTP.");
            if (profile.StartupMacro?.Enabled==true || profile.Hooks.Any(h=>h.Enabled) || profile.JumpHost.Length > 0 || profile.Tunnels.Count > 0 || profile.RequirePostQuantumKex || profile.Subsystem.Length > 0 || profile.TmuxSession.Length > 0)
                throw new ArgumentException("Il profilo contiene opzioni avanzate non esportabili: " + profile.Name);
            if (!Regex.IsMatch(profile.Host, @"^[a-zA-Z0-9.:_-]+$", RegexOptions.CultureInvariant) || !Regex.IsMatch(profile.Username, @"^[a-zA-Z0-9.@_\\-]+$", RegexOptions.CultureInvariant))
                throw new ArgumentException("Host o username non rappresentabile nell'export: " + profile.Name);
            var alias = Regex.Replace(profile.Name, @"[^a-zA-Z0-9._-]", "-"); if (alias.Length == 0) alias = "relay";
            var original = alias; int index = 2; while (!aliases.Add(alias)) alias = original + "-" + index++;
            result.Append("\nHost ").Append(alias).Append("\n    HostName ").Append(profile.Host).Append("\n    User ").Append(profile.Username)
                .Append("\n    Port ").Append(profile.Port).Append("\n    ConnectTimeout ").Append(profile.ConnectTimeoutSeconds).Append("\n    ServerAliveInterval ").Append(profile.KeepAliveSeconds).Append('\n');
        }
        return result.ToString();
    }
}
