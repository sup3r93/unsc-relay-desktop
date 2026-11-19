using System.Net;
using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 ArchiveCopies LocalArchiveCopies=>new(dataPath,cloud.State.Owner);
 static bool ArchiveOffline(Exception ex)=>ArchiveCopyPolicy.NetworkUnavailable(ex);
 void CacheArchive(SharedArchive archive,RemoteVault remote,byte[] key,bool pending=false,bool replacePending=false)
 {
  vault.StoreSyncKey(archive.ArchiveOwner,key);vault.RememberArchive(cloud.State.Owner,archive with{Offline=false});LocalArchiveCopies.Save(archive,remote,pending,replacePending);
 }
 async Task<SharedArchive> FindArchive(Guid id)
 {
  var archives=await cloud.Archives(cloudLifetime.Token);return archives.SingleOrDefault(a=>a.Id==id)??throw new InvalidOperationException("Non hai accesso a questo archivio.");
 }
 async Task PrepareArchiveDevice()
 {
  using var device=vault.SharingDevice(cloud.State.Owner);var existing=(await cloud.OwnArchiveDevices(cloudLifetime.Token)).SingleOrDefault(d=>d.Id==device.Id);
  if(existing is {} registered)
  {
   if(registered.Revoked.HasValue)throw new InvalidOperationException("Questo dispositivo è stato revocato. Collega nuovamente il dispositivo da un accesso verificato.");
   if(registered.Publickey!=device.PublicKey)throw new CryptographicException("Identità del dispositivo non corrispondente.");
  }
  else await cloud.RegisterArchiveDevice(device,cloudLifetime.Token);
  // Enrol the existing personal sync key without exporting local device identities or changing ciphertext.
  var personal=(await cloud.Archives(cloudLifetime.Token)).SingleOrDefault(a=>a.Personal&&a.Access=="owner");
  if(personal!=null){var bytes=vault.SyncKey(personal.ArchiveOwner);if(bytes!=null)try{await cloud.GrantArchiveKey(personal.Id,ArchiveKeyCrypto.Wrap(personal.Id,personal.ArchiveOwner,cloud.State.Owner,device.Id,device.PublicKey,bytes),cloudLifetime.Token);}finally{CryptographicOperations.ZeroMemory(bytes);}}
 }
 void RequireArchiveSession(long generation)
 {
  if(CriticalUpdateRequired||closing||updating||AppLocked||AccountLocked||!vault.IsUnlocked||generation!=authenticationGeneration)throw new OperationCanceledException("La sessione è stata bloccata. Ripeti l’accesso.");
 }
 async Task<byte[]> OpenArchiveKey(SharedArchive archive)
 {
  var generation=authenticationGeneration;Guid deviceId;using(var info=vault.SharingDevice(cloud.State.Owner,false))deviceId=info.Id;var grant=await cloud.ArchiveGrant(archive.Id,deviceId,cloudLifetime.Token);RequireArchiveSession(generation);
  using var device=vault.SharingDevice(cloud.State.Owner,false);return ArchiveKeyCrypto.Unwrap(grant,archive.Id,archive.ArchiveOwner,cloud.State.Owner,device.Id,device.PrivateKey);
 }
 async Task AuthorizeArchiveDevices(SharedArchive archive,string account)
 {
  var targets=await cloud.ArchiveDevices(archive.Id,account,cloudLifetime.Token);if(targets.Count==0)throw new InvalidOperationException("Il destinatario deve prima preparare un dispositivo dal proprio account.");
  var bytes=await OpenArchiveKey(archive);try{foreach(var target in targets)await cloud.GrantArchiveKey(archive.Id,ArchiveKeyCrypto.Wrap(archive.Id,archive.ArchiveOwner,account,target.Id,target.Publickey,bytes),cloudLifetime.Token);}finally{CryptographicOperations.ZeroMemory(bytes);}
 }
 async Task<object?> ArchiveAction(string action,JsonElement p)
 {
  var generation=authenticationGeneration;RequireArchiveSession(generation);
  if(action=="archiveRequestAccess"){await cloud.RequestArchiveAccess(p.GetProperty("archiveId").GetGuid(),cloudLifetime.Token);RequireArchiveSession(generation);return true;}
  if(action=="archiveLocalList")return vault.RememberedArchives(cloud.State.Owner).Where(a=>LocalArchiveCopies.Read(a)!=null).Select(a=>a with{Offline=true}).ToList();
  if(action=="archiveList")
  {
   try{var archives=await cloud.Archives(cloudLifetime.Token);foreach(var archive in archives)vault.RememberArchive(cloud.State.Owner,archive);return archives;}
   catch(Exception ex)when(ArchiveOffline(ex)){return vault.RememberedArchives(cloud.State.Owner).Where(a=>LocalArchiveCopies.Read(a)!=null).Select(a=>a with{Offline=true}).ToList();}
  }
  if(action=="archivePrepare"){await PrepareArchiveDevice();return new{ready=true};}
  if(action=="archiveCreate")
  {
   await PrepareArchiveDevice();var recovery=S(p,"recovery");if(recovery.Length is <24 or >4096)throw new ArgumentException("Conserva un codice di recupero valido.");
   var archive=await cloud.CreateArchive(S(p,"name"),cloudLifetime.Token);var key=RandomNumberGenerator.GetBytes(32);byte[]? bytes=null;
   try
   {
    using var records=JsonDocument.Parse("[]");bytes=JsonSerializer.SerializeToUtf8Bytes(new PortableSnapshot(2,records.RootElement,new AppData(),[]),VaultSyncCrypto.Json);
    var envelope=VaultSyncCrypto.Create(archive.ArchiveOwner,recovery,key,bytes);using var device=vault.SharingDevice(cloud.State.Owner,false);
    await cloud.GrantArchiveKey(archive.Id,ArchiveKeyCrypto.Wrap(archive.Id,archive.ArchiveOwner,cloud.State.Owner,device.Id,device.PublicKey,key),cloudLifetime.Token);
    var created=await cloud.WriteArchive(archive,envelope,0,cloudLifetime.Token);if(created==null)throw new InvalidOperationException("Archivio modificato durante la creazione. La copia cifrata sul server non è stata sovrascritta.");CacheArchive(archive,new(created.Value,DateTime.UtcNow,envelope),key);return archive;
   }finally{CryptographicOperations.ZeroMemory(key);if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);}
  }
  var id=p.GetProperty("archiveId").GetGuid();SharedArchive selected;var offline=false;
  try{if(p.TryGetProperty("localOnly",out var localOnly)&&localOnly.GetBoolean()){selected=vault.RememberedArchives(cloud.State.Owner).SingleOrDefault(a=>a.Id==id)??throw new InvalidOperationException("Nessuna copia locale per questo archivio.");offline=true;}else selected=await FindArchive(id);}catch(Exception ex)when(ArchiveOffline(ex)){selected=vault.RememberedArchives(cloud.State.Owner).SingleOrDefault(a=>a.Id==id)??throw new InvalidOperationException("Questo archivio non è disponibile nella copia locale.");offline=true;}
  if(offline&&action is not("archiveRead" or "archiveCopySecret" or "archiveUpdateRecord" or "archiveAddPassword" or "archiveDeleteRecord" or "archiveCommentAdd" or "archiveExport"))throw new InvalidOperationException("La gestione degli accessi richiede una connessione. La copia locale resta disponibile.");
  if(action is "archiveAccessRequests" or "archiveRequestApprove" or "archiveRequestDismiss"){
   if(selected.Access!="owner")throw new InvalidOperationException("Solo il titolare può gestire le richieste.");var requests=await cloud.ArchiveAccessRequests(id,cloudLifetime.Token);RequireArchiveSession(generation);if(action=="archiveAccessRequests")return requests;
   var requestId=p.GetProperty("requestId").GetGuid();var request=requests.EnumerateArray().SingleOrDefault(row=>row.GetProperty("id").GetGuid()==requestId);if(request.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException("La richiesta non è più disponibile.");
   var email=request.GetProperty("email").GetString()!;var access=S(p,"access");var expires=p.TryGetProperty("expiresAt",out var expiry)&&expiry.ValueKind==JsonValueKind.String?expiry.GetDateTimeOffset():(DateTimeOffset?)null;var review=NativeText(action=="archiveRequestApprove"?"Approva richiesta":"Rifiuta richiesta")+"\n\n"+selected.Name+"\n"+email+(action=="archiveRequestApprove"?"\n"+NativeText(access=="write"?"Lettura e scrittura":"Sola lettura")+"\n"+(expires?.ToLocalTime().ToString("f")??NativeText("Senza scadenza")):"");if(!Confirm(review))return new{cancelled=true};RequireArchiveSession(generation);
   if(action=="archiveRequestDismiss"){await cloud.DismissArchiveAccessRequest(id,requestId,cloudLifetime.Token);RequireArchiveSession(generation);return new{cancelled=false};}
   var recipient=await cloud.ApproveArchiveAccessRequest(id,requestId,access,expires,cloudLifetime.Token);RequireArchiveSession(generation);try{await AuthorizeArchiveDevices(selected,recipient.GetProperty("account").GetString()!);return new{ready=true,cancelled=false};}catch(InvalidOperationException){return new{ready=false,cancelled=false};}
  }
  if(action=="archiveStorageUsage"){var result=await cloud.ArchiveStorageUsage(id,cloudLifetime.Token);RequireArchiveSession(generation);return result;}
  if(action=="archiveActivity")return await cloud.ArchiveActivity(id,p.GetProperty("page").GetInt32(),cloudLifetime.Token);
  if(action=="archivePermissionsReport"){var members=await cloud.ArchiveMembers(id,cloudLifetime.Token);RequireArchiveSession(generation);var pdf=S(p,"format")=="pdf";byte[] bytes;if(pdf){var report=new PermissionReportWindow(this,NativeText("Riepilogo dei permessi"));report.Show();try{bytes=await report.Render(dataPath,ArchivePermissionReport.Html(selected,members,nativeLanguage,NativeText));}finally{report.Close();}}else bytes=Encoding.UTF8.GetBytes(ArchivePermissionReport.Create(selected,members));try{RequireArchiveSession(generation);var destination=SaveFile("Salva riepilogo permessi","relay-permissions-"+id.ToString("D")+(pdf?".pdf":".csv"));if(destination==null)return false;RequireArchiveSession(generation);PrivateKeyExport.Write(destination,bytes);return true;}finally{CryptographicOperations.ZeroMemory(bytes);}}
  if(action=="archiveAccessReview"){var result=await cloud.ArchiveAccessReview(id,accountOperationStop.Token);RequireArchiveSession(generation);return result;}
  if(action=="archiveAccessReviewed"){var result=await cloud.AcknowledgeArchiveReview(id,I(p,"intervalDays",30),S(p,"intent"),accountOperationStop.Token);RequireArchiveSession(generation);return result;}
  if(action=="archiveSharingReview")return await cloud.ReviewArchiveSharing(id,p.GetProperty("suspended").GetBoolean(),cloudLifetime.Token);
  if(action=="archiveSharingSet"){var review=await cloud.ReviewArchiveSharing(id,p.GetProperty("suspended").GetBoolean(),cloudLifetime.Token);if(review.Intent!=S(p,"intent"))throw new InvalidOperationException("Gli accessi sono cambiati. Aggiorna il riepilogo e ripeti la verifica.");await cloud.SetArchiveSharing(review,new InAppAccountBrowser(this,dataPath),()=>RequireArchiveSession(generation),cloudLifetime.Token);return true;}
  if(action=="archiveIntegrityLast")return data.IntegrityObservations.SingleOrDefault(value=>value.ArchiveId==id);
  if(action=="archiveMembers")return await cloud.ArchiveMembers(id,cloudLifetime.Token);
  if(action=="archiveRename"){await cloud.RenameArchive(id,S(p,"name"),cloudLifetime.Token);return true;}
  if(action=="archiveRevoke"){await cloud.RemoveArchiveMember(id,S(p,"account"),cloudLifetime.Token);return true;}
  if(action=="archiveAuthorize"){await AuthorizeArchiveDevices(selected,S(p,"account"));return true;}
  if(action=="archiveShare")
  {
   var requestedExpiry=p.TryGetProperty("expiresAt",out var requested)&&requested.ValueKind==JsonValueKind.String?requested.GetDateTimeOffset():(DateTimeOffset?)null;
   if(!Confirm(NativeText("Conferma invito")+"\n\n"+selected.Name+"\n"+S(p,"email")+"\n"+NativeText(S(p,"access")=="write"?"Lettura e scrittura":"Sola lettura")+"\n"+(requestedExpiry?.ToLocalTime().ToString("f")??NativeText("Senza scadenza"))))throw new OperationCanceledException();RequireArchiveSession(generation);
   var recipient=await cloud.ShareArchive(id,S(p,"email"),S(p,"access"),cloudLifetime.Token,p.TryGetProperty("expiresAt",out var expiry)&&expiry.ValueKind==JsonValueKind.String?expiry.GetDateTimeOffset():null);
   var account=recipient.GetProperty("account").GetString()!;
   try{await AuthorizeArchiveDevices(selected,account);return new{ready=true,message="Archivio condiviso e dispositivi autorizzati."};}
   catch(InvalidOperationException ex){return new{ready=false,message="Accesso aggiunto. "+ex.Message};}
  }
  if(action=="archiveTransfer")
  {
   var email=S(p,"email");if(!Confirm("Trasferire la proprietà dell’archivio a "+email+"? Perderai la gestione degli accessi. Seguirà una nuova verifica 2FA."))return false;
   var challenge=await cloud.BeginArchiveTransfer(id,email,p.GetProperty("keepAccess").GetBoolean(),cloudLifetime.Token);
   await cloud.LoginAsync(cloudLifetime.Token,new InAppAccountBrowser(this,dataPath));await cloud.CompleteArchiveTransfer(id,challenge,cloudLifetime.Token);return true;
  }
  var cached=LocalArchiveCopies.Read(selected);RemoteVault remote;
  try{if(offline)throw new HttpRequestException();remote=await cloud.ReadArchive(selected,cloudLifetime.Token)??throw new InvalidOperationException("Il titolare deve prima preparare questo archivio.");}
  catch(Exception ex)when(ArchiveOffline(ex)&&cached!=null){remote=new(cached.Revision,cached.UpdatedAt,cached.Envelope);offline=true;}
  if(cached is{Pending:true})remote=new(cached.Revision,cached.UpdatedAt,cached.Envelope);
  if(action=="archivePublish")
  {
   if(cached is not{Pending:true})return true;
   var current=await cloud.ReadArchive(selected,cloudLifetime.Token)??throw new InvalidOperationException("Archivio online non disponibile.");if(current.Revision!=cached.Revision)throw new InvalidOperationException("La copia online è cambiata: conserva il recupero cifrato e verifica le differenze prima di sostituire i dati. La copia locale non è stata eliminata.");
   var verifiedKey=await OpenArchiveKey(selected);byte[]? verified=null;
   try{verified=VaultSyncCrypto.Open(cached.Envelope,selected.ArchiveOwner,verifiedKey);VaultSyncCrypto.Parse(verified);var revision=await cloud.WriteArchive(selected,cached.Envelope,cached.Revision,cloudLifetime.Token);if(revision==null)throw new InvalidOperationException("Conflitto di revisione: le modifiche offline sono conservate.");CacheArchive(selected,new(revision.Value,DateTime.UtcNow,cached.Envelope),verifiedKey,replacePending:true);return true;}
   finally{CryptographicOperations.ZeroMemory(verifiedKey);if(verified!=null)CryptographicOperations.ZeroMemory(verified);}
  }
  if(action=="archiveRecover")
  {
   if(selected.Access!="owner")throw new InvalidOperationException("Solo il titolare può usare il codice di recupero.");await PrepareArchiveDevice();
   var recovered=VaultSyncCrypto.UnlockKey(remote.Envelope,selected.ArchiveOwner,S(p,"recovery"));byte[]? verify=null;
   try{verify=VaultSyncCrypto.Open(remote.Envelope,selected.ArchiveOwner,recovered);VaultSyncCrypto.Parse(verify);using var device=vault.SharingDevice(cloud.State.Owner,false);await cloud.GrantArchiveKey(id,ArchiveKeyCrypto.Wrap(id,selected.ArchiveOwner,cloud.State.Owner,device.Id,device.PublicKey,recovered),cloudLifetime.Token);return true;}
   finally{CryptographicOperations.ZeroMemory(recovered);if(verify!=null)CryptographicOperations.ZeroMemory(verify);}
  }
  if(action=="archiveExport")
  {
   var destination=SaveFile("Conserva copia cifrata dell’archivio","unsc-relay-"+id.ToString("D")+".json");if(destination!=null)AtomicFile.Write(destination,JsonSerializer.SerializeToUtf8Bytes(remote.Envelope,VaultSyncCrypto.Json));return true;
  }
  byte[] dataKey;
  try{if(offline)throw new HttpRequestException();dataKey=await OpenArchiveKey(selected);}
  catch(Exception ex)when(ArchiveOffline(ex)&&cached!=null){dataKey=vault.SyncKey(selected.ArchiveOwner)??throw new InvalidOperationException("La chiave di questa copia locale non è disponibile.");offline=true;}
  byte[]? plain=null,changed=null;
  try
  {
   RequireArchiveSession(generation);plain=VaultSyncCrypto.Open(remote.Envelope,selected.ArchiveOwner,dataKey);var snapshot=VaultSyncCrypto.Parse(plain);
   if(action=="archiveIntegrity"){var report=SnapshotIntegrity.Inspect(snapshot);RequireArchiveSession(generation);var pending=cached?.Pending??false;var previousObservations=data.IntegrityObservations;data.IntegrityObservations=previousObservations.Where(value=>value.ArchiveId!=id).Append(new(id,remote.Revision,report.CheckedAt,offline,pending,report.WarningCount)).OrderByDescending(value=>value.CheckedAt).Take(1000).ToList();try{store.Save(data);}catch{data.IntegrityObservations=previousObservations;throw;}return new{archiveId=id,revision=remote.Revision,offline,pending,report};}
   if(!offline)CacheArchive(selected,remote,dataKey);
   if(action=="archiveRead")return new{archive=selected,revision=remote.Revision,offline,pending=cached?.Pending??false,records=snapshot.Records.EnumerateArray().Select(r=>r.GetProperty("info").Clone()).ToArray(),profiles=snapshot.Data.Profiles.Select(profile=>new{profile.Name,profile.Protocol,profile.Host,profile.Port})};
   if(action=="archiveCopySecret")
   {
    var record=snapshot.Records.EnumerateArray().Single(r=>r.GetProperty("info").GetProperty("id").GetString()==S(p,"recordId"));var bytes=record.GetProperty("content").GetBytesFromBase64();
    try{RequireArchiveSession(generation);if(record.GetProperty("info").GetProperty("kind").GetString() is not("password" or "ssh-key" or "ssh-native-key"))throw new InvalidOperationException("Questo elemento non è un segreto copiabile.");Clipboard.SetText(Encoding.UTF8.GetString(bytes));return true;}finally{CryptographicOperations.ZeroMemory(bytes);}
   }
   if(action is "archiveUpdateRecord" or "archiveAddPassword" or "archiveDeleteRecord" or "archiveCommentAdd")
   {
    if(selected.Access=="read")throw new InvalidOperationException("L’archivio è in sola lettura.");if(p.GetProperty("revision").GetInt64()!=remote.Revision)throw new InvalidOperationException("L’archivio è cambiato. Aggiorna prima di modificare i dati.");
    var json=JsonNode.Parse(plain)!.AsObject();var records=json["records"]!.AsArray();
    if(action=="archiveAddPassword")
    {
     var name=S(p,"name").Trim();var password=S(p,"password");if(name.Length is <1 or >100||password.Length is <1 or >8192||password.Contains('\0'))throw new ArgumentException("Controlla il nome e la nuova password.");
     var bytes=Encoding.UTF8.GetBytes(password);try{var info=new KeyInfo(Guid.NewGuid().ToString("N"),name,"",DateTime.UtcNow,Kind:"password");records.Add(JsonSerializer.SerializeToNode(new{info,content=Convert.ToBase64String(bytes)},VaultSyncCrypto.Json));}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    else
    {
     var record=records.Single(r=>r!["info"]!["id"]!.GetValue<string>()==S(p,"recordId"))!;if(record["info"]!["kind"]!.GetValue<string>() is not("ssh-key" or "ssh-native-key" or "password"))throw new InvalidOperationException("Questo elemento non può essere modificato o eliminato.");
     if(action=="archiveDeleteRecord")
     {
      var recordId=S(p,"recordId");if(snapshot.Data.Profiles.Any(profile=>profile.KeyId==recordId||profile.PasswordId==recordId))throw new InvalidOperationException("L’elemento è usato da una connessione: modifica prima la connessione dal desktop.");
      if(!Confirm("Eliminare questo elemento dall’archivio condiviso?"))return false;records.Remove(record);
     }
     else if(action=="archiveCommentAdd"){var info=record["info"]!.Deserialize<KeyInfo>(VaultSyncCrypto.Json)??throw new ArgumentException("Commento non valido.");record["info"]!["discussion"]=JsonSerializer.SerializeToNode(ItemDiscussion.Append(info.Discussion,S(p,"author"),S(p,"text"),DateTimeOffset.UtcNow),VaultSyncCrypto.Json);}
     else{var name=S(p,"name").Trim();var comment=S(p,"comment");if(name.Length is <1 or >100||comment.Length>1000)throw new ArgumentException("Controlla nome e note.");record["info"]!["name"]=name;record["info"]!["comment"]=comment;}
    }
    changed=JsonSerializer.SerializeToUtf8Bytes(json,VaultSyncCrypto.Json);VaultSyncCrypto.Parse(changed);var sealedCopy=VaultSyncCrypto.Seal(remote.Envelope,dataKey,changed);
    long? written;
    try{if(offline)throw new HttpRequestException();written=await cloud.WriteArchive(selected,sealedCopy,remote.Revision,cloudLifetime.Token);}
    catch(Exception ex)when(ArchiveOffline(ex)){CacheArchive(selected,new(remote.Revision,DateTime.UtcNow,sealedCopy),dataKey,pending:true);return true;}
    if(written==null)
    {
     var folder=Path.Combine(dataPath,"SyncConflicts");FolderSync.CheckLocalParents(folder);Directory.CreateDirectory(folder);FolderSync.CheckLocalParents(folder);
     AtomicFile.Write(Path.Combine(folder,id.ToString("N")+"-"+Guid.NewGuid().ToString("N")+".json"),JsonSerializer.SerializeToUtf8Bytes(sealedCopy,VaultSyncCrypto.Json));
     throw new InvalidOperationException("Un altro dispositivo ha modificato l’archivio. Nessuna versione è stata sovrascritta; una copia cifrata delle tue modifiche è conservata nei dati dell’app.");
    }CacheArchive(selected,new(written.Value,DateTime.UtcNow,sealedCopy),dataKey,replacePending:true);return true;
   }
   throw new ArgumentException("Operazione archivio non disponibile.");
  }finally{CryptographicOperations.ZeroMemory(dataKey);if(plain!=null)CryptographicOperations.ZeroMemory(plain);if(changed!=null)CryptographicOperations.ZeroMemory(changed);}
 }
}
