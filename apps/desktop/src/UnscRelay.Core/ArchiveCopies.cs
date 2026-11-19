using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed record ArchiveCopy(string Account,Guid ArchiveId,long Revision,DateTime UpdatedAt,SyncEnvelope Envelope,bool Pending);
public sealed record ArchiveCopySummary(string Account,Guid ArchiveId,string ArchiveOwner,long Revision,bool Pending,DateTime ObservedAt,DateTime CopyUpdatedAt,long CopyBytes,long CopyWriteTicks);
public static class ArchiveDescriptorRules
{
 public static void Validate(SharedArchive value)
 {
  if(value.Id==Guid.Empty||value.Name is not{Length:>0 and <=100}||value.OwnerAccount is not{Length:64}||!value.OwnerAccount.All(char.IsAsciiHexDigit)||value.ArchiveOwner is not{Length:64}||!value.ArchiveOwner.All(char.IsAsciiHexDigit)||value.Access is not("owner" or "read" or "write"))throw new InvalidDataException("Descrizione archivio non valida.");
 }
}
public sealed class ArchiveCopies(string directory,string account)
{
 string PathFor(Guid id){if(id==Guid.Empty||account is not{Length:64}||!account.All(char.IsAsciiHexDigit))throw new ArgumentException("Archivio non valido.");var folder=Path.Combine(Path.GetFullPath(directory),"ArchiveCopies");FolderSync.CheckLocalParents(folder);return Path.Combine(folder,id.ToString("N")+".json");}
 public ArchiveCopy? Read(SharedArchive archive)
 {
  ArchiveDescriptorRules.Validate(archive);var path=PathFor(archive.Id);if(!File.Exists(path))return null;if(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))throw new IOException("La copia dell’archivio non può essere un collegamento.");
  using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(stream.Length>64*1024*1024)throw new InvalidDataException("Copia dell’archivio troppo grande.");var copy=JsonSerializer.Deserialize<ArchiveCopy>(stream,VaultSyncCrypto.Json)??throw new InvalidDataException("Copia dell’archivio non valida.");
  if(copy.Account!=account||copy.ArchiveId!=archive.Id||copy.Revision<1)throw new InvalidDataException("La copia locale appartiene a un altro account o archivio.");VaultSyncCrypto.Validate(copy.Envelope,archive.ArchiveOwner);return copy;
 }
 public ArchiveCopySummary? Summary(SharedArchive archive)
 {
  ArchiveDescriptorRules.Validate(archive);var path=PathFor(archive.Id);var folder=Path.GetDirectoryName(path)!;var name=archive.Id.ToString("N")+".status.dpapi";if(!File.Exists(path)||!File.Exists(LocalDataFiles.Child(folder,name)))return null;if(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))throw new IOException("La copia dell’archivio non può essere un collegamento.");var encrypted=LocalDataFiles.Read(folder,name,8192);byte[]? plain=null;
  try{plain=UpdateTokenStore.Transform(encrypted,true);var summary=JsonSerializer.Deserialize<ArchiveCopySummary>(plain,VaultSyncCrypto.Json)??throw new InvalidDataException("Riepilogo della copia non valido.");if(summary.Account!=account||summary.ArchiveId!=archive.Id||summary.ArchiveOwner!=archive.ArchiveOwner||summary.Revision<1||summary.CopyBytes<1||summary.ObservedAt==default)throw new InvalidDataException("Riepilogo appartenente a un altro account o archivio.");var file=new FileInfo(path);return file.Length==summary.CopyBytes&&file.LastWriteTimeUtc.Ticks==summary.CopyWriteTicks?summary:null;}finally{CryptographicOperations.ZeroMemory(encrypted);if(plain!=null)CryptographicOperations.ZeroMemory(plain);}
 }
 void SaveSummary(SharedArchive archive,RemoteVault remote,bool pending,string path){var file=new FileInfo(path);var plain=JsonSerializer.SerializeToUtf8Bytes(new ArchiveCopySummary(account,archive.Id,archive.ArchiveOwner,remote.Revision,pending,DateTime.UtcNow,remote.UpdatedAt,file.Length,file.LastWriteTimeUtc.Ticks),VaultSyncCrypto.Json);byte[]? encrypted=null;try{encrypted=UpdateTokenStore.Transform(plain,false);LocalDataFiles.Write(Path.GetDirectoryName(path)!,archive.Id.ToString("N")+".status.dpapi",encrypted);}finally{CryptographicOperations.ZeroMemory(plain);if(encrypted!=null)CryptographicOperations.ZeroMemory(encrypted);}}
 public void Save(SharedArchive archive,RemoteVault remote,bool pending=false,bool replacePending=false)
 {
  ArchiveDescriptorRules.Validate(archive);VaultSyncCrypto.Validate(remote.Envelope,archive.ArchiveOwner);if(remote.Revision<1)throw new InvalidDataException("Revisione non valida.");var previous=Read(archive);if(previous is {Pending:true}&&!pending&&!replacePending)return;
  var path=PathFor(archive.Id);Directory.CreateDirectory(Path.GetDirectoryName(path)!);FolderSync.CheckLocalParents(Path.GetDirectoryName(path)!);if(File.Exists(path)&&File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))throw new IOException("La copia dell’archivio non può essere un collegamento.");
  AtomicFile.Write(path,JsonSerializer.SerializeToUtf8Bytes(new ArchiveCopy(account,archive.Id,remote.Revision,remote.UpdatedAt,remote.Envelope,pending),VaultSyncCrypto.Json));SaveSummary(archive,remote,pending,path);
 }
}
public sealed partial class KeyVault
{
 public List<SharedArchive> RememberedArchives(string account)
 {
  lock(_gate){RequireUnlocked();var entries=_records.Where(r=>r.Info.Kind=="archive-reference"&&r.Info.Comment==account).Select(r=>JsonSerializer.Deserialize<SharedArchive>(r.Content,VaultSyncCrypto.Json)??throw new InvalidDataException("Archivio locale non valido.")).ToList();foreach(var entry in entries)ArchiveDescriptorRules.Validate(entry);return entries;}
 }
 public void RememberArchive(string account,SharedArchive archive)
 {
  ArchiveDescriptorRules.Validate(archive);if(account is not{Length:64}||!account.All(char.IsAsciiHexDigit))throw new ArgumentException("Account non valido.");
  lock(_gate){RequireUnlocked();var previous=_records.SingleOrDefault(r=>r.Info.Kind=="archive-reference"&&r.Info.Comment==account&&r.Info.Id==archive.Id.ToString("D"));var replacement=new KeyRecord(new KeyInfo(archive.Id.ToString("D"),archive.Name,account,DateTime.UtcNow,Kind:"archive-reference"),JsonSerializer.SerializeToUtf8Bytes(archive,VaultSyncCrypto.Json));if(previous!=null)_records.Remove(previous);_records.Add(replacement);try{Save();if(previous!=null)CryptographicOperations.ZeroMemory(previous.Content);}catch{_records.Remove(replacement);if(previous!=null)_records.Add(previous);CryptographicOperations.ZeroMemory(replacement.Content);throw;}}
 }
}
