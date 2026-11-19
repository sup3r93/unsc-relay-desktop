using System.Security.Cryptography;
using System.Text.Json;

namespace UnscRelay.Core;

public static class UpdateBackup
{
    // Explicit allowlist: no WebView cache, downloads or plaintext private key exports.
    public static readonly string[] DataFiles = ["profiles.json", "interface-language.json", "keys.vault", "hostkeys.json", "github-token.dpapi", "transfers.json", "account-sync.dpapi", "account-unlock.dpapi", "account-hello.dpapi", "accounts.dpapi"];
    public static string Create(string dataDirectory, string version)
    {
        dataDirectory = LocalDataFiles.Root(dataDirectory);
        FolderSync.CheckLocalParents(dataDirectory);
        var backupRoot = Path.Combine(dataDirectory, "UpdateBackups");
        FolderSync.CheckLocalParents(backupRoot);
        var files=DataFiles.ToList();
        foreach(var account in new AccountWorkspaces(dataDirectory).Load().Accounts.Where(a=>a.Id!=Guid.Empty))
        {
            var relative=Path.Combine("Accounts",account.Id.ToString("N"));FolderSync.CheckLocalParents(LocalDataFiles.Child(dataDirectory,relative));
            files.AddRange(DataFiles.Where(name=>name is not("accounts.dpapi" or "github-token.dpapi")).Select(name=>Path.Combine(relative,name)));
        }
        var prefixes=new[]{""}.Concat(new AccountWorkspaces(dataDirectory).Load().Accounts.Where(a=>a.Id!=Guid.Empty).Select(a=>Path.Combine("Accounts",a.Id.ToString("N"))));
        foreach(var prefix in prefixes)
        {
            var relative=Path.Combine(prefix,"ArchiveCopies");var sourceRoot=LocalDataFiles.Child(dataDirectory,relative);FolderSync.CheckLocalParents(sourceRoot);if(!Directory.Exists(sourceRoot))continue;
            foreach(var source in Directory.EnumerateFiles(sourceRoot,"*.json",SearchOption.TopDirectoryOnly))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(source),"N",out var id))continue;
                if(File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Il backup non può includere copie collegate.");
                using(var stream=LocalDataFiles.OpenRead(dataDirectory,Path.Combine(relative,id.ToString("N")+".json"),64*1024*1024))
                {
                    if(stream.Length>64*1024*1024)throw new InvalidDataException("Copia archivio troppo grande.");var copy=JsonSerializer.Deserialize<ArchiveCopy>(stream,VaultSyncCrypto.Json)??throw new InvalidDataException("Copia archivio non valida.");
                    if(copy.ArchiveId!=id||copy.Account is not{Length:64}||!copy.Account.All(char.IsAsciiHexDigit)||copy.Revision<1)throw new InvalidDataException("Copia archivio non valida.");VaultSyncCrypto.Validate(copy.Envelope,copy.Envelope.Owner);
                }
                files.Add(Path.Combine(relative,id.ToString("N")+".json"));
            }
        }
        // Validate the complete allowlist before creating a backup or copying any file.
        foreach (var name in files)
        {
            var source = LocalDataFiles.Child(dataDirectory, name);
            if (File.Exists(source) && File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Il backup non può includere file collegati: " + name);
        }
        var directory = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var hashes = new Dictionary<string, string>();
        foreach (var name in files)
        {
            var source = LocalDataFiles.Child(dataDirectory, name); if (!File.Exists(source)) continue;
            using var input = LocalDataFiles.OpenRead(dataDirectory, name, 512L * 1024 * 1024);
            var target = LocalDataFiles.Child(directory, name); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            LocalDataFiles.Child(directory, name);
            using var stream = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            input.CopyTo(stream); stream.Flush(true); stream.Position = 0;
            hashes[name] = Convert.ToHexString(SHA256.HashData(stream));
        }
        AtomicFile.Write(Path.Combine(directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(new { version, createdUtc = DateTime.UtcNow, hashes }, ProfileStore.Json));
        return directory;
    }
}
