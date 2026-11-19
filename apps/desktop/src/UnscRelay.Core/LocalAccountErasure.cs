using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace UnscRelay.Core;
public sealed record LocalErasureResult(int Files,long Bytes,bool Completed);
/// <summary>Runs only after sessions and writers for the account have stopped.</summary>
public sealed class LocalAccountErasure
{
 readonly string root;readonly LocalErasureJournal journal;
 public LocalAccountErasure(string directory):this(directory,new LocalErasureJournal(directory)){}
 internal LocalAccountErasure(string directory,LocalErasureJournal proof){root=LocalDataFiles.Root(directory);journal=proof;}
 static string Join(string parent,string name)=>parent.Length==0?name:parent+"/"+name;
 static bool InScope(string relative,string scope)=>scope.Length==0||relative.StartsWith(scope+"/",StringComparison.OrdinalIgnoreCase);
 static byte[] ReadHeld(ManagedFileDeletion file,int maximum){var stream=file.Content;if(stream.Length>maximum)throw new InvalidDataException("File locale troppo grande per la rimozione verificata.");var bytes=new byte[checked((int)stream.Length)];stream.ReadExactly(bytes);return bytes;}
 string Leaf(string relative){var full=FolderSync.LocalChild(root,relative);FolderSync.CheckLocalParents(Path.GetDirectoryName(full)!);return full;}
 static bool Exists(string path){try{File.GetAttributes(path);return true;}catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}}
 public async Task<LocalErasureResult> Run(string owner,Func<WindowsHelloChallenge,Task> removeHello,CancellationToken cancellation=default)
 {
  ArgumentNullException.ThrowIfNull(removeHello);var plan=new LocalErasurePlanner(root,journal).Create(owner);if(plan.Notice.Completed&&plan.Scopes.Length==0&&plan.Unattributed.Length==0)return new(0,0,true);if(plan.Bindings.Length>2048)throw new IOException("Troppe copie locali da rimuovere in una sola operazione.");
  // The DPAPI journal is outside every backup. Record discovered workspaces before touching data.
  journal.Save(plan.Notice with{Completed=false});if(plan.Unattributed.Length>0)throw new IOException("Una copia locale non ha un’identità verificabile. Nessun dato ambiguo verrà rimosso; la pulizia resta in attesa.");var held=new Dictionary<string,ManagedFileDeletion>(StringComparer.OrdinalIgnoreCase);var deleted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var files=0;long bytes=0;
  try{
   foreach(var binding in plan.Bindings){cancellation.ThrowIfCancellationRequested();var handle=new ManagedFileDeletion(LocalDataFiles.Child(root,binding));try{var state=AccountSyncStore.Decode(ReadHeld(handle,256*1024));if(state.Owner!=owner||state.Subject!=plan.Notice.Subject)throw new InvalidDataException("Lo spazio account è cambiato. Nessun dato di un altro account verrà rimosso.");held.Add(binding,handle);}catch{handle.Dispose();throw;}}
   var hello=(plan.Notice.HelloKeys??[]).ToList();
   foreach(var scope in plan.Scopes)hello.AddRange(new WindowsHelloRegistration(Path.GetDirectoryName(LocalDataFiles.Child(root,Join(scope.Relative,"account-hello.dpapi")))!).Load(owner,plan.Notice.Subject));
   var keys=hello.DistinctBy(value=>(value.KeyName,value.PublicKey)).ToArray();if(keys.Length>1000)throw new InvalidDataException("Troppi collegamenti Windows Hello.");journal.Save(plan.Notice with{Completed=false,HelloKeys=keys});foreach(var key in keys){cancellation.ThrowIfCancellationRequested();await removeHello(key);}
   foreach(var item in plan.Items){cancellation.ThrowIfCancellationRequested();if(!plan.Scopes.Any(scope=>InScope(item.Relative,scope.Relative)&&held.ContainsKey(Join(scope.Relative,"account-sync.dpapi"))))throw new InvalidDataException("File privo di collegamento account verificato.");var path=Leaf(item.Relative);if(!Exists(path)){deleted.Add(item.Relative);continue;}using(var file=new ManagedFileDeletion(path,item.Link)){file.Delete();}deleted.Add(item.Relative);files++;bytes+=item.Bytes;}
   // Redact only this owner from shared indexes; never delete the indexes of other accounts.
   foreach(var relative in plan.Indexes){cancellation.ThrowIfCancellationRequested();var path=LocalDataFiles.Child(root,relative);if(!File.Exists(path))continue;var directory=Path.GetDirectoryName(path)!;var spaces=new AccountWorkspaces(directory);var index=spaces.Load();var removed=index.Accounts.Where(value=>value.Owner==owner).Select(value=>value.Id).ToHashSet();if(removed.Count==0)continue;index.Accounts.RemoveAll(value=>value.Owner==owner);if(removed.Contains(index.Active)){if(index.Accounts.Count==0){var fresh=Guid.NewGuid();index.Accounts.Add(new(fresh,"","",""));index.Active=fresh;}else index.Active=index.Accounts[0].Id;}spaces.Save(index);}
   foreach(var relative in plan.Manifests){var path=LocalDataFiles.Child(root,relative);if(!File.Exists(path))continue;var content=LocalDataFiles.Read(root,relative,4*1024*1024);JsonObject manifest;try{manifest=JsonNode.Parse(content)?.AsObject()??throw new InvalidDataException("Manifest del backup non valido.");}finally{CryptographicOperations.ZeroMemory(content);}var hashes=manifest["hashes"]?.AsObject()??throw new InvalidDataException("Manifest del backup non valido.");var prefix=relative[..^"manifest.json".Length];foreach(var pair in hashes.ToArray()){var full=prefix+pair.Key.Replace('\\','/');if(deleted.Contains(full)||plan.Bindings.Contains(full,StringComparer.OrdinalIgnoreCase)){hashes.Remove(pair.Key);continue;}if(plan.Indexes.Contains(full,StringComparer.OrdinalIgnoreCase)&&File.Exists(LocalDataFiles.Child(root,full))){using var stream=LocalDataFiles.OpenRead(root,full,256*1024);hashes[pair.Key]=Convert.ToHexString(SHA256.HashData(stream));}}LocalDataFiles.Write(root,relative,JsonSerializer.SerializeToUtf8Bytes(manifest,ProfileStore.Json));}
   void PruneEmpty(string relative,int depth=0){if(depth>64)throw new IOException("Troppe cartelle nella rimozione locale.");var path=LocalDataFiles.Child(root,relative);if(!Directory.Exists(path))return;foreach(var child in new DirectoryInfo(path).EnumerateDirectories()){if(child.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("Una cartella è cambiata durante la rimozione.");PruneEmpty(Join(relative,child.Name),depth+1);}if(!Directory.EnumerateFileSystemEntries(path).Any()){using var directory=new ManagedFileDeletion(path,directoryObject:true);directory.Delete();}}
   foreach(var scope in plan.Scopes)foreach(var folder in LocalErasurePlanner.Folders)PruneEmpty(Join(scope.Relative,folder));
   // Credentials are the final files removed. Held handles prevent rebinding while cleaning.
   foreach(var pair in held){cancellation.ThrowIfCancellationRequested();pair.Value.Delete();files++;}foreach(var handle in held.Values)handle.Dispose();held.Clear();
   if(plan.Items.Any(item=>Exists(Leaf(item.Relative)))||plan.Bindings.Any(binding=>Exists(LocalDataFiles.Child(root,binding))))throw new IOException("La rimozione locale richiede un nuovo tentativo. L’account resta bloccato.");
   // If a recorded live scope lost its identity before this run, never infer ownership of residual data.
   foreach(var id in plan.Notice.Workspaces){var directory=new AccountWorkspaces(root).DirectoryFor(id);var state=new AccountSyncStore(directory).Load();if(state.Owner.Length>0&&state.Owner!=owner)continue;if(state.Owner.Length==0&&(LocalErasurePlanner.Files.Any(name=>Exists(LocalDataFiles.Child(directory,name)))||LocalErasurePlanner.Folders.Any(name=>{var path=Leaf(Path.GetRelativePath(root,Path.Combine(directory,name)).Replace('\\','/'));return Exists(path)&&(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)||Directory.EnumerateFileSystemEntries(path).Any());})))throw new IOException("Sono rimasti dati senza identità verificabile. L’account resta bloccato.");}
   journal.Save(plan.Notice with{Completed=true,HelloKeys=keys});return new(files,bytes,true);
  }finally{foreach(var handle in held.Values)handle.Dispose();}
 }
}
