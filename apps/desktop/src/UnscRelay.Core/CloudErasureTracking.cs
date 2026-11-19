using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed class ErasureVerificationPendingException(Exception? cause=null):OnlineAccessException("La scadenza della cancellazione è trascorsa. Serve una verifica online prima di aprire i dati; nessun file è stato eliminato."){public Exception? Cause{get;}=cause;}
public sealed class AccountErasedOnlineException(VerifiedAccountErasure verified):OnlineAccessException("Questo account è stato cancellato.",HttpStatusCode.Gone){public VerifiedAccountErasure Verified{get;}=verified;}
public sealed record VerifiedAccountErasure(ErasureReceipt Receipt,ErasureReceiptClaims Claims);
public sealed partial class CloudAccountClient
{
 public event Action<VerifiedAccountErasure>? ErasureVerified;int erasureRevoked;
 public bool ErasureRevoked=>Volatile.Read(ref erasureRevoked)!=0;
 internal async Task ThrowIfErased(HttpResponseMessage response,CancellationToken ct,ErasureReceiptVerifier? trusted=null,DateTimeOffset? verifiedAt=null){
  if(response.StatusCode!=HttpStatusCode.Gone)return;
  if(State.Owner is not{Length:64}||State.Subject.Length==0)throw new OnlineAccessException("Accesso revocato. Nessun dato locale è stato eliminato.",HttpStatusCode.Gone);
  await using var stream=await response.Content.ReadAsStreamAsync(ct);var buffer=new byte[16385];var total=0;while(total<buffer.Length){var count=await stream.ReadAsync(buffer.AsMemory(total),ct);if(count==0)break;total+=count;}if(total>16384)throw new InvalidDataException("Ricevuta di cancellazione troppo grande.");
  using var document=JsonDocument.Parse(buffer.AsMemory(0,total));if(!document.RootElement.TryGetProperty("receipt",out var proof)||proof.ValueKind!=JsonValueKind.Object)throw new OnlineAccessException("Accesso revocato. La ricevuta di cancellazione non è disponibile; nessun dato locale è stato eliminato.",HttpStatusCode.Gone);
  var receipt=proof.Deserialize<ErasureReceipt>(VaultSyncCrypto.Json)??throw new InvalidDataException("Ricevuta di cancellazione non valida.");var claims=(trusted??new ErasureReceiptVerifier()).Verify(receipt,State.Owner,State.Subject,verifiedAt??DateTimeOffset.UtcNow,State.ErasureRequestId==Guid.Empty?null:State.ErasureRequestId);var verified=new VerifiedAccountErasure(receipt,claims);SignalErasure(verified);throw new AccountErasedOnlineException(verified);
 }
 static string ErasureCapabilityHash(string value){var bytes=Convert.FromBase64String(value.Replace('-','+').Replace('_','/')+"=");try{if(bytes.Length!=32)throw new InvalidDataException("Verifica del dispositivo non valida.");return Convert.ToHexString(SHA256.HashData(bytes));}finally{CryptographicOperations.ZeroMemory(bytes);}}
 void SignalErasure(VerifiedAccountErasure verified){if(Interlocked.Exchange(ref erasureRevoked,1)==0)ErasureVerified?.Invoke(verified);}
 async Task ProbePriorErasure(CancellationToken ct){if(State.ErasureExecuteAfter>DateTimeOffset.UtcNow)return;VerifiedAccountErasure? receipt;try{receipt=await CheckAccountErasure(ct);}catch(Exception error)when(error is System.Net.Http.HttpRequestException or OnlineAccessException){return;}if(receipt!=null){SignalErasure(receipt);throw new AccountErasedOnlineException(receipt);}}
 public async Task<VerifiedAccountErasure?> CheckErasureAccess(CancellationToken ct){
  VerifiedAccountErasure? verified=null;Exception? lookupFailure=null;try{verified=await CheckAccountErasure(ct);}catch(Exception error)when(error is System.Net.Http.HttpRequestException or OperationCanceledException or OnlineAccessException){lookupFailure=error;}if(verified!=null)return verified;
  if(State.ErasureExecuteAfter<=DateTimeOffset.UtcNow){try{await TrackErasureStatus(await AccountDeletionStatus(ct),ct);}catch(AccountErasedOnlineException erased){return erased.Verified;}catch(Exception error)when(error is System.Net.Http.HttpRequestException or OperationCanceledException or OnlineAccessException or JsonException or InvalidDataException){throw new ErasureVerificationPendingException(error);}if(State.ErasureExecuteAfter<=DateTimeOffset.UtcNow)throw new ErasureVerificationPendingException(lookupFailure);}
  return null;
 }
 public async Task RefreshErasureTracking(CancellationToken ct)
 {
  await ProbePriorErasure(ct);var result=await AccountDeletionStatus(ct);await TrackErasureStatus(result,ct);
 }
 public async Task TrackErasureStatusPublic(JsonElement result,CancellationToken ct){await ProbePriorErasure(ct);await TrackErasureStatus(result,ct);}
 internal async Task TrackErasureStatus(JsonElement result,CancellationToken ct)
 {
  var status=result.GetProperty("status");var state=status.GetProperty("state").GetString();
  if(state is "none" or "cancelled"){if(State.ErasureRequestId!=Guid.Empty){State.ErasureRequestId=Guid.Empty;State.ErasureExecuteAfter=null;State.ErasureCapability="";State.ErasureCapabilityRegistered=false;Save();}return;}
  if(state!="pending")return;
  if(!State.Activated||State.Owner is not{Length:64}||!Guid.TryParseExact(State.Subject,"D",out _))throw new InvalidDataException("Identità del dispositivo non valida.");
  var requestId=status.GetProperty("requestId").GetGuid();var deadline=status.GetProperty("executeAfter").GetDateTimeOffset();var requested=status.GetProperty("requestedAt").GetDateTimeOffset();
  if(requestId==Guid.Empty||status.GetProperty("graceDays").GetInt32()!=30||deadline-requested!=TimeSpan.FromDays(30))throw new InvalidDataException("Periodo di cancellazione non valido.");
  if(State.ErasureRequestId!=requestId){State.ErasureRequestId=requestId;State.ErasureExecuteAfter=deadline;var random=RandomNumberGenerator.GetBytes(32);try{State.ErasureCapability=Convert.ToBase64String(random).TrimEnd('=').Replace('+','-').Replace('/','_');}finally{CryptographicOperations.ZeroMemory(random);}State.ErasureCapabilityRegistered=false;Save();}
  if(State.ErasureExecuteAfter!=deadline||State.ErasureCapability.Length!=43)throw new InvalidDataException("La richiesta di cancellazione del dispositivo è cambiata.");
  if(!State.ErasureCapabilityRegistered){await PortalWrite("api/account/deletion/receipt-capability",new{requestId=State.ErasureRequestId,hash=ErasureCapabilityHash(State.ErasureCapability)},ct);State.ErasureCapabilityRegistered=true;Save();}
 }
 public Task<VerifiedAccountErasure?> CheckAccountErasure(CancellationToken ct)=>State.ErasureExecuteAfter>DateTimeOffset.UtcNow?Task.FromResult<VerifiedAccountErasure?>(null):PollErasureReceipt(ct,new ErasureReceiptVerifier());
 internal async Task<VerifiedAccountErasure?> PollErasureReceipt(CancellationToken ct,ErasureReceiptVerifier verifier)
 {
  if(State.ErasureRequestId==Guid.Empty||State.ErasureCapability.Length==0)return null;
  if(State.Owner is not{Length:64}||!Guid.TryParseExact(State.Subject,"D",out _)||State.ErasureCapability.Length!=43)throw new InvalidDataException("Verifica del dispositivo non valida.");
  using var request=new HttpRequestMessage(HttpMethod.Post,"account-erasure/receipt"){Content=JsonContent.Create(new{owner=State.Owner,subject=State.Subject,requestId=State.ErasureRequestId,capability=State.ErasureCapability},options:VaultSyncCrypto.Json)};
  // This recovery channel intentionally sends no access token or cookies. It remains
  // usable after Keycloak revocation and never follows redirects to another origin.
  using var response=await erasureReceiptHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);if(response.StatusCode==HttpStatusCode.NoContent)return null;
  if(response.StatusCode!=HttpStatusCode.OK)throw new OnlineAccessException("La verifica della cancellazione non è disponibile. Nessun dato locale è stato eliminato.",response.StatusCode);
  if(response.Content.Headers.ContentLength>16384)throw new InvalidDataException("Ricevuta di cancellazione troppo grande.");await using var stream=await response.Content.ReadAsStreamAsync(ct);var buffer=new byte[16385];var total=0;while(total<buffer.Length){var count=await stream.ReadAsync(buffer.AsMemory(total),ct);if(count==0)break;total+=count;}if(total>16384)throw new InvalidDataException("Ricevuta di cancellazione troppo grande.");
  using var document=JsonDocument.Parse(buffer.AsMemory(0,total));var receipt=document.RootElement.GetProperty("receipt").Deserialize<ErasureReceipt>(VaultSyncCrypto.Json)??throw new InvalidDataException("Ricevuta di cancellazione assente.");var claims=verifier.Verify(receipt,State.Owner,State.Subject,DateTimeOffset.UtcNow,State.ErasureRequestId);if(State.ErasureExecuteAfter?.ToUnixTimeMilliseconds()!=claims.ExecuteAfter)throw new InvalidDataException("La scadenza della ricevuta non corrisponde alla richiesta salvata.");return new(receipt,claims);
 }
}
