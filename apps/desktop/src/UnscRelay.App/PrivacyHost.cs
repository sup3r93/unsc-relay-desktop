using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 async Task<object?> PrivacyAction(string action,JsonElement p){
  var generation=authenticationGeneration;RequireOperationSession(generation);if(action=="privacyInventory"){var privacyRoot=dataPath;var inventory=await Task.Run(()=>PrivacyData.Inventory(privacyRoot));RequireOperationSession(generation);return inventory;}
  if(action=="privacyCacheClear"){if(!Confirm(NativeText("Svuotare la cache dell’interfaccia? Account e archivi restano conservati.")))return false;RequireOperationSession(generation);await Browser.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache);RequireOperationSession(generation);return true;}
  if(QueueBusy||EditorsBusy||editBuffers.Count>0||activeOperations>1||remoteSearches.Count>0||folderScans.Count>0||cloudUiBusy||PlainExportBusy||AccountErasureBusy)throw new InvalidOperationException("Chiudi gli editor e completa le operazioni prima della pulizia.");var category=S(p,"category");return PrivacyData.Cleanup(dataPath,category,summary=>{RequireOperationSession(generation);var label=NativeText(category=="editor"?"Copie degli editor":"File temporanei gestiti");var approved=Confirm(label+": "+summary.Files+" · "+summary.Bytes+" B\n"+NativeText("Rimuovere questi file senza Cestino? Le copie degli editor possono contenere modifiche non caricate. Archivi e registrazioni restano conservati."));RequireOperationSession(generation);return approved;});
 }
}
