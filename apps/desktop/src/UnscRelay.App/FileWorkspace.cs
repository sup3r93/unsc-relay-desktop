using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    sealed class FileJob
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; set; } = "";
        public string ProfileId { get; init; } = "";
        public string Host { get; init; } = "";
        public int Port { get; init; }
        public string Username { get; init; } = "";
        public TransferRequest Request { get; set; } = null!;
        public string Status { get; set; } = "In coda";
        public double Progress { get; set; }
        public string Error { get; set; } = "";
        public CancellationTokenSource? Cancellation { get; set; }
        public bool RecoveryPaused { get; set; }
        public Task? Running { get; set; }
    }
    readonly Dictionary<string, FileJob> fileJobs = [];
    SemaphoreSlim transferSlots = new(2, 2);
    int transferParallelism = 2;
    bool QueueBusy => recoveryTasks.Count>0 || fileJobs.Values.Any(j => j.Status is "In coda" or "Trasferimento" or "Pausa richiesta");
    object QueueSnapshot() => fileJobs.Values.Select(j => new { j.Id, j.SessionId, j.Host, j.Port, name = Path.GetFileName(j.Request.Local), j.Request.Local, j.Request.Remote, direction = j.Request.Upload ? "upload" : "download", j.Status, j.Progress, j.Error }).ToArray();
    sealed record JobJournal(string Id, string ProfileId, string Host, int Port, string Username, TransferRequest Request, string Status, double Progress);
    void LoadFileQueue()
    {
        var path = Path.Combine(dataPath, "transfers.json"); if (!File.Exists(path)) return;
        try
        {
            var journal = JsonSerializer.Deserialize<List<JobJournal>>(File.ReadAllText(path), ProfileStore.Json) ?? [];
            foreach (var j in journal.Take(5000))
                if (!string.IsNullOrWhiteSpace(j.Host) && j.Port is > 0 and <= 65535 && Guid.TryParse(j.Id, out _) && j.Id == j.Request.Id && data.Profiles.Any(p => p.Id == j.ProfileId))
                    fileJobs.TryAdd(j.Id, new() { Id = j.Id, ProfileId = j.ProfileId, Host = j.Host, Port = j.Port, Username = j.Username, Request = j.Request, Status = "In pausa", Progress = j.Progress });
        }
        catch (Exception ex) when (ex is IOException or JsonException) { /* An invalid journal never starts work or changes profiles. */ }
    }
    void RebindFileQueue(string profileId, string sessionId)
    {
        foreach (var j in fileJobs.Values.Where(j => j.ProfileId == profileId && j.Cancellation == null && !sessions.ContainsKey(j.SessionId))) j.SessionId = sessionId;
        EmitQueue();
    }
    void EmitQueue(bool persist = true)
    {
        try { if (persist) AtomicFile.Write(Path.Combine(dataPath, "transfers.json"), JsonSerializer.SerializeToUtf8Bytes(fileJobs.Values.Where(j => j.Status is not ("Completato" or "Saltato")).Select(j => new JobJournal(j.Id, j.ProfileId, j.Host, j.Port, j.Username, j.Request, j.Status, j.Progress)), ProfileStore.Json)); }
        catch (IOException) { Emit("notice", new { text = "Impossibile salvare la coda: i trasferimenti restano disponibili fino alla chiusura." }); }
        Emit("fileQueue", QueueSnapshot());
    }
    async Task RunJob(FileJob job, bool resume, bool automaticRecovery=false)
    {
        job.Cancellation = new(); var token = job.Cancellation.Token; job.RecoveryPaused=false; job.Status = "In coda"; job.Error = ""; EmitQueue();
        var acquired = false;
        try
        {
            await Task.Run(()=>TransferWindow.Wait(job.Request.WindowStartMinute,job.Request.WindowEndMinute,token),token);
            await transferSlots.WaitAsync(token); acquired = true;
            if (!sessions.TryGetValue(job.SessionId, out var session)) throw new IOException("Riapri la sessione SFTP prima di trasferire.");
            CheckJobEndpoint(job, session);if(automaticRecovery)ValidateRecovery(session,true);
            job.Status = "Trasferimento"; EmitQueue(); var last = DateTime.MinValue;
            var transferred = await session.TransferAdvancedAsync(job.Request with { Resume = resume, AutomaticRecovery=automaticRecovery }, (done, total) =>
            {
                if (DateTime.UtcNow - last < TimeSpan.FromMilliseconds(150)) return; last = DateTime.UtcNow;
                Dispatcher.BeginInvoke(() => { job.Progress = total > 0 ? Math.Min(100, done * 100d / total) : 0; EmitQueue(false); });
            }, token, checkpoint => Dispatcher.Invoke(() => { if(job.Request.Checkpoint==null){job.Request=job.Request with{Checkpoint=checkpoint};EmitQueue();} }));
            job.Status = transferred ? "Completato" : "Saltato"; job.Progress = 100; job.RecoveryPaused=false;
        }
        catch (OperationCanceledException) { job.Status = job.RecoveryPaused ? "Attesa connessione" : "In pausa"; }
        catch (Exception ex)
        {
            if(!token.IsCancellationRequested && NetworkReconnect.IsTransient(ex)){MarkRecoveryNeeded(job.SessionId,ex);job.RecoveryPaused=true;}
            job.Status = job.RecoveryPaused ? "Attesa connessione" : token.IsCancellationRequested ? "In pausa" : "Errore"; job.Error = ex.Message;
        }
        finally { if (acquired) transferSlots.Release(); job.Cancellation.Dispose(); job.Cancellation = null; EmitQueue(); }
    }
    object EnqueueFiles(JsonElement payload)
    {
        var session = Session(payload); if(recoveryTasks.ContainsKey(session.Profile.Id))throw new IOException("Ripristino in corso: attendi prima di aggiungere trasferimenti."); if (!session.FilesAvailable) throw new IOException("SFTP non disponibile.");
        var items = payload.GetProperty("items").EnumerateArray().ToArray(); if (items.Length is < 1 or > 1000) throw new ArgumentException("Seleziona da 1 a 1000 file.");
        if (fileJobs.Count > 5000) throw new IOException("Svuota i trasferimenti completati prima di aggiungerne altri.");
        var requests = items.Select(item =>
        {
            var local = Path.GetFullPath(S(item, "local")); var remote = S(item, "remote");
            if (remote.Length == 0 || remote.Any(char.IsControl)) throw new ArgumentException("Percorso remoto non valido.");
            var upload = S(payload, "direction") == "upload";
            var isDirectory = item.TryGetProperty("directory", out var dir) && dir.GetBoolean();
            var parents = item.TryGetProperty("createParents", out var cp) && cp.GetBoolean();
            if (upload && !(isDirectory ? Directory.Exists(local) : File.Exists(local))) throw new FileNotFoundException("La sorgente locale non esiste.");
            if (!upload && !parents && !Directory.Exists(Path.GetDirectoryName(local))) throw new DirectoryNotFoundException("Seleziona una cartella locale esistente.");
            var conflict = S(item, "conflict", S(payload, "conflict", "ask")); if (conflict is not ("ask" or "replace" or "skip" or "new" or "resume")) throw new ArgumentException("Regola conflitto non valida.");
            var mode = S(payload, "permissions"); short? permissions = mode.Length == 0 ? null : System.Text.RegularExpressions.Regex.IsMatch(mode, "^[0-7]{3,4}$") ? Convert.ToInt16(mode, 8) : throw new ArgumentException("Permessi non validi.");
            int? TimeOption(string name){if(!payload.TryGetProperty(name,out var v)||v.ValueKind==JsonValueKind.Null||v.ValueKind==JsonValueKind.String&&v.GetString()=="")return null;return v.GetInt32();}
            var windowStart=TimeOption("windowStartMinute");var windowEnd=TimeOption("windowEndMinute");TransferWindow.Validate(windowStart,windowEnd);
            var bandwidth = I(payload, "bandwidthKiB", 0); if (bandwidth is < 0 or > 1048576) throw new ArgumentException("Limite banda non valido.");
            var id = Guid.NewGuid().ToString("N"); return new FileJob { Id = id, SessionId = session.Id, ProfileId = session.Profile.Id, Host = session.Profile.Host, Port = session.Profile.Port, Username = session.Profile.Username, Request = new(id, local, remote, upload, conflict, Permissions: permissions, CreateParents: parents, Directory: isDirectory, BandwidthKiB: bandwidth, WindowStartMinute:windowStart,WindowEndMinute:windowEnd) };
        }).ToArray();
        if (requests.GroupBy(j => j.Request.Upload ? j.Request.Remote : j.Request.Local, requests[0].Request.Upload ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new IOException("Destinazione duplicata nella selezione.");
        foreach (var job in requests)
            if (fileJobs.Values.Any(j => j.Status is not ("Completato" or "Saltato") && (job.Request.Upload && j.Request.Upload && j.SessionId == job.SessionId && j.Request.Remote == job.Request.Remote || !job.Request.Upload && !j.Request.Upload && j.Request.Local.Equals(job.Request.Local, StringComparison.OrdinalIgnoreCase)))) throw new IOException("La destinazione è già presente nella coda.");
        foreach (var job in requests) { fileJobs.Add(job.Id, job); job.Running = RunJob(job, false); }
        return QueueSnapshot();
    }
    static void CheckJobEndpoint(FileJob job, SshSession session)
    {
        if (!job.Host.Equals(session.Profile.Host, StringComparison.OrdinalIgnoreCase) || job.Port != session.Profile.Port || job.Username != session.Profile.Username)
            throw new IOException("Il profilo ora indica una macchina o un utente diverso dalla coda originale. Ripristina host, porta e utente precedenti oppure crea un nuovo trasferimento.");
    }
    async Task<object> PlanFiles(JsonElement payload)
    {
        var session = Session(payload); var upload = S(payload, "direction") == "upload";
        var localBase = Path.GetFullPath(S(payload, "localDirectory")); var remoteBase = S(payload, "remoteDirectory");
        var planned = new List<object>(); var skipped = 0;
        string LocalChild(string parent, string name)
        {
            if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\') || name.EndsWith('.') || name.EndsWith(' ')) throw new IOException("Nome remoto non compatibile con Windows: " + name);
            return Path.Combine(parent, name);
        }
        async Task Walk(string local, string remote, bool directory, bool link, int depth)
        {
            if (link) { skipped++; return; }
            if (depth > 32 || planned.Count >= 1000) throw new IOException("Trasferisci al massimo 1000 elementi e 32 livelli per operazione.");
            var remoteStamp = await session.StatAsync(remote);
            var localInfo = new FileInfo(local);
            FileStamp? localStamp = File.Exists(local) ? new(local, false, localInfo.Length, localInfo.LastWriteTimeUtc) : System.IO.Directory.Exists(local) ? new(local, true, 0, System.IO.Directory.GetLastWriteTimeUtc(local)) : null;
            planned.Add(new { local, remote, directory, createParents = true, source = upload ? localStamp : remoteStamp, target = upload ? remoteStamp : localStamp });
            if (!directory) return;
            if (upload)
            {
                foreach (var item in new DirectoryInfo(local).EnumerateFileSystemInfos())
                {
                    if (item.Name.Contains('\\') || item.Name.Contains('/')) throw new IOException("Nome non valido.");
                    await Walk(item.FullName, remote.TrimEnd('/') + "/" + item.Name, item.Attributes.HasFlag(FileAttributes.Directory), item.Attributes.HasFlag(FileAttributes.ReparsePoint), depth + 1);
                }
            }
            else
            {
                var entries = JsonSerializer.SerializeToElement(await session.ListAsync(remote), ProfileStore.Json);
                foreach (var item in entries.EnumerateArray())
                {
                    var name = S(item, "name"); if (name is "." or "..") continue;
                    await Walk(LocalChild(local, name), S(item, "path"), item.GetProperty("directory").GetBoolean(), item.TryGetProperty("link", out var symbolic) && symbolic.GetBoolean(), depth + 1);
                }
            }
        }
        foreach (var item in payload.GetProperty("paths").EnumerateArray())
        {
            var path = item.GetString() ?? throw new IOException("Percorso mancante.");
            if (upload)
            {
                var full = Path.GetFullPath(path); var attr = File.GetAttributes(full);
                await Walk(full, remoteBase.TrimEnd('/') + "/" + Path.GetFileName(full), attr.HasFlag(FileAttributes.Directory), attr.HasFlag(FileAttributes.ReparsePoint), 0);
            }
            else
            {
                var parent = path[..path.LastIndexOf('/')];
                var entries = JsonSerializer.SerializeToElement(await session.ListAsync(parent.Length == 0 ? "/" : parent), ProfileStore.Json);
                var file = entries.EnumerateArray().Single(f => S(f, "path") == path);
                await Walk(LocalChild(localBase, S(file, "name")), path, file.GetProperty("directory").GetBoolean(), file.TryGetProperty("link", out var symbolic) && symbolic.GetBoolean(), 0);
            }
        }
        return new { items = planned, skippedLinks = skipped };
    }
    object LocalFiles(string path)
    {
        path = Path.GetFullPath(path.Length > 0 ? path : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        var dir = new DirectoryInfo(path);
        return new { path = dir.FullName, parent = dir.Parent?.FullName, home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), entries = dir.EnumerateFileSystemInfos().Take(10000).Select(f => new { name = f.Name, path = f.FullName, directory = f.Attributes.HasFlag(FileAttributes.Directory), link = f.Attributes.HasFlag(FileAttributes.ReparsePoint), size = f is FileInfo file ? file.Length : 0, modified = f.LastWriteTimeUtc, attributes = FilePresentation.WindowsAttributes(f.Attributes) }).OrderByDescending(f => f.directory).ThenBy(f => f.name).ToArray() };
    }
    sealed class EditBuffer
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; init; } = "";
        public RemoteText Original { get; set; } = null!;
        public string Local { get; set; } = "";
        public FileSystemWatcher? Watcher { get; set; }
        public bool Busy { get; set; }
        public bool AutoUpload { get; set; }
        public bool Dirty { get; set; }
        public bool AutoScheduled { get; set; }
        public int ExternalRevision { get; set; }
    }
    readonly Dictionary<string, EditBuffer> editBuffers = [];
    bool EditorsBusy => editBuffers.Values.Any(e => e.Busy);
    async Task<object> OpenRemoteEditor(JsonElement payload)
    {
        var session = Session(payload); var remote = await session.ReadTextAsync(S(payload, "path"));
        var existing = editBuffers.Values.FirstOrDefault(e => e.SessionId == session.Id && e.Original.Path == remote.Path);
        if (existing != null) return new { existing.Id, existing.SessionId, existing.Original.Path, existing.Original.Text, existing.Original.Hash };
        var edit = new EditBuffer { SessionId = session.Id, Original = remote }; editBuffers.Add(edit.Id, edit);
        return new { edit.Id, edit.SessionId, remote.Path, remote.Text, remote.Hash };
    }
    async Task SaveRemoteEditor(EditBuffer edit, string text, bool external)
    {
        if(sessions.TryGetValue(edit.SessionId,out var editorSession)&&recoveryTasks.ContainsKey(editorSession.Profile.Id))throw new IOException("Ripristino SFTP in corso. Le modifiche dell’editor sono conservate.");
        if (edit.Busy) throw new IOException("Salvataggio già in corso."); edit.Busy = true;
        var scratch = "";
        try
        {
            var bytes = external ? await File.ReadAllBytesAsync(edit.Local) : new UTF8Encoding(edit.Original.Bom, true).GetPreamble().Concat(new UTF8Encoding(false, true).GetBytes(text)).ToArray();
            if (bytes.Length > 2 * 1024 * 1024 || bytes.Contains((byte)0)) throw new IOException("L’editor accetta testo UTF-8 fino a 2 MiB.");
            var bom = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }); var content = new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var dir = Path.Combine(dataPath, "Editor"); Directory.CreateDirectory(dir); scratch = Path.Combine(dir, edit.Id + ".upload"); await File.WriteAllBytesAsync(scratch, bytes);
            if (!sessions.TryGetValue(edit.SessionId, out var session)) throw new IOException("La sessione SFTP è stata chiusa.");
            var transfer = new TransferRequest(Guid.NewGuid().ToString("N"), scratch, edit.Original.Path, true, "replace", ExpectedHash: edit.Original.Hash);
            try { await session.TransferAdvancedAsync(transfer, (_, _) => { }, CancellationToken.None); }
            catch { try { await session.DiscardPartialAsync(transfer); } catch { } throw; }
            edit.Original = new(edit.Original.Path, content, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), bom);
            Emit("editorSaved", new { edit.Id, edit.Original.Text, edit.Original.Hash, external });
        }
        finally { if (File.Exists(scratch)) File.Delete(scratch); edit.Busy = false; }
    }
    void ExternalEditor(EditBuffer edit, bool choose)
    {
        var exe = choose ? OpenFile("Seleziona l’EXE dell’editor (es. Notepad++ o Code)") : data.Settings.ExternalEditor.Length>0?data.Settings.ExternalEditor:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
        if (exe == null) return; if (!File.Exists(exe))throw new IOException("Editor non trovato. Sceglilo di nuovo in Impostazioni → Editor.");if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Seleziona un programma EXE.");
        PrepareExternalEditor(edit);
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, ArgumentList = { edit.Local } });
    }
    void PrepareExternalEditor(EditBuffer edit)
    {
        if (edit.Local.Length == 0)
        {
            var dir = Path.Combine(dataPath, "Editor", edit.Id); Directory.CreateDirectory(dir);
            var name = Path.GetFileName(edit.Original.Path); if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) name = "remote.txt";
            edit.Local = Path.Combine(dir, name); File.WriteAllText(edit.Local, edit.Original.Text, new UTF8Encoding(edit.Original.Bom));
            edit.Watcher = new FileSystemWatcher(dir) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName, EnableRaisingEvents = true };
            void Changed() => Dispatcher.BeginInvoke(async () =>
            {
                edit.ExternalRevision++; Emit("editorExternalChanged", new { edit.Id });
                if (!edit.AutoUpload || edit.AutoScheduled) return;
                edit.AutoScheduled = true;
                try
                {
                    while (edit.AutoUpload && editBuffers.ContainsKey(edit.Id))
                    {
                        var revision = edit.ExternalRevision; await Task.Delay(700);
                        if (!edit.AutoUpload || !editBuffers.ContainsKey(edit.Id)) return;
                        if (revision != edit.ExternalRevision || edit.Busy) continue;
                        try { await SaveRemoteEditor(edit, "", true); }
                        catch (Exception ex) { edit.AutoUpload = false; Emit("editorConflict", new { edit.Id, error = ex.Message }); return; }
                        if (revision == edit.ExternalRevision) return;
                    }
                }
                finally { edit.AutoScheduled = false; }
            });
            edit.Watcher.Changed += (_, _) => Changed(); edit.Watcher.Created += (_, _) => Changed(); edit.Watcher.Renamed += (_, _) => Changed();
        }
    }
    void CloseEditor(string id)
    {
        if (!editBuffers.TryGetValue(id, out var edit)) return; if (edit.Busy) throw new IOException("Attendi il salvataggio dell’editor.");
        editBuffers.Remove(id); edit.Watcher?.Dispose();
        // Keep external scratch copies: a still-open editor must not lose its unsaved buffer.
    }
    async Task<object?> FileAction(string action, JsonElement p)
    {
        switch (action)
        {
            case "folderCompare": return await CompareFolders(p);
            case "folderSync": return await EnqueueFolderPlan(p);
            case "folderCancel": if (folderScans.TryGetValue(S(p, "scanId"), out var scan)) scan.Cancel(); return null;
            case "fileLocation":
                var locationSession = Session(p); if (!locationSession.Profile.RememberFileDirectories) return null;
                data.FileLocations[locationSession.Profile.Id] = new(locationSession.Profile.Host, locationSession.Profile.Port, locationSession.Profile.Username, S(p, "local"), S(p, "remote")); store.Save(data); return null;
            case "localList": return await Task.Run(() => LocalFiles(S(p, "path")));
            case "localChoose": var folder = new OpenFolderDialog { Title = "Scegli cartella locale" }; return folder.ShowDialog(this) == true ? await Task.Run(() => LocalFiles(folder.FolderName)) : null;
            case "sftpDirectory": return await Session(p).CanonicalDirectoryAsync(S(p, "path", "."));
            case "sftpHome": return Session(p).HomeDirectory;
            case "fileCompare":
                var remoteText = await Session(p).ReadTextAsync(S(p, "remote"));
                var localText = await Task.Run(() =>
                {
                    var path = Path.GetFullPath(S(p, "local")); using var stream = File.OpenRead(path);
                    if (stream.Length > 2 * 1024 * 1024) throw new IOException("Confronto disponibile per file UTF-8 fino a 2 MiB ciascuno.");
                    using var bytes = new MemoryStream(); var buffer = new byte[65536]; int read; while ((read = stream.Read(buffer)) > 0) { if (bytes.Length + read > 2 * 1024 * 1024) throw new IOException("Il file supera il limite del confronto."); bytes.Write(buffer, 0, read); }
                    var content = bytes.ToArray(); if (content.Contains((byte)0)) throw new IOException("Il confronto testuale non supporta file binari.");
                    var bom = content.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf });
                    return new RemoteText(path, new UTF8Encoding(false, true).GetString(content, bom ? 3 : 0, content.Length - (bom ? 3 : 0)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), bom);
                });
                return new { local = localText, remote = remoteText };
            case "sftpOperation":
                if (QueueBusy || EditorsBusy) throw new IOException("Completa o metti in pausa i trasferimenti prima di modificare i file.");
                if (S(p, "operation") == "delete" && !Confirm("Eliminare «" + S(p, "path") + "» dal server? Le cartelle devono essere vuote.")) return null;
                await Session(p).FileOperationAsync(S(p, "operation"), S(p, "path"), S(p, "value")); return null;
            case "fileEnqueue": return EnqueueFiles(p);
            case "filePlan": return await PlanFiles(p);
            case "fileParallelism": if (QueueBusy) throw new IOException("Metti in pausa la coda prima di cambiare il parallelismo."); var limit = I(p, "value", 2); if (limit is < 1 or > 4) throw new ArgumentException("Parallelismo: da 1 a 4."); transferSlots.Dispose(); transferSlots = new(limit, limit); transferParallelism = limit; return limit;
            case "fileQueue": return QueueSnapshot();
            case "fileClear": foreach (var id in fileJobs.Values.Where(j => j.Status is "Completato" or "Saltato").Select(j => j.Id).ToArray()) fileJobs.Remove(id); EmitQueue(); return QueueSnapshot();
            case "fileDiscard": var discard = fileJobs[S(p, "id")]; if (discard.Cancellation != null) throw new IOException("Metti prima in pausa il trasferimento."); if (!Confirm("Rimuovere il trasferimento e il suo file parziale? I file originali non vengono eliminati.")) return QueueSnapshot(); if (!sessions.TryGetValue(discard.SessionId, out var owner)) throw new IOException("Riapri SFTP per rimuovere il file parziale."); CheckJobEndpoint(discard, owner); await owner.DiscardPartialAsync(discard.Request); fileJobs.Remove(discard.Id); EmitQueue(); return QueueSnapshot();
            case "filePause": var pause = fileJobs[S(p, "id")]; pause.RecoveryPaused=false; if (pause.Cancellation != null) { pause.Status = "Pausa richiesta"; pause.Cancellation.Cancel(); } else if(pause.Status=="Attesa connessione")pause.Status="In pausa"; EmitQueue(); return QueueSnapshot();
            case "fileResume": var retry = fileJobs[S(p, "id")]; if(recoveryTasks.Count>0)throw new IOException("Attendi il ripristino delle connessioni."); if (retry.Cancellation != null) throw new IOException("Attendi l’arresto del trasferimento."); if (retry.Status == "Completato") return QueueSnapshot(); retry.RecoveryPaused=false;retry.Running = RunJob(retry, true); return QueueSnapshot();
            case "editorOpen": return await OpenRemoteEditor(p);
            case "editorSave": await SaveRemoteEditor(editBuffers[S(p, "id")], S(p, "text"), false); return null;
            case "editorExternal": ExternalEditor(editBuffers[S(p, "id")], p.TryGetProperty("choose", out var choose) && choose.GetBoolean()); return null;
            case "editorPublish": await SaveRemoteEditor(editBuffers[S(p, "id")], "", true); return null;
            case "editorAuto": var edit = editBuffers[S(p, "id")]; edit.AutoUpload = p.GetProperty("enabled").GetBoolean(); return null;
            case "editorDirty": editBuffers[S(p, "id")].Dirty = p.GetProperty("dirty").GetBoolean(); return null;
            case "editorReload": var reload = editBuffers[S(p, "id")]; if (reload.Busy) throw new IOException("Attendi il salvataggio."); reload.Original = await Session(JsonSerializer.SerializeToElement(new { sessionId = reload.SessionId })).ReadTextAsync(reload.Original.Path); return new { reload.Id, reload.Original.Text, reload.Original.Hash };
            case "editorClose": CloseEditor(S(p, "id")); return null;
            case "clipboardWrite": Clipboard.SetText(S(p, "text")); return null;
            case "clipboardRead": return Clipboard.ContainsText() ? Clipboard.GetText() : "";
            case "machineSave":
                var machine=p.Deserialize<MachineAddress>(ProfileStore.Json)??throw new ArgumentException("Indirizzo non valido.");machine.Validate();
                var duplicate=data.Machines.FirstOrDefault(m=>m.Id!=machine.Id&&m.Host.Equals(machine.Host,StringComparison.OrdinalIgnoreCase)&&m.Port==machine.Port);
                if(duplicate!=null)throw new ArgumentException("Indirizzo e porta gia presenti: "+duplicate.Name);
                var priorMachines=data.Machines;data.Machines=priorMachines.Where(m=>m.Id!=machine.Id).Append(machine).ToList();try{store.Save(data);}catch{data.Machines=priorMachines;throw;}return Snapshot();
            case "machineDelete": data.Machines.RemoveAll(m => m.Id == S(p, "id")); store.Save(data); return Snapshot();
            default: throw new ArgumentException("Azione file non disponibile.");
        }
    }
}
