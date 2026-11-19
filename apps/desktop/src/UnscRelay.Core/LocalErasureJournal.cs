using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed class LocalAccountErasedException():IOException("Questo account è stato cancellato. I dati locali restano bloccati mentre la rimozione sicura viene completata.");
public sealed record LocalErasureNotice(int Version,string Owner,string Subject,ErasureReceipt Receipt,Guid[] Workspaces,bool Completed=false,WindowsHelloChallenge[]? HelloKeys=null);
public sealed class LocalErasureJournal
{
 readonly string root;readonly ErasureReceiptVerifier verifier;
 public LocalErasureJournal(string directory):this(directory,new ErasureReceiptVerifier()){}
 internal LocalErasureJournal(string directory,ErasureReceiptVerifier trusted){root=LocalDataFiles.Root(directory);verifier=trusted;}
 string Relative(string owner){if(owner is not{Length:64}||!owner.All(char.IsAsciiHexDigit))throw new InvalidDataException("Identità della cancellazione locale non valida.");return "ErasureJournal/"+owner+".dpapi";}
 LocalErasureNotice Validate(LocalErasureNotice value){if(value.Version!=1||value.Owner is not{Length:64}||value.Subject is null||value.Receipt==null||value.Workspaces is null||value.Workspaces.Length is <1 or >1000||value.Workspaces.Distinct().Count()!=value.Workspaces.Length||value.HelloKeys is{Length:>1000})throw new InvalidDataException("Registro della cancellazione locale non valido.");verifier.Verify(value.Receipt,value.Owner,value.Subject,DateTimeOffset.UtcNow);return value;}
 internal LocalErasureNotice? Find(string owner){var relative=Relative(owner);if(!File.Exists(LocalDataFiles.Child(root,relative)))return null;var sealedBytes=LocalDataFiles.Read(root,relative,256*1024);byte[]? bytes=null;try{bytes=UpdateTokenStore.Transform(sealedBytes,true);var value=JsonSerializer.Deserialize<LocalErasureNotice>(bytes,VaultSyncCrypto.Json)??throw new InvalidDataException("Registro della cancellazione locale non valido.");if(value.Owner!=owner)throw new InvalidDataException("Registro della cancellazione locale non valido.");return Validate(value);}finally{CryptographicOperations.ZeroMemory(sealedBytes);if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);}}
 internal void Save(LocalErasureNotice notice){Validate(notice);var bytes=JsonSerializer.SerializeToUtf8Bytes(notice,VaultSyncCrypto.Json);try{if(bytes.Length>256*1024)throw new InvalidDataException("Registro della cancellazione locale troppo grande.");LocalDataFiles.Write(root,Relative(notice.Owner),UpdateTokenStore.Transform(bytes,false));}finally{CryptographicOperations.ZeroMemory(bytes);}}
 public IReadOnlyList<LocalErasureNotice> Notices(){var directory=LocalDataFiles.Child(root,"ErasureJournal");if(!Directory.Exists(directory))return [];var files=Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly).ToArray();if(files.Length>2048)throw new InvalidDataException("Troppi registri di cancellazione locale.");var result=new List<LocalErasureNotice>();foreach(var file in files){if(Path.GetExtension(file)!=".dpapi")throw new InvalidDataException("File non riconosciuto nel registro delle cancellazioni.");var owner=Path.GetFileNameWithoutExtension(file);result.Add(Find(owner)??throw new InvalidDataException("Registro della cancellazione locale scomparso."));}return result;}
 public LocalErasureNotice Accept(ErasureReceipt receipt,string owner,string subject,Guid requestId,Guid activeWorkspace)
 {
  var claims=verifier.Verify(receipt,owner,subject,DateTimeOffset.UtcNow,requestId);var spaces=new AccountWorkspaces(root);var index=spaces.Load();var state=new AccountSyncStore(spaces.DirectoryFor(activeWorkspace)).Load();if(state.Owner!=owner||state.Subject!=subject)throw new InvalidDataException("La ricevuta non appartiene allo spazio locale selezionato.");
  var old=Find(owner);if(old!=null){var previous=verifier.Verify(old.Receipt,owner,subject,DateTimeOffset.UtcNow,requestId);if(previous.RequestId!=claims.RequestId)throw new InvalidDataException("La richiesta di cancellazione è cambiata.");return old;}
  var targets=new HashSet<Guid>{activeWorkspace};foreach(var account in index.Accounts.Where(account=>account.Owner==owner)){var current=new AccountSyncStore(spaces.DirectoryFor(account.Id)).Load();if(current.Owner!=owner||current.Subject!=subject)throw new InvalidDataException("Uno spazio account non corrisponde alla cancellazione.");targets.Add(account.Id);}
  var notice=new LocalErasureNotice(1,owner,subject,receipt,targets.Order().ToArray());Save(notice);return notice;
 }
 void GuardState(AccountSyncState state){if(state.Owner is{Length:64}&&Find(state.Owner) is{} notice){verifier.Verify(notice.Receipt,state.Owner,state.Subject,DateTimeOffset.UtcNow);throw new LocalAccountErasedException();}}
 public void GuardWorkspace(Guid workspace)
 {
  var directory=new AccountWorkspaces(root).DirectoryFor(workspace);var state=new AccountSyncStore(directory).Load();GuardState(state);
  var recovery=LocalDataFiles.Child(directory,"sync-transaction.json");
  if(File.Exists(recovery)){
   var bytes=LocalDataFiles.Read(directory,"sync-transaction.json",4096);string? name;try{name=JsonSerializer.Deserialize<string>(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
   if(string.IsNullOrEmpty(name)||name!=Path.GetFileName(name)||name.Contains("..",StringComparison.Ordinal))throw new InvalidDataException("Ripristino locale non valido.");
   var backup=LocalDataFiles.Child(directory,"MigrationBackups/"+name);var backupState=new AccountSyncStore(backup).Load();GuardState(backupState);
   if(backupState.Owner.Length==0&&Notices().Any(notice=>notice.Workspaces.Contains(workspace)))throw new LocalAccountErasedException();
  }
  if(state.Owner.Length==0&&Notices().Any(notice=>!notice.Completed&&notice.Workspaces.Contains(workspace)))throw new LocalAccountErasedException();
 }
}
