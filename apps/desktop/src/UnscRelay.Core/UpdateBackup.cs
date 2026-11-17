using System.Security.Cryptography;
using System.Text.Json;

namespace UnscRelay.Core;

public static class UpdateBackup
{
    // Explicit allowlist: no WebView cache, downloads or plaintext private key exports.
    public static readonly string[] DataFiles = ["profiles.json", "keys.vault", "hostkeys.json", "github-token.dpapi", "transfers.json", "account-sync.dpapi", "account-unlock.dpapi", "accounts.dpapi"];
    public static string Create(string dataDirectory, string version)
    {
        dataDirectory = Path.GetFullPath(dataDirectory);
        FolderSync.CheckLocalParents(dataDirectory);
        var backupRoot = Path.Combine(dataDirectory, "UpdateBackups");
        FolderSync.CheckLocalParents(backupRoot);
        var files=DataFiles.ToList();
        foreach(var account in new AccountWorkspaces(dataDirectory).Load().Accounts.Where(a=>a.Id!=Guid.Empty))
        {
            var relative=Path.Combine("Accounts",account.Id.ToString("N"));FolderSync.CheckLocalParents(Path.Combine(dataDirectory,relative));
            files.AddRange(DataFiles.Where(name=>name is not("accounts.dpapi" or "github-token.dpapi")).Select(name=>Path.Combine(relative,name)));
        }
        var prefixes=new[]{""}.Concat(new AccountWorkspaces(dataDirectory).Load().Accounts.Where(a=>a.Id!=Guid.Empty).Select(a=>Path.Combine("Accounts",a.Id.ToString("N"))));
        foreach(var prefix in prefixes)
        {
            var relative=Path.Combine(prefix,"ArchiveCopies");var sourceRoot=Path.Combine(dataDirectory,relative);FolderSync.CheckLocalParents(sourceRoot);if(!Directory.Exists(sourceRoot))continue;
            foreach(var source in Directory.EnumerateFiles(sourceRoot,"*.json",SearchOption.TopDirectoryOnly))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(source),"N",out var id))continue;
                if(File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Il backup non può includere copie collegate.");
                using(var stream=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length>64*1024*1024)throw new InvalidDataException("Copia archivio troppo grande.");var copy=JsonSerializer.Deserialize<ArchiveCopy>(stream,VaultSyncCrypto.Json)??throw new InvalidDataException("Copia archivio non valida.");
                    if(copy.ArchiveId!=id||copy.Account is not{Length:64}||!copy.Account.All(char.IsAsciiHexDigit)||copy.Revision<1)throw new InvalidDataException("Copia archivio non valida.");VaultSyncCrypto.Validate(copy.Envelope,copy.Envelope.Owner);
                }
                files.Add(Path.Combine(relative,Path.GetFileName(source)));
            }
        }
        // Validate the complete allowlist before creating a backup or copying any file.
        foreach (var name in files)
        {
            var source = Path.Combine(dataDirectory, name);
            if (File.Exists(source) && File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Il backup non può includere file collegati: " + name);
        }
        var directory = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var hashes = new Dictionary<string, string>();
        foreach (var name in files)
        {
            var source = Path.Combine(dataDirectory, name); if (!File.Exists(source)) continue;
            var target = Path.Combine(directory, name); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, false);
            using var stream = File.OpenRead(target); hashes[name] = Convert.ToHexString(SHA256.HashData(stream));
        }
        AtomicFile.Write(Path.Combine(directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(new { version, createdUtc = DateTime.UtcNow, hashes }, ProfileStore.Json));
        return directory;
    }
}
