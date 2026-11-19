namespace UnscRelay.Core;
public sealed record LocalAccountEntry(Guid Workspace,string Directory,AccountSyncState State);
/// <summary>Includes remembered and orphaned GUID workspaces, without following links.</summary>
public sealed class LocalAccountInventory(string directory)
{
 public static bool HasCanonicalIdentity(AccountSyncState state)=>state.Owner is{Length:64}&&state.Owner.All(value=>value is >='0' and <='9' or >='A' and <='F')&&Guid.TryParseExact(state.Subject,"D",out var subject)&&subject!=Guid.Empty;
 public IReadOnlyList<LocalAccountEntry> Accounts(){var root=LocalDataFiles.Root(directory);var spaces=new AccountWorkspaces(root);var ids=spaces.Load().Accounts.Select(value=>value.Id).Append(Guid.Empty).ToHashSet();var accounts=LocalDataFiles.Child(root,"Accounts");if(System.IO.Directory.Exists(accounts)){var children=new DirectoryInfo(accounts).EnumerateDirectories().Take(1001).ToArray();if(children.Length>1000)throw new IOException("Troppi spazi account locali.");foreach(var child in children)if(Guid.TryParseExact(child.Name,"N",out var id)){if(child.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("Uno spazio account è un collegamento.");ids.Add(id);}}var result=new List<LocalAccountEntry>();foreach(var id in ids){var path=spaces.DirectoryFor(id);var state=new AccountSyncStore(path).Load();if(state.Owner.Length==64)result.Add(new(id,path,state));}return result;}
}
