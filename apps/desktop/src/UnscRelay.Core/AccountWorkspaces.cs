using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed record RememberedAccount(Guid Id,string Owner,string Email,string Name,string Alias="",string Color="olive");
public sealed class AccountWorkspaceIndex {public int Version{get;set;}=1;public Guid Active{get;set;}public List<RememberedAccount> Accounts{get;set;}=[];}
public sealed class AccountWorkspaces(string root)
{
 public static readonly string[] Colors=["olive","blue","purple","orange","teal","rose"];
 string FilePath=>LocalDataFiles.Child(root,"accounts.dpapi");
 public AccountWorkspaceIndex Load()
 {
  FolderSync.CheckLocalParents(root);if(!File.Exists(FilePath))return new();if(File.GetAttributes(FilePath).HasFlag(FileAttributes.ReparsePoint)||new FileInfo(FilePath).Length>256*1024)throw new InvalidDataException("Elenco account non valido. I dati non sono stati modificati.");
  var bytes=UpdateTokenStore.Transform(LocalDataFiles.Read(root,"accounts.dpapi",256*1024),true);try{var value=JsonSerializer.Deserialize<AccountWorkspaceIndex>(bytes,ProfileStore.Json)??throw new InvalidDataException("Elenco account non valido.");Validate(value);return value;}finally{CryptographicOperations.ZeroMemory(bytes);}
 }
 static void Validate(AccountWorkspaceIndex index)
 {
  if(index.Version!=1||index.Accounts==null||index.Accounts.Count>20||index.Accounts.Any(a=>a==null||a.Owner==null||a.Email==null||a.Name==null||a.Owner.Length!=0&&(a.Owner.Length!=64||!a.Owner.All(char.IsAsciiHexDigit))||a.Alias==null||a.Color==null||a.Alias.Length>80||a.Alias.Any(char.IsControl)||!Colors.Contains(a.Color,StringComparer.Ordinal)||a.Email.Length>254||a.Name.Length>254||a.Email.Any(char.IsControl)||a.Name.Any(char.IsControl))||index.Accounts.Select(a=>a.Id).Distinct().Count()!=index.Accounts.Count||index.Active!=Guid.Empty&&!index.Accounts.Any(a=>a.Id==index.Active))throw new InvalidDataException("Elenco account non valido.");
 }
 public void Save(AccountWorkspaceIndex index){Validate(index);FolderSync.CheckLocalParents(root);if(File.Exists(FilePath)&&File.GetAttributes(FilePath).HasFlag(FileAttributes.ReparsePoint))throw new IOException("L’elenco account non può essere un collegamento.");var bytes=JsonSerializer.SerializeToUtf8Bytes(index,ProfileStore.Json);try{LocalDataFiles.Write(root,"accounts.dpapi",UpdateTokenStore.Transform(bytes,false));}finally{CryptographicOperations.ZeroMemory(bytes);}}
 public string DirectoryFor(Guid id)
 {
  var path=id==Guid.Empty?LocalDataFiles.Root(root):LocalDataFiles.Child(root,"Accounts/"+id.ToString("N"));FolderSync.CheckLocalParents(path);return path;
 }
 public string ActiveDirectory()=>DirectoryFor(Load().Active);
 public void Remember(AccountSyncState state)
 {
  if(state.Owner is not{Length:64}||!state.Owner.All(char.IsAsciiHexDigit))throw new InvalidDataException("Account non valido.");var index=Load();var previous=index.Accounts.SingleOrDefault(a=>a.Id==index.Active);if(previous is {Owner.Length:>0}&&previous.Owner!=state.Owner)throw new InvalidDataException("Questo spazio appartiene a un altro account.");
  index.Accounts.RemoveAll(a=>a.Id==index.Active);index.Accounts.Add(new(index.Active,state.Owner,CloudAccountClient.RememberedEmail(state),state.Name.Length<=254?state.Name:state.Name[..254],previous?.Alias??"",previous?.Color??"olive"));Save(index);
 }
 public Guid Add(string email)
 {
  email=email.Trim();if(email.Length is <1 or >254||email.Any(char.IsControl)||!System.Net.Mail.MailAddress.TryCreate(email,out var address)||address.Address!=email)throw new ArgumentException("Inserisci un indirizzo email valido.");var index=Load();if(index.Accounts.Count>=20)throw new InvalidOperationException("Rimuovi un account dall’elenco prima di aggiungerne un altro.");
  var existing=index.Accounts.SingleOrDefault(a=>a.Email.Equals(email,StringComparison.OrdinalIgnoreCase));if(existing!=null)return existing.Id;
  var id=Guid.NewGuid();var directory=DirectoryFor(id);Directory.CreateDirectory(directory);FolderSync.CheckLocalParents(directory);new AccountSyncStore(directory).Save(new(){Email=email});index.Accounts.Add(new(id,"",email,email));Save(index);return id;
 }
 public void Customize(Guid id,string alias,string color)
 {
  alias=alias.Trim();if(alias.Length>80||alias.Any(char.IsControl)||!Colors.Contains(color,StringComparer.Ordinal))throw new ArgumentException("Controlla il nome e il colore dell’account.");
  var index=Load();var position=index.Accounts.FindIndex(a=>a.Id==id);if(position<0)throw new ArgumentException("Account non presente nell’elenco.");index.Accounts[position]=index.Accounts[position] with{Alias=alias,Color=color};Save(index);
 }
 public void Select(Guid id){var index=Load();if(id!=Guid.Empty&&!index.Accounts.Any(a=>a.Id==id))throw new ArgumentException("Account non presente nell’elenco.");DirectoryFor(id);index.Active=id;Save(index);}
 public void Forget(Guid id){var index=Load();if(index.Active==id)throw new InvalidOperationException("Passa a un altro account prima di rimuovere questo dall’elenco.");index.Accounts.RemoveAll(a=>a.Id==id);Save(index);}
}
