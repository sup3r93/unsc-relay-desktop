using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 readonly Dictionary<string,(string Session,CancellationTokenSource Stop)> remoteSearches=[];
 async Task<object?> RemoteSearchAction(string action,JsonElement p){
  var request=p.GetProperty("requestId").GetGuid().ToString("N");var sessionId=S(p,"sessionId");
  if(action=="remoteSearchCancel"){if(remoteSearches.TryGetValue(request,out var running)&&running.Session==sessionId){running.Stop.Cancel();return true;}return false;}
  if(remoteSearches.Count>=2||remoteSearches.ContainsKey(request))throw new InvalidOperationException("Attendi o annulla la ricerca in corso.");var generation=authenticationGeneration;RequireOperationSession(generation);var session=Session(p);var query=p.GetProperty("query").Deserialize<RemoteSearchQuery>(ProfileStore.Json)??throw new ArgumentException("Filtri della ricerca remota non validi.");query.Validate();using var stop=CancellationTokenSource.CreateLinkedTokenSource(accountOperationStop.Token);stop.CancelAfter(TimeSpan.FromSeconds(30));remoteSearches.Add(request,(sessionId,stop));try{var result=await session.SearchFilesAsync(query,stop.Token);RequireOperationSession(generation);return result;}finally{remoteSearches.Remove(request);}
 }
}
