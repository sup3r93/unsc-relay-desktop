using System.Net;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed record SharedArchive(Guid Id,string Name,string OwnerAccount,string ArchiveOwner,string Access,long Version,string OwnerName="",bool Offline=false,bool Personal=false,bool SharingSuspended=false,DateTimeOffset? AccessExpiresAt=null,bool AccessReviewDue=false);
public sealed record SharedArchiveDevice(Guid Id,string Publickey,string Fingerprint);
public sealed record OwnArchiveDevice(Guid Id,string Publickey,DateTimeOffset? Revoked);
public sealed record SharedArchiveMember(string Account,string Email,string Access,DateTimeOffset? ExpiresAt=null);
public sealed record SharedArchiveTransfer(Guid Id,string Proof,DateTimeOffset ExpiresAt);
public sealed partial class CloudAccountClient
{
 static string ArchivePath(Guid archive){if(archive==Guid.Empty)throw new ArgumentException("Archivio non valido.");return "api/archives/"+archive.ToString("D");}
 async Task<T> ArchiveRequest<T>(HttpMethod method,string path,object? value,CancellationToken ct)
 {
  using var response=await Send(method,path,value,ct);
  if(!response.IsSuccessStatusCode){string message="Operazione sull’archivio non riuscita.";try{using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));if(body.RootElement.TryGetProperty("error",out var e)&&e.GetString() is {Length:>0 and <1000} text)message=text;}catch(JsonException){ /* Keep the safe generic message when the server response is not JSON. */ }throw new OnlineAccessException(message,response.StatusCode);}
  if(typeof(T)==typeof(bool))return (T)(object)true;
  return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(ct),VaultSyncCrypto.Json)??throw new InvalidDataException("Risposta archivio non valida.");
 }
 public Task<bool> RequestArchiveAccess(Guid id,CancellationToken ct){if(id==Guid.Empty)throw new ArgumentException("Archivio non valido.");return ArchiveRequest<bool>(HttpMethod.Post,"api/archive-access-requests",new{archiveId=id},ct);}
 public Task<JsonElement> ArchiveAccessRequests(Guid id,CancellationToken ct)=>ArchiveRequest<JsonElement>(HttpMethod.Get,ArchivePath(id)+"/access-requests",null,ct);
 public Task<JsonElement> ApproveArchiveAccessRequest(Guid id,Guid request,string access,DateTimeOffset? expiresAt,CancellationToken ct)=>ArchiveRequest<JsonElement>(HttpMethod.Post,ArchivePath(id)+"/access-requests/"+request.ToString("D")+"/approve",new{access,expiresAt},ct);
 public Task<bool> DismissArchiveAccessRequest(Guid id,Guid request,CancellationToken ct)=>ArchiveRequest<bool>(HttpMethod.Delete,ArchivePath(id)+"/access-requests/"+request.ToString("D"),null,ct);
 public Task<List<OwnArchiveDevice>> OwnArchiveDevices(CancellationToken ct)=>ArchiveRequest<List<OwnArchiveDevice>>(HttpMethod.Get,"api/archive-devices",null,ct);
 public Task<JsonElement> ArchiveAccessReview(Guid id,CancellationToken ct)=>ArchiveRequest<JsonElement>(HttpMethod.Get,ArchivePath(id)+"/access-review",null,ct);
 public Task<bool> AcknowledgeArchiveReview(Guid id,int intervalDays,string intent,CancellationToken ct)=>ArchiveRequest<bool>(HttpMethod.Post,ArchivePath(id)+"/access-review",new{intervalDays,intent},ct);
 public Task<List<SharedArchive>> Archives(CancellationToken ct)=>ArchiveRequest<List<SharedArchive>>(HttpMethod.Get,"api/archives",null,ct);
 public async Task<SharedArchive> PersonalArchive(CancellationToken ct)
 {
  var archives=await Archives(ct);var personal=archives.Where(a=>a.Personal&&a.Access=="owner"&&a.OwnerAccount==State.Owner).ToArray();
  if(personal.Length!=1)throw new OnlineAccessException("Archivio personale sincronizzato non identificabile. Nessun dato locale è stato modificato.");
  return personal[0];
 }
 public Task<SharedArchive> CreateArchive(string name,CancellationToken ct)=>ArchiveRequest<SharedArchive>(HttpMethod.Post,"api/archives",new{name},ct);
 public Task<bool> RenameArchive(Guid id,string name,CancellationToken ct)=>ArchiveRequest<bool>(HttpMethod.Put,ArchivePath(id),new{name},ct);
 public Task<JsonElement> ArchiveStorageUsage(Guid id,CancellationToken ct)=>ArchiveRequest<JsonElement>(HttpMethod.Get,ArchivePath(id)+"/storage",null,ct);
 public Task<JsonElement> ArchiveActivity(Guid id,int page,CancellationToken ct){if(page is <0 or >10000)throw new ArgumentException("Pagina non valida.");return ArchiveRequest<JsonElement>(HttpMethod.Get,ArchivePath(id)+"/activity?page="+page.ToString(System.Globalization.CultureInfo.InvariantCulture),null,ct);}
 public Task<List<SharedArchiveMember>> ArchiveMembers(Guid id,CancellationToken ct)=>ArchiveRequest<List<SharedArchiveMember>>(HttpMethod.Get,ArchivePath(id)+"/members",null,ct);
 public Task<JsonElement> ShareArchive(Guid id,string email,string access,CancellationToken ct,DateTimeOffset? expiresAt=null)=>ArchiveRequest<JsonElement>(HttpMethod.Post,ArchivePath(id)+"/members",new{email,access,expiresAt},ct);
 public Task<bool> RemoveArchiveMember(Guid id,string account,CancellationToken ct){if(account is not{Length:64}||!account.All(char.IsAsciiHexDigit))throw new ArgumentException("Destinatario non valido.");return ArchiveRequest<bool>(HttpMethod.Delete,ArchivePath(id)+"/members/"+account,null,ct);}
 public Task<List<SharedArchiveDevice>> ArchiveDevices(Guid id,string account,CancellationToken ct){if(account is not{Length:64}||!account.All(char.IsAsciiHexDigit))throw new ArgumentException("Destinatario non valido.");return ArchiveRequest<List<SharedArchiveDevice>>(HttpMethod.Get,ArchivePath(id)+"/devices/"+account,null,ct);}
 public Task<bool> RegisterArchiveDevice(ArchiveDeviceKey device,CancellationToken ct){if(device.Account!=State.Owner)throw new OnlineAccessException("Il dispositivo appartiene a un altro account.");return ArchiveRequest<bool>(HttpMethod.Post,"api/archive-devices",new{deviceId=device.Id,publicKey=device.PublicKey,label="UNSC Relay · Windows"},ct);}
 public Task<bool> GrantArchiveKey(Guid id,ArchiveKeyGrant grant,CancellationToken ct){if(grant.ArchiveId!=id)throw new ArgumentException("Archivio non valido.");return ArchiveRequest<bool>(HttpMethod.Put,ArchivePath(id)+"/keys",new{deviceId=grant.DeviceId,accountOwner=grant.AccountOwner,wrappedKey=grant.WrappedKey},ct);}
 public Task<ArchiveKeyGrant> ArchiveGrant(Guid id,Guid device,CancellationToken ct){if(device==Guid.Empty)throw new ArgumentException("Dispositivo non valido.");return ArchiveRequest<ArchiveKeyGrant>(HttpMethod.Get,ArchivePath(id)+"/keys/"+device.ToString("D"),null,ct);}
 public Task<SharedArchiveTransfer> BeginArchiveTransfer(Guid id,string email,bool keepAccess,CancellationToken ct)=>ArchiveRequest<SharedArchiveTransfer>(HttpMethod.Post,ArchivePath(id)+"/transfer",new{email,keepAccess},ct);
 public Task<bool> CompleteArchiveTransfer(Guid id,SharedArchiveTransfer challenge,CancellationToken ct)=>ArchiveRequest<bool>(HttpMethod.Post,ArchivePath(id)+"/transfer/"+challenge.Id.ToString("D"),new{proof=challenge.Proof},ct);
 public async Task<RemoteVault?> ReadArchive(SharedArchive archive,CancellationToken ct)
 {
  using var response=await Send(HttpMethod.Get,ArchivePath(archive.Id)+"/vault",null,ct);if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();
  var remote=JsonSerializer.Deserialize<RemoteVault>(await response.Content.ReadAsStringAsync(ct),VaultSyncCrypto.Json)??throw new InvalidDataException("Archivio non valido.");if(remote.Revision<1)throw new InvalidDataException("Revisione non valida.");VaultSyncCrypto.Validate(remote.Envelope,archive.ArchiveOwner);return remote;
 }
 public async Task<long?> WriteArchive(SharedArchive archive,SyncEnvelope envelope,long revision,CancellationToken ct)
 {
  if(archive.Access=="read")throw new OnlineAccessException("L’archivio è in sola lettura.");VaultSyncCrypto.Validate(envelope,archive.ArchiveOwner);
  using var response=await Send(HttpMethod.Put,ArchivePath(archive.Id)+"/vault",envelope,ct,revision);if(response.StatusCode==HttpStatusCode.Conflict)return null;response.EnsureSuccessStatusCode();using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return body.RootElement.GetProperty("revision").GetInt64();
 }
}
