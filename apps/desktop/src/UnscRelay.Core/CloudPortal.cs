using System.Net.Http.Json;
using System.Text.Json;

namespace UnscRelay.Core;

public sealed partial class CloudAccountClient
{
 public Task<JsonElement> NotificationSummary(CancellationToken ct)=>PortalRead("api/notifications/summary",ct);
 public Task<JsonElement> ReadNotification(Guid id,CancellationToken ct)=>PortalWrite("api/notifications/"+id.ToString("D")+"/read",new{},ct);
 public Task<JsonElement> ReadAllNotifications(CancellationToken ct)=>PortalWrite("api/notifications/read-all",new{},ct);
 public Task<JsonElement> WorkspaceSettings(CancellationToken ct)=>PortalRead("api/workspace-settings",ct);
 public async Task<JsonElement> NotificationDisplay(JsonElement value,long version,CancellationToken ct){using var response=await Send(HttpMethod.Put,"api/workspace-settings",new{key="notificationDisplay",value,version},ct);return await PortalResult(response,ct);}
 public Task<JsonElement> StorageAttachments(int page,CancellationToken ct){if(page is <0 or >10000)throw new ArgumentException("Pagina non valida.");return PortalRead("api/storage-attachments?page="+page.ToString(System.Globalization.CultureInfo.InvariantCulture),ct);}
 public async Task<JsonElement> DeleteStorageAttachment(Guid attachment,CancellationToken ct){if(attachment==Guid.Empty)throw new ArgumentException("Allegato non valido.");using var response=await Send(HttpMethod.Delete,"api/storage-attachments/"+attachment.ToString("D"),null,ct);return await PortalResult(response,ct);}
 public Task<JsonElement> StorageUsage(CancellationToken ct)=>PortalRead("api/storage-usage",ct);
 public Task<JsonElement> HomeNotifications(CancellationToken ct)=>PortalRead("api/notifications",ct);
 public Task<JsonElement> SecurityMethods(CancellationToken ct)=>PortalRead("api/security-methods",ct);
 public Task<JsonElement> Tickets(CancellationToken ct,string search=""){if(search.Length>160||search.Any(char.IsControl))throw new ArgumentException("Ricerca ticket non valida.");return PortalRead("api/tickets?page=0&search="+Uri.EscapeDataString(search)+"&status=",ct);}
 public Task<JsonElement> TicketLinks(Guid ticket,CancellationToken ct)=>PortalRead("api/tickets/"+ticket.ToString("D")+"/links",ct);
 public Task<JsonElement> LinkTickets(Guid ticket,Guid related,string kind,CancellationToken ct)=>PortalWrite("api/tickets/"+ticket.ToString("D")+"/links",new{relatedId=related,kind},ct);
 public async Task<JsonElement> UnlinkTickets(Guid ticket,Guid link,CancellationToken ct){using var response=await Send(HttpMethod.Delete,"api/tickets/"+ticket.ToString("D")+"/links/"+link.ToString("D"),null,ct);return await PortalResult(response,ct);}

 public Task<JsonElement> Ticket(Guid id,CancellationToken ct)=>PortalRead("api/tickets/"+id.ToString("D"),ct);
 public Task<JsonElement> CreateTicket(string title,string body,string category,string priority,CancellationToken ct)=>PortalWrite("api/tickets",new{title,body,category,priority},ct);
 public Task<JsonElement> ReplyTicket(Guid id,string body,long version,CancellationToken ct)=>PortalWrite("api/tickets/"+id.ToString("D")+"/reply",new{body,version},ct);
 async Task<JsonElement> PortalRead(string path,CancellationToken ct)
 {
  using var response=await Send(HttpMethod.Get,path,null,ct);return await PortalResult(response,ct);
 }
 async Task<JsonElement> PortalWrite(string path,object body,CancellationToken ct)
 {
  using var response=await Send(HttpMethod.Post,path,body,ct);return await PortalResult(response,ct);
 }
 static async Task<JsonElement> PortalResult(HttpResponseMessage response,CancellationToken ct)
 {
  if(!response.IsSuccessStatusCode){string message="Operazione online non completata. Controlla la connessione e aggiorna l’elenco prima di riprovare.";try{using var error=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));if(error.RootElement.TryGetProperty("error",out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString() is {Length:>0 and <1000} safe)message=safe;}catch(JsonException){}throw new OnlineAccessException(message,response.StatusCode);}
  var text=await response.Content.ReadAsStringAsync(ct);
  if(string.IsNullOrWhiteSpace(text))return JsonSerializer.SerializeToElement(new{});
  using var document=JsonDocument.Parse(text);return document.RootElement.Clone();
 }
}
