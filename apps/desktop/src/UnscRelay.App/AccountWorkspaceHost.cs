using System.Diagnostics;
using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 async Task<object?> WorkspaceAccountAction(string action,JsonElement p)
 {
  var spaces=new AccountWorkspaces(rootDataPath);var index=spaces.Load();
  if(action=="workspaceList")return index.Accounts.Select(a=>{
   var state=new AccountSyncStore(spaces.DirectoryFor(a.Id)).Load();return new{a.Id,a.Email,a.Name,a.Alias,a.Color,current=a.Id==index.Active,connected=state.Activated&&(state.AccessToken.Length>0||state.RefreshToken.Length>0)&&state.Owner==a.Owner};
  }).ToArray();
  if(!await cloudGate.WaitAsync(0))throw new InvalidOperationException("Attendi la sincronizzazione in corso.");
  try
  {
   if(action=="workspaceCustomize"){spaces.Customize(p.GetProperty("id").GetGuid(),S(p,"alias"),S(p,"color"));return true;}
   if(action=="workspaceForget"){var id=p.GetProperty("id").GetGuid();var saved=new AccountSyncStore(spaces.DirectoryFor(id)).Load();if(saved.Activated)throw new InvalidOperationException("Disconnetti prima questo account dall’app.");spaces.Forget(id);return true;}
   if(smokeTest)throw new InvalidOperationException("Il collaudo isolato non riavvia l’app su dati personali.");
   if(startupSequencePlaying||activeOperations>1||transferActive||QueueBusy||EditorsBusy||ExportBusy||helloBusy||certificateRenewBusy||conversionOperation||keyGenerationBusy||connectingSessions.Count>0||accessBusy)throw new InvalidOperationException("Completa o interrompi le operazioni in corso prima di cambiare account.");
   if(!Confirm("Per cambiare account Relay si riavvia e chiude le sessioni remote e locali. I dati di ciascun account restano separati e cifrati. "+PendingEditorWarning()+" Continuare?"))return false;
   var target=action=="workspaceAdd"?spaces.Add(S(p,"email")):p.GetProperty("id").GetGuid();
   if(target==index.Active)return true;
   store.Save(data);spaces.Select(target);
   try
   {
    var executable=Environment.ProcessPath??throw new IOException("Percorso applicazione non disponibile.");var start=new ProcessStartInfo(executable){UseShellExecute=false};start.ArgumentList.Add("--account-switch-wait");start.ArgumentList.Add(Environment.ProcessId.ToString());start.Environment.Remove("RELAY_ASKPASS_HELPER");start.Environment.Remove("RELAY_NATIVE_PIPE");
    using var process=Process.Start(start)??throw new IOException("Riavvio non riuscito.");
    accountLoggingOut=true;updateClosing=true;Close();return true;
   }
   catch{spaces.Select(index.Active);throw;}
  }
  finally{cloudGate.Release();}
 }
}
