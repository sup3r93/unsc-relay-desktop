using System.Net;
using System.Net.Http.Json;
using UnscRelay.Core;

internal static class PortalTransportChecks
{
 sealed class PortalServer:HttpMessageHandler
 {
  public List<string> Queries=[];public List<string> Paths=[];public List<HttpMethod> Methods=[];public bool Failed;public string AttachmentMedia="text/plain";public byte[] AttachmentBytes=System.Text.Encoding.UTF8.GetBytes("<script>inert fixture</script>");public bool UnknownLength;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   if(request.RequestUri!.Scheme!="https"||request.RequestUri.Host!="unsc.responda.eu"||request.Headers.Authorization?.Parameter!="fixture-access")throw new Exception("Incorrect account transport");
   Queries.Add(request.RequestUri.Query);Paths.Add(request.RequestUri.AbsolutePath);Methods.Add(request.Method);
   if(Failed)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
   if(request.RequestUri.AbsolutePath.Contains("/attachments/",StringComparison.Ordinal)){HttpContent content=UnknownLength?new StreamingFixtureContent(AttachmentBytes):new ByteArrayContent(AttachmentBytes);content.Headers.ContentType=new(AttachmentMedia);content.Headers.ContentDisposition=new("attachment"){FileName="fixture.txt"};return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});}
   if(request.RequestUri.AbsolutePath.EndsWith("/reply",StringComparison.Ordinal))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("")});
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{accepted=true})});
  }
 }
 sealed class StreamingFixtureContent(byte[] bytes):HttpContent
 {
  protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>stream.WriteAsync(bytes).AsTask();
  protected override bool TryComputeLength(out long length){length=0;return false;}
  protected override Task<Stream> CreateContentReadStreamAsync()=>Task.FromResult<Stream>(new MemoryStream(bytes,false));
 }
 public static async Task<int> Run()
 {
  var root=Directory.CreateTempSubdirectory("relay-portal-transport-");var count=0;
  void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS portal: "+label);count++;}
  try{
   var server=new PortalServer();using var client=new CloudAccountClient(root.FullName,server);
   try{await client.Tickets(CancellationToken.None);throw new Exception("Disconnected request accepted");}catch(OnlineAccessException){Check(server.Paths.Count==0,"disconnected account never sends a request");}
   client.State.Activated=true;client.State.AccessToken="fixture-access";client.State.TokenExpires=DateTimeOffset.UtcNow.AddMinutes(5);
   await client.SecurityMethods(CancellationToken.None);Check(server.Paths.Single()=="/api/security-methods","registered factors use the active account bearer and fixed HTTPS endpoint");
   await client.HomeNotifications(CancellationToken.None);Check(server.Paths.Last()=="/api/notifications","home notifications remain on selected-account authenticated fixed endpoint");
   await client.StorageUsage(CancellationToken.None);Check(server.Paths.Last()=="/api/storage-usage","storage usage remains selected-account authenticated metadata");
   await client.StorageAttachments(2,CancellationToken.None);Check(server.Paths.Last()=="/api/storage-attachments"&&server.Queries.Last()=="?page=2"&&server.Methods.Last()==HttpMethod.Get,"attachment explorer pages metadata on the selected-account authenticated endpoint");var beforeInvalidPage=server.Paths.Count;try{await client.StorageAttachments(-1,CancellationToken.None);throw new Exception("Invalid page accepted");}catch(ArgumentException){Check(server.Paths.Count==beforeInvalidPage,"invalid storage page is refused before network access");}
   var deletingAttachment=Guid.NewGuid();await client.DeleteStorageAttachment(deletingAttachment,CancellationToken.None);Check(server.Paths.Last()=="/api/storage-attachments/"+deletingAttachment.ToString("D")&&server.Methods.Last()==HttpMethod.Delete,"attachment deletion uses a fixed authenticated DELETE endpoint and typed identifier");
   var beforeInvalidDeletion=server.Paths.Count;try{await client.DeleteStorageAttachment(Guid.Empty,CancellationToken.None);throw new Exception("empty deletion identifier accepted");}catch(ArgumentException){Check(server.Paths.Count==beforeInvalidDeletion,"empty attachment deletion refused before network access");}
   await client.NotificationSummary(CancellationToken.None);Check(server.Paths.Last()=="/api/notifications/summary"&&server.Methods.Last()==HttpMethod.Get,"notification counts use active-account authenticated endpoint");
   var notification=Guid.NewGuid();await client.ReadNotification(notification,CancellationToken.None);Check(server.Paths.Last()=="/api/notifications/"+notification.ToString("D")+"/read"&&server.Methods.Last()==HttpMethod.Post,"mark read uses a typed identifier and explicit POST");
   await client.ReadAllNotifications(CancellationToken.None);Check(server.Paths.Last()=="/api/notifications/read-all"&&server.Methods.Last()==HttpMethod.Post,"bulk read applies through the authenticated account transport");
   await client.WorkspaceSettings(CancellationToken.None);Check(server.Paths.Last()=="/api/workspace-settings"&&server.Methods.Last()==HttpMethod.Get,"display preferences use selected-account workspace metadata");
   await client.NotificationDisplay(System.Text.Json.JsonSerializer.SerializeToElement(new{categories=new[]{"support"},quiet=true,summaries=true}),4,CancellationToken.None);Check(server.Paths.Last()=="/api/workspace-settings"&&server.Methods.Last()==HttpMethod.Put,"notification preference writes use the fixed PUT endpoint");
   var id=Guid.NewGuid();await client.Ticket(id,CancellationToken.None);Check(server.Paths.Last()=="/api/tickets/"+id.ToString("D"),"ticket lookup accepts only a typed identifier");
   var result=await client.ReplyTicket(id,"Synthetic reply",1,CancellationToken.None);Check(result.ValueKind==System.Text.Json.JsonValueKind.Object,"successful empty reply response is accepted without a false error");
   var attachment=Guid.NewGuid();var preview=await client.TicketAttachment(id,attachment,CancellationToken.None);Check(preview.MediaType=="text/plain"&&System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(preview.Data))=="<script>inert fixture</script>"&&server.Paths.Last()=="/api/tickets/"+id.ToString("D")+"/attachments/"+attachment.ToString("D"),"bounded attachment read uses selected-account bearer and typed ticket/attachment identifiers");
   server.AttachmentMedia="text/html";try{await client.TicketAttachment(id,attachment,CancellationToken.None);throw new Exception("Active media accepted");}catch(InvalidDataException){Check(true,"HTML attachments are refused before preview");}server.AttachmentMedia="text/plain";server.AttachmentBytes=new byte[2097153];try{await client.TicketAttachment(id,attachment,CancellationToken.None);throw new Exception("Oversized attachment accepted");}catch(InvalidDataException){Check(true,"declared oversized attachment is refused");}server.UnknownLength=true;try{await client.TicketAttachment(id,attachment,CancellationToken.None);throw new Exception("Unbounded stream accepted");}catch(InvalidDataException){Check(true,"undeclared oversized stream is bounded while reading");}server.UnknownLength=false;server.AttachmentBytes=[];
   server.Failed=true;var before=server.Paths.Count;try{await client.CreateTicket("Synthetic","Test only","technical","normal",CancellationToken.None);throw new Exception("Failed request accepted");}catch(OnlineAccessException){Check(server.Paths.Count==before+1,"failed report is not retried automatically");}
   return 0;
  }finally{root.Delete(true);}
 }
}
