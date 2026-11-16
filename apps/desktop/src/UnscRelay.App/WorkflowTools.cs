using System.IO;
using System.Text;
using System.Text.Json;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    SshConfigPreview? sshConfigPreview;
    OpenSshConfigEdit? sshConfigEdit;
    string sshConfigEditId="";
    string sshConfigSource = "";
    object? WorkflowAction(string action, JsonElement p)
    {
        if (action == "multiSend")
        {
            var ids = p.GetProperty("sessionIds").Deserialize<string[]>() ?? [];
            var text = S(p, "text"); bool enter = p.TryGetProperty("enter", out var e) && e.GetBoolean();
            TerminalTarget[] Available() => sessions.Values.Select(s => new TerminalTarget(s.Id, s.Profile.Name, s.Profile.Host, s.Profile.Port, s.Profile.Username, s.CanWrite, s.Profile.Protocol)).Concat(nativeSessionProfiles.Where(kv=>kv.Value.Native.Kind=="ssh"&&localSessions.TryGetValue(kv.Key,out var process)&&process.Running).Select(kv=>new TerminalTarget(kv.Key,kv.Value.Name,kv.Value.Native.Target,kv.Value.Port,kv.Value.Username,true,"ssh"))).ToArray();
            var targets = MultiSend.Validate(Available(), ids, text);
            var recipients = string.Join("\n", targets.Select(t => t.Name + " / " + t.Username + "@" + t.Host + ":" + t.Port));
            if (!Confirm("Inviare lo stesso testo a questi " + targets.Length + " terminali?\n\n" + recipients + "\n\n" + (enter ? "Invio finale ATTIVO: i comandi possono essere eseguiti." : "Senza Invio finale; eventuali righe interne possono comunque eseguire comandi.") + "\n\nAnteprima (" + text.Length + " caratteri):\n" + (text.Length > 1200 ? text[..1200] + "\n[anteprima abbreviata]" : text))) throw new OperationCanceledException();
            MultiSend.Validate(Available(), ids, text);
            var report = new List<object>();
            foreach (var target in targets)
            {
                try { if(localSessions.TryGetValue(target.Id,out var native)){if(!native.Running)throw new IOException("Processo terminato durante l’invio.");native.Write(text+(enter?"\r":""));}else{var session = sessions[target.Id]; if (!session.CanWrite) throw new IOException("Sessione disconnessa durante l'invio."); session.Write(text + (enter ? "\r" : ""));} report.Add(new { id = target.Id, sent = true, error = "" }); }
                catch (Exception ex) { report.Add(new { id = target.Id, sent = false, error = ex.Message }); }
            }
            return report; // No automatic retry: it could execute the same command twice.
        }
        if (action == "sshConfigPreview")
        {
            sshConfigPreview = null; sshConfigSource = "";
            var path = OpenFile("Seleziona il file OpenSSH config (sola lettura)");
            if (path == null) return null;
            if (new FileInfo(path).Length > 1048576) throw new IOException("Configurazione troppo grande (massimo 1 MiB).");
            var parsed = OpenSshConfig.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            sshConfigPreview = parsed; sshConfigSource = Path.GetFullPath(path); return parsed;
        }
        if (action == "sshConfigImport")
        {
            var preview = sshConfigPreview ?? throw new InvalidOperationException("Seleziona prima il file da importare.");
            var aliases = p.GetProperty("aliases").Deserialize<string[]>() ?? [];
            if (aliases.Length == 0 || aliases.Distinct().Count() != aliases.Length) throw new ArgumentException("Seleziona almeno un alias distinto.");
            var profiles = OpenSshConfig.SelectForImport(preview, aliases);
            var clone = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, ProfileStore.Json), ProfileStore.Json)!;
            clone.Profiles.AddRange(profiles); FolderCatalog.Add(clone, "OpenSSH"); store.Save(clone); data = clone; sshConfigPreview = null; return Snapshot();
        }
        if (action == "sshConfigExport")
        {
            var ids = p.GetProperty("profileIds").Deserialize<string[]>() ?? [];
            if (ids.Length == 0 || ids.Distinct().Count() != ids.Length) throw new ArgumentException("Seleziona i profili da esportare.");
            var text = OpenSshConfig.Export(ids.Select(id => data.Profiles.Single(profile => profile.Id == id)));
            var path = SaveFile("Esporta endpoint OpenSSH in un file separato", "relay-ssh-config"); if (path == null) return null;
            var full = Path.GetFullPath(path);
            if (full.Equals(sshConfigSource, StringComparison.OrdinalIgnoreCase) || full.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config"), StringComparison.OrdinalIgnoreCase)) throw new IOException("Scegli un file separato: la configurazione OpenSSH originale deve restare invariata.");
            AtomicFile.Write(full, Encoding.UTF8.GetBytes(text)); return null;
        }
        if(action=="sshConfigWritePreview")
        {
            sshConfigEdit=null;sshConfigEditId="";
            var ids=p.GetProperty("profileIds").Deserialize<string[]>()??[];
            if(ids.Length==0||ids.Distinct().Count()!=ids.Length)throw new ArgumentException("Seleziona i profili da scrivere.");
            var profiles=ids.Select(id=>data.Profiles.Single(x=>x.Id==id)).ToArray();
            var path=SaveFile("Scegli il config OpenSSH da aggiornare (anteprima prima della scrittura)","config");if(path==null)return null;
            if(Path.GetFullPath(path).StartsWith(Path.GetFullPath(dataPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Scegli un config fuori dalla cartella dati dell'app.");
            sshConfigEdit=new OpenSshConfigEdit(path,profiles);sshConfigEditId=Guid.NewGuid().ToString("N");
            return new{id=sshConfigEditId,path=sshConfigEdit.PathName,before=sshConfigEdit.Before,after=sshConfigEdit.After,expires=sshConfigEdit.Expires};
        }
        if(action=="sshConfigWriteApply")
        {
            var plan=sshConfigEdit??throw new IOException("Genera prima l'anteprima.");if(S(p,"id")!=sshConfigEditId)throw new IOException("Anteprima non piÃƒÂ¹ disponibile.");
            if(!Confirm("Aggiornare il config OpenSSH?\n"+plan.PathName+"\n\nViene sostituita soltanto la sezione iniziale gestita da Relay. Il resto ÃƒÂ¨ conservato byte per byte; un file esistente avrÃƒÂ  una copia .relay-backup. Le opzioni personali generali continuano ad applicarsi agli endpoint. Nessuna credenziale ÃƒÂ¨ esportata."))throw new OperationCanceledException();
            var backup=plan.Apply();sshConfigEdit=null;sshConfigEditId="";return new{saved=true,backup};
        }
        throw new ArgumentException("Comando workflow non valido.");
    }
}
