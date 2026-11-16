using System.IO;
using System.Text.Json;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    sealed record FolderPlan(SshSession Owner, string SessionId, string LocalRoot, string RemoteRoot, List<SyncDifference> Items, DateTime Created);
    readonly Dictionary<string, FolderPlan> folderPlans = [];
    readonly Dictionary<string, CancellationTokenSource> folderScans = [];
    async Task<object> CompareFolders(JsonElement payload)
    {
        var session = Session(payload); var scanId = S(payload, "scanId");
        if (!Guid.TryParse(scanId, out _) || folderScans.ContainsKey(scanId)) throw new IOException("Confronto non valido o già in corso.");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(5)); folderScans.Add(scanId, cancel); var token = cancel.Token;
        try
        {
            var localRoot = Path.GetFullPath(S(payload, "localDirectory"));
            var remoteRoot = await session.CanonicalDirectoryAsync(S(payload, "remoteDirectory"));
            int skipped = 0; var local = await Task.Run(() => FolderSync.ScanLocal(localRoot, out skipped), token);
            var remote = new List<SyncEntry>();
            async Task Walk(string path, string prefix, int depth)
            {
                token.ThrowIfCancellationRequested(); if (depth > 32) throw new IOException("Massimo 32 livelli di cartelle.");
                var entries = JsonSerializer.SerializeToElement(await session.ListAsync(path), ProfileStore.Json);
                foreach (var entry in entries.EnumerateArray())
                {
                    token.ThrowIfCancellationRequested(); var name = S(entry, "name"); if (name is "." or "..") continue;
                    if (remote.Count >= FolderSync.MaxEntries) throw new IOException("Confronta al massimo 1000 elementi per lato.");
                    var relative = prefix + name; FolderSync.LocalChild(localRoot, relative);
                    var link = entry.GetProperty("link").GetBoolean(); var directory = entry.GetProperty("directory").GetBoolean();
                    remote.Add(new(relative, directory, entry.GetProperty("size").GetInt64(), entry.GetProperty("modified").GetDateTime(), Link: link));
                    if (link) skipped++; else if (directory) await Walk(S(entry, "path"), relative + "/", depth + 1);
                }
            }
            await Walk(remoteRoot, "", 0);
            var bytes = local.Concat(remote).Where(e => !e.Directory && !e.Link).Sum(e => (decimal)e.Size);
            if (bytes > FolderSync.MaxHashBytes) throw new IOException("Confronto contenuti limitato a 512 MiB totali. Apri sottocartelle più piccole.");
            for (var i = 0; i < local.Count; i++) if (!local[i].Directory && !local[i].Link)
            { token.ThrowIfCancellationRequested(); var entry = local[i]; local[i] = entry with { Hash = await Task.Run(() => FolderSync.HashLocal(FolderSync.LocalChild(localRoot, entry.Relative)), token) }; }
            for (var i = 0; i < remote.Count; i++) if (!remote[i].Directory && !remote[i].Link)
            { token.ThrowIfCancellationRequested(); var entry = remote[i]; remote[i] = entry with { Hash = await session.RemoteHashAsync(remoteRoot.TrimEnd('/') + "/" + entry.Relative, token) }; }
            token.ThrowIfCancellationRequested(); var differences = FolderSync.Compare(local, remote);
            foreach (var old in folderPlans.Where(p => p.Value.Created < DateTime.UtcNow.AddMinutes(-10) || p.Value.SessionId == session.Id).Select(p => p.Key).ToArray()) folderPlans.Remove(old);
            if (folderPlans.Count >= 30) throw new IOException("Chiudi un confronto prima di aprirne un altro.");
            var id = Guid.NewGuid().ToString("N"); folderPlans.Add(id, new(session, session.Id, localRoot, remoteRoot, differences, DateTime.UtcNow));
            return new { id, localRoot, remoteRoot, items = differences, skippedLinks = skipped };
        }
        finally { folderScans.Remove(scanId); }
    }
    async Task<object> EnqueueFolderPlan(JsonElement payload, bool confirm = true)
    {
        if(recoveryTasks.Count>0)throw new IOException("Attendi il ripristino prima di avviare una sincronizzazione.");
        var id = S(payload, "id"); if (!folderPlans.TryGetValue(id, out var plan) || DateTime.UtcNow - plan.Created > TimeSpan.FromMinutes(10)) throw new IOException("Anteprima scaduta. Confronta nuovamente le cartelle.");
        var session = Session(payload); if (session.Id != plan.SessionId || !ReferenceEquals(session, plan.Owner)) throw new IOException("La sessione del confronto è cambiata.");
        var direction = S(payload, "direction"); if (direction is not ("upload" or "download")) throw new ArgumentException("Scegli una direzione.");
        var upload = direction == "upload"; var names = payload.GetProperty("paths").EnumerateArray().Select(p => p.GetString() ?? "").ToArray();
        if (names.Length is < 1 or > 1000 || names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new IOException("Selezione non valida.");
        var selected = names.Select(n => plan.Items.SingleOrDefault(i => i.Relative == n) ?? throw new IOException("Elemento non incluso nel confronto.")).ToArray();
        var jobs = new List<FileJob>();
        foreach (var item in selected.OrderBy(i => i.Relative.Count(c => c == '/')))
        {
            var source = upload ? item.Local : item.Remote; var target = upload ? item.Remote : item.Local;
            if (!item.Transferable || source == null || source.Link) throw new IOException("Elemento non trasferibile: " + item.Relative);
            var local = FolderSync.LocalChild(plan.LocalRoot, item.Relative); var remote = plan.RemoteRoot.TrimEnd('/') + "/" + item.Relative;
            FolderSync.CheckLocalParents(Path.GetDirectoryName(local)!);
            var remoteNow = await session.StatAsync(remote);
            var localExists = File.Exists(local) || Directory.Exists(local);
            var localNow = localExists ? new FileStamp(local, Directory.Exists(local), File.Exists(local) ? new FileInfo(local).Length : 0, File.GetLastWriteTimeUtc(local)) : null;
            bool Same(SyncEntry? before, FileStamp? now) => before == null ? now == null : now != null && before.Directory == now.Directory && (before.Directory || before.Size == now.Size && before.Modified == now.Modified);
            if (!Same(item.Local, localNow) || !Same(item.Remote, remoteNow)) throw new IOException("Una cartella o un file è cambiato. Ripeti il confronto: " + item.Relative);
            var jobId = Guid.NewGuid().ToString("N"); jobs.Add(new() { Id = jobId, SessionId = session.Id, ProfileId = session.Profile.Id, Host = session.Profile.Host, Port = session.Profile.Port, Username = session.Profile.Username,
                Request = new(jobId, local, remote, upload, target == null ? "new" : "replace", ExpectedHash: target?.Hash, CreateParents: true, Directory: source.Directory, ExpectedSourceHash: source.Hash, ExpectedMissingTarget: target == null) });
        }
        if (fileJobs.Count + jobs.Count > 5000) throw new IOException("Svuota la coda prima di sincronizzare.");
        foreach (var job in jobs) if (fileJobs.Values.Any(j => j.Status is not ("Completato" or "Saltato") && (upload ? j.Request.Upload && j.SessionId == job.SessionId && j.Request.Remote == job.Request.Remote : !j.Request.Upload && j.Request.Local.Equals(job.Request.Local, StringComparison.OrdinalIgnoreCase)))) throw new IOException("Una destinazione è già nella coda.");
        if (confirm && !Confirm($"Trasferire {jobs.Count} elementi {(upload ? "verso il server" : "verso il PC")}? I file diversi selezionati verranno sostituiti. Nessuna eliminazione automatica.")) return new { cancelled = true };
        folderPlans.Remove(id); foreach (var job in jobs) fileJobs.Add(job.Id, job);
        foreach (var job in jobs) job.Running = RunJob(job, false); EmitQueue(); return new { queued = jobs.Count };
    }
}
