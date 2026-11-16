using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace UnscRelay.Core;

public sealed record TransferRequest(string Id, string Local, string Remote, bool Upload, string Conflict = "ask", bool Resume = false, short? Permissions = null, string? ExpectedHash = null, bool CreateParents = false, bool Directory = false, int BandwidthKiB = 0, string? ExpectedSourceHash = null, bool ExpectedMissingTarget = false, long MaximumSourceBytes = long.MaxValue, TransferCheckpoint? Checkpoint = null, bool AutomaticRecovery = false, int? WindowStartMinute = null, int? WindowEndMinute = null);
public sealed record TransferFileVersion(long Size, DateTime Modified, string? Hash = null);
public sealed record TransferCheckpoint(TransferFileVersion Source, TransferFileVersion? Target)
{
    public void Validate(TransferCheckpoint current)
    {
        if (Source != current.Source) throw new IOException("La sorgente è cambiata: ripresa rifiutata. Crea un nuovo trasferimento.");
        if (Target != current.Target) throw new IOException("La destinazione è cambiata durante l’interruzione: controlla il conflitto prima di riprendere.");
    }
}
public sealed record RemoteText(string Path, string Text, string Hash, bool Bom);
public sealed record FileStamp(string Path, bool Directory, long Size, DateTime Modified);
public sealed partial class SshSession
{
    readonly SemaphoreSlim _channelConnect = new(1, 1);
    Func<bool,SftpClient> _fileChannel = null!;
    Action<HostKeyEventArgs,bool> _fileTrust = null!;
    readonly Dictionary<string, (long Size, DateTime Time)> _transferSources = [];
    public string HomeDirectory => _sftp.WorkingDirectory;
    async Task<SftpClient> FileChannel(CancellationToken token,bool automatic=false)
    {
        await _channelConnect.WaitAsync(token);
        SftpClient? channel = null;
        try
        {
            if (_disposed) throw new InvalidOperationException("Sessione chiusa.");
            channel = _fileChannel(automatic); channel.HostKeyReceived += (_, e) => _fileTrust(e,automatic);
            await channel.ConnectAsync(token); return channel;
        }
        catch { channel?.Dispose(); throw; }
        finally { _channelConnect.Release(); }
    }
    public Task<string> CanonicalDirectoryAsync(string path) => Task.Run(() =>
    {
        _files.Wait(); try { var full = _sftp.Get(path).FullName; if (!_sftp.GetAttributes(full).IsDirectory) throw new IOException("Il percorso non Ã¨ una cartella."); return full; } finally { _files.Release(); }
    });
    public async Task<FileStamp?> StatAsync(string path)
    {
        await _files.WaitAsync();
        try { return await Task.Run(() => { if (!_sftp.Exists(path)) return null; var a = _sftp.GetAttributes(path); return new FileStamp(path, a.IsDirectory, a.Size, a.LastWriteTimeUtc); }); }
        finally { _files.Release(); }
    }
    public async Task FileOperationAsync(string operation, string path, string value)
    {
        await _files.WaitAsync();
        try { await Task.Run(() =>
        {
            switch (operation)
            {
                case "mkdir": _sftp.CreateDirectory(path); break;
                case "rename": _sftp.RenameFile(path, value); break;
                case "delete": if (_sftp.GetAttributes(path).IsDirectory) _sftp.DeleteDirectory(path); else _sftp.DeleteFile(path); break;
                case "chmod": if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-7]{3,4}$")) throw new ArgumentException("Permessi ottali non validi."); _sftp.ChangePermissions(path, Convert.ToInt16(value, 8)); break;
                default: throw new ArgumentException("Operazione file non valida.");
            }
        }); } finally { _files.Release(); }
    }
    public async Task<RemoteText> ReadTextAsync(string path)
    {
        using var channel = await FileChannel(CancellationToken.None);
        return await Task.Run(() =>
        {
            var attributes = channel.GetAttributes(path); if (!attributes.IsRegularFile || attributes.Size > 2 * 1024 * 1024) throw new IOException("Lâ€™editor accetta file di testo UTF-8 fino a 2 MiB.");
            using var stream = channel.OpenRead(path); using var bytes = new MemoryStream(); var buffer = new byte[65536]; int read;
            while ((read = stream.Read(buffer)) > 0) { if (bytes.Length + read > 2 * 1024 * 1024) throw new IOException("File troppo grande per lâ€™editor."); bytes.Write(buffer, 0, read); }
            var content = bytes.ToArray(); if (content.Contains((byte)0)) throw new IOException("Il file contiene dati binari.");
            var bom = content.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf });
            var text = new UTF8Encoding(false, true).GetString(content, bom ? 3 : 0, content.Length - (bom ? 3 : 0));
            return new RemoteText(path, text, Convert.ToHexString(SHA256.HashData(content)), bom);
        });
    }
    public async Task<string> RemoteHashAsync(string path, CancellationToken token = default,long maximumBytes=long.MaxValue)
    {
        using var channel = await FileChannel(token);
        using var stream = channel.OpenRead(path);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];long total=0;int read;
        while((read=await stream.ReadAsync(buffer,token))>0){total+=read;if(total>maximumBytes)throw new IOException("File remoto oltre il limite previsto.");hash.AppendData(buffer,0,read);}return Convert.ToHexString(hash.GetHashAndReset());
    }
    public async Task<bool> TransferAdvancedAsync(TransferRequest request, Action<ulong, long> progress, CancellationToken cancellation, Action<TransferCheckpoint>? checkpointObserved = null)
    {
        if (!Guid.TryParse(request.Id, out _)) throw new ArgumentException("Identificatore trasferimento non valido.");
        TransferWindow.Validate(request.WindowStartMinute,request.WindowEndMinute);
        await Task.Run(()=>TransferWindow.Wait(request.WindowStartMinute,request.WindowEndMinute,cancellation),cancellation);
        if (request.BandwidthKiB is < 0 or > 1048576) throw new ArgumentException("Limite banda non valido.");
        if (request.Conflict is not ("ask" or "replace" or "skip" or "new" or "resume")) throw new ArgumentException("Regola conflitto non valida.");
        using var channel = await FileChannel(cancellation,request.AutomaticRecovery);
        return await Task.Run(() =>
        {
            var local = Path.GetFullPath(request.Local); var remote = request.Remote;
            if (request.ExpectedSourceHash != null || request.ExpectedMissingTarget)
            {
                FolderSync.CheckLocalParents(request.Directory ? local : Path.GetDirectoryName(local)!);
                if (!request.Directory && request.Upload && File.GetAttributes(local).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Sorgente collegata non ammessa.");
                if (!request.Directory && !request.Upload && channel.Get(remote).IsSymbolicLink) throw new IOException("Sorgente collegata non ammessa.");
            }
            if (request.CreateParents || request.Directory)
            {
                if (request.Upload) EnsureRemoteDirectory(channel, request.Directory ? remote : remote[..remote.LastIndexOf('/')]);
                else System.IO.Directory.CreateDirectory(request.Directory ? local : Path.GetDirectoryName(local)!);
                if (request.Directory) { progress(0, 0); return true; }
            }
            var exists = request.Upload ? channel.Exists(remote) : File.Exists(local);
            if (exists && request.Conflict == "skip") return false;
            if (exists && request.Conflict is "ask" or "new") throw new IOException("La destinazione esiste giÃ . Scegli Sostituisci oppure un altro nome.");
            if (request.Upload && channel.GetAttributes(Path.GetDirectoryName(remote.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? ".").IsDirectory == false) throw new IOException("Cartella remota non valida.");
            var partial = request.Upload ? remote + ".relay-" + request.Id + ".partial" : local + ".relay-" + request.Id + ".partial";
            var size = request.Upload ? new FileInfo(local).Length : channel.GetAttributes(remote).Size;
            if(size>request.MaximumSourceBytes||request.MaximumSourceBytes<0)throw new IOException("La sorgente supera il limite di questa operazione.");
            var time = request.Upload ? File.GetLastWriteTimeUtc(local) : channel.GetAttributes(remote).LastWriteTimeUtc;
            using Stream source = request.Upload ? new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read) : channel.OpenRead(remote);
            var verifyContent=checkpointObserved!=null||request.Checkpoint?.Source.Hash!=null;
            string? sourceHash=null;if(verifyContent){sourceHash=CheckpointHash(source,cancellation);source.Position=0;}
            TransferFileVersion? targetVersion=null;
            if(exists){if(request.Upload){var target=channel.GetAttributes(remote);using var targetStream=channel.OpenRead(remote);targetVersion=new(target.Size,target.LastWriteTimeUtc,verifyContent?CheckpointHash(targetStream,cancellation):null);}else{var target=new FileInfo(local);using var targetStream=File.OpenRead(local);targetVersion=new(target.Length,target.LastWriteTimeUtc,verifyContent?CheckpointHash(targetStream,cancellation):null);}}
            var checkpoint=new TransferCheckpoint(new(size,time,sourceHash),targetVersion);
            if(request.Resume)request.Checkpoint?.Validate(checkpoint);
            checkpointObserved?.Invoke(request.Checkpoint ?? checkpoint);
            lock (_transferSources)
            {
                if (request.Resume && _transferSources.TryGetValue(request.Id, out var original) && original != (size, time)) throw new IOException("La sorgente Ã¨ cambiata: ripresa rifiutata. Avvia un nuovo trasferimento.");
                _transferSources[request.Id] = (size, time);
            }
            var partialExists = request.Upload ? channel.Exists(partial) : File.Exists(partial);
            long offset = 0;
            if (request.Resume && partialExists)
            {
                using Stream prefix = request.Upload ? channel.OpenRead(partial) : new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.Read);
                offset = prefix.Length;
                if (offset > size) throw new IOException("File parziale piÃ¹ grande della sorgente: ripresa rifiutata.");
                VerifyPrefix(source, prefix, offset, cancellation);
            }
            else if (exists && request.Conflict == "resume")
            {
                using Stream prefix = request.Upload ? channel.OpenRead(remote) : new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read);
                offset = prefix.Length;
                if (offset > size) throw new IOException("La destinazione Ã¨ piÃ¹ grande della sorgente: ripresa rifiutata.");
                VerifyPrefix(source, prefix, offset, cancellation); prefix.Position = 0;
                using Stream stage = request.Upload ? channel.Open(partial, FileMode.Create, FileAccess.Write) : new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
                var copyBuffer = new byte[65536]; long copied = 0;
                while (copied < offset) { cancellation.ThrowIfCancellationRequested(); var n = prefix.Read(copyBuffer, 0, (int)Math.Min(copyBuffer.Length, offset - copied)); if (n == 0) throw new IOException("La destinazione Ã¨ cambiata durante la verifica."); stage.Write(copyBuffer, 0, n); copied += n; }
                stage.Flush();
            }
            using (Stream destination = request.Upload ? channel.Open(partial, offset > 0 ? FileMode.Open : FileMode.Create, FileAccess.Write) : new FileStream(partial, offset > 0 ? FileMode.Open : FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.Position = offset; destination.Position = offset;
                var buffer = new byte[65536]; long done = offset; int read; progress((ulong)done, size);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    TransferWindow.Wait(request.WindowStartMinute,request.WindowEndMinute,cancellation);cancellation.ThrowIfCancellationRequested(); if(done+read>request.MaximumSourceBytes)throw new IOException("La sorgente supera il limite di questa operazione."); destination.Write(buffer, 0, read); done += read; progress((ulong)done, size);
                    if (request.BandwidthKiB > 0)
                    {
                        var delay = TimeSpan.FromSeconds((done - offset) / (request.BandwidthKiB * 1024d)) - watch.Elapsed;
                        if (delay > TimeSpan.Zero && cancellation.WaitHandle.WaitOne(delay)) cancellation.ThrowIfCancellationRequested();
                    }
                }
                destination.Flush(); if (done != size) throw new IOException("Dimensione sorgente cambiata durante il trasferimento.");
            }
            cancellation.ThrowIfCancellationRequested();
            if (request.ExpectedSourceHash != null || sourceHash!=null)
            {
                using Stream staged = request.Upload ? channel.OpenRead(partial) : File.OpenRead(partial);
                if (CheckpointHash(staged,cancellation) != (request.ExpectedSourceHash ?? sourceHash)) throw new IOException("La sorgente Ã¨ cambiata dal confronto. Ripeti lâ€™anteprima.");
                if (request.Upload ? channel.Exists(remote) && channel.Get(remote).IsSymbolicLink : File.Exists(local) && File.GetAttributes(local).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Destinazione collegata non ammessa.");
                FolderSync.CheckLocalParents(Path.GetDirectoryName(local)!);
            }
            if (request.ExpectedMissingTarget && (request.Upload ? channel.Exists(remote) : File.Exists(local) || System.IO.Directory.Exists(local))) throw new IOException("La destinazione Ã¨ comparsa dopo il confronto. Ripeti lâ€™anteprima.");
            if ((request.Upload ? File.GetLastWriteTimeUtc(local) : channel.GetAttributes(remote).LastWriteTimeUtc) != time || (request.Upload ? new FileInfo(local).Length : channel.GetAttributes(remote).Size) != size) throw new IOException("La sorgente Ã¨ cambiata durante il trasferimento. Destinazione originale conservata.");
            if(verifyContent || request.Resume && request.Checkpoint!=null)
            {
                TransferFileVersion? targetNow=null;
                if(request.Upload?channel.Exists(remote):File.Exists(local)){if(request.Upload){var a=channel.GetAttributes(remote);using var targetStream=channel.OpenRead(remote);targetNow=new(a.Size,a.LastWriteTimeUtc,verifyContent?CheckpointHash(targetStream,cancellation):null);}else{var a=new FileInfo(local);using var targetStream=File.OpenRead(local);targetNow=new(a.Length,a.LastWriteTimeUtc,verifyContent?CheckpointHash(targetStream,cancellation):null);}}
                (request.Checkpoint ?? checkpoint).Validate(new(new(size,time,sourceHash),targetNow));
            }
            if (request.Upload)
            {
                if (request.ExpectedHash != null)
                {
                    if (!channel.Exists(remote)) throw new IOException("CONFLITTO: il file remoto Ã¨ stato rimosso.");
                    using var original = channel.OpenRead(remote);
                    if (Convert.ToHexString(SHA256.HashData(original)) != request.ExpectedHash) throw new IOException("CONFLITTO: il file remoto Ã¨ cambiato. Ricaricalo prima di salvare.");
                }
                if (request.Permissions is { } mode) channel.ChangePermissions(partial, mode);
                else if (exists) { var attr = channel.GetAttributes(remote); var own = channel.GetAttributes(partial); own.OwnerCanRead = attr.OwnerCanRead; own.OwnerCanWrite = attr.OwnerCanWrite; own.OwnerCanExecute = attr.OwnerCanExecute; own.GroupCanRead = attr.GroupCanRead; own.GroupCanWrite = attr.GroupCanWrite; own.GroupCanExecute = attr.GroupCanExecute; own.OthersCanRead = attr.OthersCanRead; own.OthersCanWrite = attr.OthersCanWrite; own.OthersCanExecute = attr.OthersCanExecute; channel.SetAttributes(partial, own); }
                // No delete fallback: an unsupported atomic replace must preserve the original.
                var nowExists = channel.Exists(remote);
                if (nowExists && request.Conflict is not ("replace" or "resume")) throw new IOException("La destinazione Ã¨ comparsa durante il trasferimento. File originale conservato.");
                channel.RenameFile(partial, remote, nowExists);
            }
            else
            {
                if (request.ExpectedHash != null && (!File.Exists(local) || FolderSync.HashLocal(local) != request.ExpectedHash)) throw new IOException("CONFLITTO: il file locale Ã¨ cambiato dal confronto.");
                File.Move(partial, local, request.Conflict is "replace" or "resume");
            }
            lock (_transferSources) _transferSources.Remove(request.Id);
            progress((ulong)size, size);
            return true;
        }, cancellation);
    }
    public async Task DiscardPartialAsync(TransferRequest request)
    {
        if (!Guid.TryParse(request.Id, out _)) throw new ArgumentException("Identificatore non valido.");
        if (request.Directory) return;
        var path = (request.Upload ? request.Remote : Path.GetFullPath(request.Local)) + ".relay-" + request.Id + ".partial";
        if (request.Upload) { using var channel = await FileChannel(CancellationToken.None); await Task.Run(() => { if (channel.Exists(path)) channel.DeleteFile(path); }); }
        else if (File.Exists(path)) File.Delete(path);
    }
    static string CheckpointHash(Stream stream,CancellationToken cancellation)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];int count;
        while((count=stream.Read(buffer,0,buffer.Length))>0){cancellation.ThrowIfCancellationRequested();hash.AppendData(buffer,0,count);}return Convert.ToHexString(hash.GetHashAndReset());
    }
    static void EnsureRemoteDirectory(SftpClient channel, string path)
    {
        var current = path.StartsWith('/') ? "/" : ".";
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue; if (part == "..") throw new IOException("Percorso destinazione non valido.");
            current = current.TrimEnd('/') + "/" + part;
            if (!channel.Exists(current)) { try { channel.CreateDirectory(current); } catch (SftpException) { if (!channel.Exists(current)) throw; } }
            var attr = channel.GetAttributes(current); if (!attr.IsDirectory || channel.Get(current).IsSymbolicLink) throw new IOException("La destinazione deve essere una cartella, senza link simbolici.");
        }
    }
    static void VerifyPrefix(Stream source, Stream partial, long count, CancellationToken token)
    {
        var a = new byte[65536]; var b = new byte[65536]; long done = 0;
        while (done < count)
        {
            token.ThrowIfCancellationRequested(); var length = (int)Math.Min(a.Length, count - done);
            source.ReadExactly(a.AsSpan(0, length)); partial.ReadExactly(b.AsSpan(0, length));
            if (!CryptographicOperations.FixedTimeEquals(a.AsSpan(0, length), b.AsSpan(0, length))) throw new IOException("Il file parziale non corrisponde alla sorgente: ripresa rifiutata.");
            done += length;
        }
    }
}
