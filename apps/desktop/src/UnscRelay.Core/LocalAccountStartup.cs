namespace UnscRelay.Core;
/// <summary>Runs before profile/vault recovery; only an independently bound foreign account may proceed past another owner's pending cleanup.</summary>
public sealed class LocalAccountStartup
{
 readonly string root;readonly LocalErasureJournal journal;
 public LocalAccountStartup(string directory):this(directory,new LocalErasureJournal(directory)){}
 internal LocalAccountStartup(string directory,LocalErasureJournal trusted){root=LocalDataFiles.Root(directory);journal=trusted;}
 public async Task Resume(Guid active,Func<WindowsHelloChallenge,Task> removeHello,CancellationToken ct=default){var spaces=new AccountWorkspaces(root);var state=new AccountSyncStore(spaces.DirectoryFor(active)).Load();if(LocalAccountInventory.HasCanonicalIdentity(state)){using var binding=new AccountUnlockStore(spaces.DirectoryFor(active)).Read(state.Owner);}var notices=journal.Notices();foreach(var notice in notices){try{await new LocalAccountErasure(root,journal).Run(notice.Owner,removeHello,ct);}catch(Exception error)when(LocalAccountInventory.HasCanonicalIdentity(state)&&state.Owner!=notice.Owner&&error is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException){/* Another explicitly bound account may open; the failed owner's journal remains pending. */}}journal.GuardWorkspace(new AccountWorkspaces(root).Load().Active);}
}
