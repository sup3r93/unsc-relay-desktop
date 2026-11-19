using System.Net.Http;
using System.Security.Cryptography;
namespace UnscRelay.Core;
public sealed record TicketAttachmentPreview(string Name,string MediaType,string Data);
public sealed partial class CloudAccountClient
{
 public async Task<TicketAttachmentPreview> TicketAttachment(Guid ticket,Guid attachment,CancellationToken ct)
 {
  if(ticket==Guid.Empty||attachment==Guid.Empty)throw new ArgumentException("Allegato non valido.");using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(30));ct=deadline.Token;
  using var response=await Send(HttpMethod.Get,"api/tickets/"+ticket.ToString("D")+"/attachments/"+attachment.ToString("D"),null,ct,completion:HttpCompletionOption.ResponseHeadersRead);if(!response.IsSuccessStatusCode)throw new OnlineAccessException("Allegato non disponibile. Aggiorna il ticket e verifica l’accesso.",response.StatusCode);
  var media=response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant()??"";if(media is not("image/png" or "image/jpeg" or "image/webp" or "text/plain" or "application/json"))throw new InvalidDataException("Formato non supportato per l’anteprima protetta.");if(response.Content.Headers.ContentLength is >2097152)throw new InvalidDataException("Allegato massimo 2 MiB.");
  var rawName=response.Content.Headers.ContentDisposition?.FileNameStar??response.Content.Headers.ContentDisposition?.FileName?.Trim('"')??"attachment";var name=Path.GetFileName(rawName);if(name.Length is 0 or >200||name.Any(char.IsControl)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)name="attachment";
  await using var input=await response.Content.ReadAsStreamAsync(ct);using var memory=new MemoryStream();var buffer=new byte[65536];try{while(true){var read=await input.ReadAsync(buffer,ct);if(read==0)break;if(memory.Length+read>2097152)throw new InvalidDataException("Allegato massimo 2 MiB.");await memory.WriteAsync(buffer.AsMemory(0,read),ct);}return new(name,media,Convert.ToBase64String(memory.GetBuffer(),0,(int)memory.Length));}finally{CryptographicOperations.ZeroMemory(buffer);if(memory.TryGetBuffer(out var contents))CryptographicOperations.ZeroMemory(contents.AsSpan());}
 }
}
