using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
internal static class ArchiveClientChecks
{
 sealed class Server(SharedArchive archive,SyncEnvelope initial):HttpMessageHandler
 {
  public bool Offline,Revoked;public HttpStatusCode? Failure;public long Revision=7;public SyncEnvelope Envelope=initial;public ArchiveKeyGrant? Grant;public int Requests;public long? LastMatch;public HttpMethod? RequestMethod;public string RequestPath="",RequestBody="";
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   Requests++;if(Offline)throw new HttpRequestException("Synthetic network unavailable");if(request.Headers.Authorization?.Parameter!="synthetic-access"||request.RequestUri!.Host!="unsc.responda.eu"||request.RequestUri.Scheme!="https")throw new Exception("Unexpected archive transport");
   if(Failure is {} failure)return new(failure){Content=JsonContent.Create(new{error="Synthetic API failure"})};
   var path=request.RequestUri!.AbsolutePath;
   if(path=="/api/archive-access-requests"||path.Contains("/access-requests")){RequestMethod=request.Method;RequestPath=path;RequestBody=request.Content==null?"":await request.Content.ReadAsStringAsync(ct);return new(path=="/api/archive-access-requests"?HttpStatusCode.Accepted:HttpStatusCode.OK){Content=JsonContent.Create(request.Method==HttpMethod.Get?(object)Array.Empty<object>():new{account=new string('D',64),email="recipient@example.invalid"})};}
   if(path.EndsWith("/storage")){RequestMethod=request.Method;RequestPath=path;return new(HttpStatusCode.OK){Content=JsonContent.Create(new{revision=Revision,encryptedBytes=8192,measuredAt=DateTimeOffset.UtcNow})};}
   if(path=="/api/archives")return new(HttpStatusCode.OK){Content=JsonContent.Create(new[]{archive},options:VaultSyncCrypto.Json)};
   if(path.EndsWith("/keys")&&request.Method==HttpMethod.Put){using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var value=body.RootElement;Grant=new(1,archive.Id,archive.ArchiveOwner,value.GetProperty("accountOwner").GetString()!,value.GetProperty("deviceId").GetGuid(),value.GetProperty("wrappedKey").GetString()!);return new(HttpStatusCode.OK){Content=JsonContent.Create(new{})};}
   if(path.Contains("/keys/"))return Revoked||Grant==null?new(HttpStatusCode.NotFound){Content=JsonContent.Create(new{error="Device revoked"})}:new(HttpStatusCode.OK){Content=JsonContent.Create(Grant,options:VaultSyncCrypto.Json)};
   if(path.EndsWith("/vault"))
   {
    if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=JsonContent.Create(new RemoteVault(Revision,DateTime.UtcNow,Envelope),options:VaultSyncCrypto.Json)};
    LastMatch=long.Parse(request.Headers.IfMatch.Single().Tag.Trim('"'));if(LastMatch!=Revision)return new(HttpStatusCode.Conflict);
    Envelope=JsonSerializer.Deserialize<SyncEnvelope>(await request.Content!.ReadAsStringAsync(ct),VaultSyncCrypto.Json)!;Revision++;return new(HttpStatusCode.OK){Content=JsonContent.Create(new{revision=Revision})};
   }
   throw new Exception("Unexpected synthetic archive route");
  }
 }
 public static async Task<int> Run()
 {
  var root=Directory.CreateTempSubdirectory("relay-archive-client-");var count=0;var account=new string('A',64);var namespaceOwner=new string('C',64);var key=RandomNumberGenerator.GetBytes(32);
  void Check(bool yes,string label){if(!yes)throw new Exception(label);count++;Console.WriteLine("PASS archive client: "+label);}
  try
  {
   new AccountSyncStore(root.FullName).Save(new(){Owner=account,Subject="synthetic-user",Activated=true,AccessToken="synthetic-access",TokenExpires=DateTimeOffset.UtcNow.AddHours(1),Revision=99});
   using var records=JsonDocument.Parse("[]");var plain=JsonSerializer.SerializeToUtf8Bytes(new PortableSnapshot(1,records.RootElement,new AppData(),[]),VaultSyncCrypto.Json);SyncEnvelope envelope;try{envelope=VaultSyncCrypto.Create(namespaceOwner,"Synthetic recovery code",key,plain);}finally{CryptographicOperations.ZeroMemory(plain);}
   var archive=new SharedArchive(Guid.NewGuid(),"Shared fixture",new string('B',64),namespaceOwner,"write",1);var server=new Server(archive,envelope);using var client=new CloudAccountClient(root.FullName,server);var ct=CancellationToken.None;
   Check((await client.Archives(ct)).Single().ArchiveOwner==namespaceOwner,"selected archive namespace remains distinct from authenticated account ownership");
   var storage=await client.ArchiveStorageUsage(archive.Id,ct);Check(storage.GetProperty("encryptedBytes").GetInt64()==8192&&server.RequestMethod==HttpMethod.Get&&server.RequestPath=="/api/archives/"+archive.Id.ToString("D")+"/storage","storage measurement stays on the selected archive and authenticated HTTPS transport");
   await client.RequestArchiveAccess(archive.Id,ct);using(var requestBody=JsonDocument.Parse(server.RequestBody))Check(server.RequestMethod==HttpMethod.Post&&server.RequestPath=="/api/archive-access-requests"&&requestBody.RootElement.GetProperty("archiveId").GetGuid()==archive.Id,"request creation carries only the selected archive id over authenticated HTTPS");
   await client.ArchiveAccessRequests(archive.Id,ct);Check(server.RequestMethod==HttpMethod.Get&&server.RequestPath=="/api/archives/"+archive.Id.ToString("D")+"/access-requests","owner review uses the selected archive route");var requestId=Guid.NewGuid();await client.ApproveArchiveAccessRequest(archive.Id,requestId,"read",DateTimeOffset.UtcNow.AddDays(7),ct);using(var approval=JsonDocument.Parse(server.RequestBody))Check(server.RequestMethod==HttpMethod.Post&&server.RequestPath.EndsWith("/"+requestId.ToString("D")+"/approve")&&approval.RootElement.GetProperty("access").GetString()=="read"&&!approval.RootElement.TryGetProperty("email",out _),"approval binds the request and chosen permissions without trusting a supplied recipient email");await client.DismissArchiveAccessRequest(archive.Id,requestId,ct);Check(server.RequestMethod==HttpMethod.Delete&&server.RequestPath.EndsWith("/"+requestId.ToString("D")),"decline uses an authenticated request-specific deletion");
   using var vault=new KeyVault(Path.Combine(root.FullName,"keys.vault"));vault.Unlock("Synthetic local account secret");using var device=vault.SharingDevice(account);var grant=ArchiveKeyCrypto.Wrap(archive.Id,namespaceOwner,account,device.Id,device.PublicKey,key);await client.GrantArchiveKey(archive.Id,grant,ct);
   Check(server.Grant?.WrappedKey==grant.WrappedKey&&!grant.WrappedKey.Contains(Convert.ToBase64String(key)),"native grant transport carries only recipient-bound ciphertext");
   var received=await client.ArchiveGrant(archive.Id,device.Id,ct);var restored=ArchiveKeyCrypto.Unwrap(received,archive.Id,namespaceOwner,account,device.Id,device.PrivateKey);
   try{var remote=await client.ReadArchive(archive,ct)??throw new Exception();var opened=VaultSyncCrypto.Open(remote.Envelope,namespaceOwner,restored);try{Check(VaultSyncCrypto.Parse(opened).Records.GetArrayLength()==0,"native client opens the selected shared vault using its own account device");}finally{CryptographicOperations.ZeroMemory(opened);}vault.StoreSyncKey(namespaceOwner,restored);vault.RememberArchive(account,archive);new ArchiveCopies(root.FullName,account).Save(archive,remote);}finally{CryptographicOperations.ZeroMemory(restored);}
   Check(await client.WriteArchive(archive,envelope,7,ct)==8&&server.LastMatch==7,"shared writes use their own CAS revision rather than the personal vault revision");
   Check(await client.WriteArchive(archive,envelope,7,ct)==null&&server.Revision==8,"stale shared writes preserve the current server copy");
   var requests=server.Requests;try{await client.WriteArchive(archive with{Access="read"},envelope,8,ct);throw new Exception("Reader write accepted");}catch(OnlineAccessException){Check(server.Requests==requests,"read-only archive writes are rejected before transport");}
   server.Offline=true;try{await client.ReadArchive(archive,ct);throw new Exception("Offline read unexpectedly reached API");}catch(HttpRequestException ex){Check(ArchiveCopyPolicy.NetworkUnavailable(ex),"a real transport failure is eligible for an existing encrypted offline copy");}
   var copy=new ArchiveCopies(root.FullName,account).Read(archive)!;var offlineKey=vault.SyncKey(namespaceOwner)!;try{var content=VaultSyncCrypto.Open(copy.Envelope,namespaceOwner,offlineKey);try{Check(VaultSyncCrypto.Parse(content).Version==1,"the downloaded copy decrypts locally without another API request");}finally{CryptographicOperations.ZeroMemory(content);}}finally{CryptographicOperations.ZeroMemory(offlineKey);}
   vault.Lock();try{vault.SyncKey(namespaceOwner);throw new Exception("Locked cache key readable");}catch(InvalidOperationException){Check(true,"an encrypted offline copy does not bypass account vault locking");}
   server.Offline=false;server.Failure=HttpStatusCode.Forbidden;try{await client.ReadArchive(archive,ct);throw new Exception("Denied read accepted");}catch(OnlineAccessException ex){Check(ex.Status==HttpStatusCode.Forbidden&&!ArchiveCopyPolicy.NetworkUnavailable(ex),"server denial never silently falls back to offline authority");}
   server.Failure=HttpStatusCode.ServiceUnavailable;try{await client.ReadArchive(archive,ct);throw new Exception("Unavailable service accepted");}catch(HttpRequestException ex){Check(ArchiveCopyPolicy.NetworkUnavailable(ex),"server unavailability is distinct from a permission denial");}
   server.Failure=null;server.Revoked=true;try{await client.ArchiveGrant(archive.Id,device.Id,ct);throw new Exception("Revoked grant readable");}catch(OnlineAccessException ex){Check(ex.Status==HttpStatusCode.NotFound&&!ArchiveCopyPolicy.NetworkUnavailable(ex),"a revoked device grant is not treated as a network failure");}
   Check(!ArchiveCopyPolicy.NetworkUnavailable(new OnlineAccessException("Expired session")),"an authentication expiry is never guessed to be a network outage");
   var personalArchive=archive with{OwnerAccount=account,Access="owner",Personal=true};var personalServer=new Server(personalArchive,envelope);
   using(var personalClient=new CloudAccountClient(root.FullName,personalServer))
   {
    Check((await personalClient.PersonalArchive(ct)).Id==archive.Id,"personal descriptor is bound to the authenticated owner and reserved archive");
    try{await personalClient.Read(ct);throw new Exception("Changed personal namespace silently accepted");}catch(PersonalArchiveChangedException){Check(true,"changed personal namespace requires explicit recovery before applying content");}
    personalClient.State.ArchiveOwner=namespaceOwner;personalClient.Save();
    Check((await personalClient.Read(ct))?.Envelope.Owner==namespaceOwner&&personalClient.State.Owner==account,"explicitly recovered personal namespace never changes the account identity");
   }
   using(var reopened=new CloudAccountClient(root.FullName,new Server(personalArchive,envelope)))Check(reopened.SyncOwner==namespaceOwner&&reopened.State.Owner==account,"recovered namespace survives protected state reopening");
   Console.WriteLine("Archive client checks: "+count);return 0;
  }
  finally{CryptographicOperations.ZeroMemory(key);root.Delete(true);}
 }
}
