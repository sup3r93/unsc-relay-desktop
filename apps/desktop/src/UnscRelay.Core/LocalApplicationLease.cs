using System.IO;
namespace UnscRelay.Core;
/// <summary>One desktop host per data root; detached session windows remain in that host.</summary>
public sealed class LocalApplicationLease:IDisposable
{
 readonly FileStream held;
 public LocalApplicationLease(string directory){var root=LocalDataFiles.Root(directory);Directory.CreateDirectory(root);var path=LocalDataFiles.Child(root,"relay-session.lock");FileStream? opened=null;try{opened=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);LocalDataFiles.RequireOpenedPath(opened,path);held=opened;}catch(IOException error){opened?.Dispose();throw new IOException("Relay è già aperto per questa cartella dati, oppure la cartella non è disponibile. Usa la finestra già aperta.",error);}}
 public void Dispose()=>held.Dispose();
}
