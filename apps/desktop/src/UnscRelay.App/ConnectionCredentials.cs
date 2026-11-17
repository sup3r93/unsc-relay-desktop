using System.IO;
using System.Text.Json;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    readonly Dictionary<string, string> connectionPassphrases = [];
    object DeleteConnectionProfile(string id)
    {
        var profile = data.Profiles.Single(p => p.Id == id);
        if (profile.PasswordId.Length > 0 && !vault.IsUnlocked) throw new InvalidOperationException("Sblocca l'archivio prima di eliminare un profilo con password salvata.");
        if (!Confirm("Eliminare il profilo Â«" + profile.Name + "Â»" + (profile.PasswordId.Length > 0 ? " e la relativa password salvata" : "") + "? Le chiavi SSH non vengono eliminate.")) throw new OperationCanceledException();
        var prior = data.Profiles; data.Profiles = data.Profiles.Where(p => p.Id != id).ToList();
        try { store.Save(data); } catch { data.Profiles = prior; throw; }
        if (profile.PasswordId.Length > 0 && vault.Passwords().Any(p => p.Id == profile.PasswordId))
        {
            try { vault.Delete(profile.PasswordId); } catch (IOException) { Emit("notice", new { text = "Profilo eliminato. La password cifrata non Ã¨ stata rimossa dall'archivio a causa di un errore di scrittura." }); }
        }
        connectionPasswords.Remove(id); connectionPassphrases.Remove(id); return Snapshot();
    }
    object SaveConnectionProfile(JsonElement payload)
    {
        var profile = payload.Deserialize<ConnectionProfile>(ProfileStore.Json) ?? throw new ArgumentException("Profilo non valido."); profile.Validate();
        var old = data.Profiles.SingleOrDefault(x => x.Id == profile.Id);
        if(profile.Protocol=="native"&&!profile.Storage.Browser&&(old==null||System.Text.Json.JsonSerializer.Serialize(old.Native)!=System.Text.Json.JsonSerializer.Serialize(profile.Native))&&!Confirm("Salvare questa sessione con un client esterno? Il client e le opzioni del file config possono eseguire comandi locali. Nessuna password deve essere inserita negli argomenti.\n\n"+profile.Native.Kind+" / "+profile.Native.Target))throw new OperationCanceledException();
        ConfirmHookChanges(profile,old);ConfirmLocalChanges(profile,old);ConfirmStartupMacroChanges(profile,old);
        var password = S(payload, "password"); var phrase = S(payload, "keyPassphrase");
        if (password.Length > 8192 || phrase.Length > 8192) throw new ArgumentException("Credenziale troppo lunga.");
        if(profile.Storage.Browser&&password.Any(char.IsControl))throw new ArgumentException("La credenziale storage non puo contenere caratteri di controllo.");
        var remember = payload.TryGetProperty("rememberPassword", out var flag) ? flag.GetBoolean() : !string.IsNullOrEmpty(old?.PasswordId);
        profile.PasswordId = remember ? old?.PasswordId ?? "" : "";
        if (remember && profile.PasswordId.Length == 0 && password.Length == 0) throw new ArgumentException("Inserisci la password da salvare nell'archivio.");
        if (!remember && !string.IsNullOrEmpty(old?.PasswordId) && !vault.IsUnlocked) throw new InvalidOperationException("Sblocca l'archivio prima di rimuovere la password salvata.");
        if (!remember && !string.IsNullOrEmpty(old?.PasswordId) && !Confirm("Rimuovere dall'archivio la password salvata per Â«" + old.Name + "Â»?")) throw new OperationCanceledException();
        var candidate = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, ProfileStore.Json), ProfileStore.Json)!;
        profile.Folder = FolderCatalog.Add(candidate, profile.Folder);
        if (payload.TryGetProperty("addToAddressBook", out var add) && add.GetBoolean())
        {
            var machine = new MachineAddress(Guid.NewGuid().ToString("N"), S(payload, "machineName").Trim(), profile.Host, profile.Port); machine.Validate();
            candidate.Machines.RemoveAll(m => m.Host.Equals(machine.Host, StringComparison.OrdinalIgnoreCase) && m.Port == machine.Port);
            candidate.Machines.Add(machine);
        }
        var index = candidate.Profiles.FindIndex(p => p.Id == profile.Id);
        if (index >= 0) { if(profile.Protocol!="local")profile.Tunnels = candidate.Profiles[index].Tunnels; candidate.Profiles[index] = profile; } else candidate.Profiles.Add(profile);
        if (profile.Protocol is "ssh" or "sftp") SshRoutes.Resolve(profile, candidate.Profiles);
        if (profile.Protocol is "rdp" or "vnc") DesktopRoutes.Validate(profile, candidate.Profiles);
        var newPasswordId = "";
        try
        {
            if (remember && password.Length > 0) profile.PasswordId = newPasswordId = vault.StorePassword(profile.Name, password);
            store.Save(candidate);
        }
        catch { if (newPasswordId.Length > 0) vault.Delete(newPasswordId); throw; }
        data = candidate;
        if (old?.PasswordId.Length > 0 && old.PasswordId != profile.PasswordId && vault.IsUnlocked && vault.Passwords().Any(k => k.Id == old.PasswordId))
        {
            try { vault.Delete(old.PasswordId); }
            catch (IOException) { Emit("notice", new { text = "Connessione salvata. La precedente password Ã¨ rimasta nell'archivio perchÃ© non Ã¨ stato possibile rimuoverla." }); }
        }
        if (!remember && password.Length > 0) connectionPasswords[profile.Id] = password; else if (remember) connectionPasswords.Remove(profile.Id);
        if (old?.KeyId != profile.KeyId || profile.Authentication is not ("key" or "auto")) connectionPassphrases.Remove(profile.Id);
        if (profile.Authentication is "key" or "auto") connectionPassphrases[profile.Id] = phrase;
        return Snapshot();
    }
    string ConnectionPassword(ConnectionProfile profile, bool automatic = false)
    {
        if (connectionPasswords.TryGetValue(profile.Id, out var temporary)) return temporary;
        if (profile.PasswordId.Length > 0)
        {
            if (!vault.IsUnlocked) throw new InvalidOperationException("Sblocca l'archivio per usare la password salvata di Â«" + profile.Name + "Â».");
            if (!vault.Passwords().Any(k => k.Id == profile.PasswordId)) throw new InvalidOperationException("La password salvata non Ã¨ piÃ¹ disponibile. Modifica la connessione.");
            return vault.ReadPassword(profile.PasswordId);
        }
        if (automatic) throw new InvalidOperationException("Password richiesta: riconnetti manualmente.");
        if(profile.Storage.Browser && profile.Native.Kind=="s3")return Secret("Secret access key S3 / "+profile.Name,"Segreto per l'access key "+profile.Username+" del bucket "+profile.Storage.Bucket+". Non viene salvato automaticamente.");
        return Secret("Password / " + profile.Name, "Password per " + profile.Username + "@" + profile.Host + ". Non verrÃ  salvata automaticamente.");
    }
}
