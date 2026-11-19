using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 sealed class NativePlainExport(PlainExportReview review,long generation,string owner)
 {
  public Guid Token{get;}=Guid.NewGuid();public PlainExportReview Review{get;}=review;public long Generation{get;}=generation;public string Owner{get;}=owner;public DateTimeOffset Expires{get;set;}=DateTimeOffset.UtcNow.AddMinutes(10);public string Phase="verification";public PlainExportChallenge? Challenge;public byte[]? Prepared;public CancellationTokenSource Cancellation=new();
 }
 NativePlainExport? nativePlainExport;readonly SemaphoreSlim plainExportGate=new(1,1);
 bool PlainExportBusy=>nativePlainExport!=null;
 void ClearNativePlainExport(){ClearNativeErasure();var job=nativePlainExport;nativePlainExport=null;if(job==null)return;job.Cancellation.Cancel();if(job.Prepared!=null)CryptographicOperations.ZeroMemory(job.Prepared);job.Prepared=null;Emit("plainExportClosed",new{token=job.Token});}
 void CheckNativePlainExport(NativePlainExport job){RequireArchiveSession(job.Generation);if(nativePlainExport!=job||job.Owner!=cloud.State.Owner||job.Expires<=DateTimeOffset.UtcNow||job.Cancellation.IsCancellationRequested)throw new OperationCanceledException("Esportazione annullata o scaduta. Richiedi una nuova verifica.");}
 async Task<object?> NativePlainExportAction(string action,JsonElement p)
 {
  if(action=="archivePlainCancel"){var cancelling=nativePlainExport;if(cancelling==null||p.GetProperty("token").GetGuid()!=cancelling.Token)return true;ClearNativePlainExport();if(cancelling.Challenge!=null)try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await cloud.CancelPlainExport(cancelling.Review.ArchiveId,cancelling.Challenge.Id,timeout.Token);}catch(Exception e)when(e is OnlineAccessException or HttpRequestException or OperationCanceledException){}return true;}
  if(!await plainExportGate.WaitAsync(0))throw new InvalidOperationException("Attendi l’operazione di esportazione in corso.");
  var cloudEntered=false;try{
   if(action!="archivePlainCancel"){if(!await cloudGate.WaitAsync(0))throw new InvalidOperationException("Attendi la sincronizzazione in corso.");cloudEntered=true;}
   if(action=="archivePlainReview"){
    RequireArchiveSession(authenticationGeneration);if(nativePlainExport!=null||AccountErasureBusy)throw new InvalidOperationException("Chiudi prima l’esportazione già aperta.");var id=p.GetProperty("archiveId").GetGuid();var generation=authenticationGeneration;var selected=await FindArchive(id);RequireArchiveSession(generation);if(selected.Access!="owner"||selected.OwnerAccount!=cloud.State.Owner)throw new InvalidOperationException("Solo il titolare può esportare tutti i contenuti.");if(selected.Offline||LocalArchiveCopies.Read(selected) is{Pending:true}||selected.Personal&&(cloudConflict||cloudPersonalRecovery))throw new InvalidOperationException("Conserva e sincronizza le modifiche locali prima di esportare la revisione corrente.");if(selected.Personal){var snapshot=VaultSyncCrypto.Capture(vault,data,hosts.List());try{if(VaultSyncCrypto.Hash(snapshot)!=cloud.State.Baseline)throw new InvalidOperationException("Sincronizza prima le modifiche dell’archivio personale.");}finally{CryptographicOperations.ZeroMemory(snapshot);}}var review=await cloud.ReviewPlainExport(id,cloudLifetime.Token);RequireArchiveSession(generation);if(review.ArchiveOwner!=selected.ArchiveOwner)throw new InvalidDataException("Identità dell’archivio cambiata.");nativePlainExport=new(review,generation,cloud.State.Owner);return new{token=nativePlainExport.Token,review};
   }
   var job=nativePlainExport??throw new InvalidOperationException("Apri prima il riepilogo di esportazione.");if(p.GetProperty("token").GetGuid()!=job.Token)throw new InvalidOperationException("Finestra di esportazione non più valida.");
   if(action=="archivePlainCancel"){ClearNativePlainExport();if(job.Challenge!=null)try{await cloud.CancelPlainExport(job.Review.ArchiveId,job.Challenge.Id,cloudLifetime.Token);}catch(Exception e)when(e is OnlineAccessException or HttpRequestException or OperationCanceledException){}return true;}
   CheckNativePlainExport(job);using var linked=CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token,cloudLifetime.Token);var ct=linked.Token;
   if(action=="archivePlainBegin"){
    if(job.Challenge!=null)throw new InvalidOperationException("Il codice email è già stato richiesto.");if(!Confirm("Creare una copia in chiaro di tutti i dati della revisione "+job.Review.Revision+" di “"+job.Review.Name+"”? Include password leggibili e chiavi nel formato originale. Sono escluse versioni storiche, fattori dell’account e ticket. Seguiranno 2FA e un codice email."))return new{cancelled=true};CheckNativePlainExport(job);if(job.Review.Format==1){var envelope=await cloud.PlainExportSource(job.Review,ct);var opened=PortablePlainExport.OpenLegacy(envelope,S(p,"password"));CryptographicOperations.ZeroMemory(opened.Bytes);}else{var selected=await FindArchive(job.Review.ArchiveId);var verified=await OpenArchiveKey(selected);CryptographicOperations.ZeroMemory(verified);}CheckNativePlainExport(job);var challenge=await cloud.BeginPlainExport(job.Review,new InAppAccountBrowser(this,dataPath),ct);CheckNativePlainExport(job);job.Challenge=challenge;job.Phase="email";job.Expires=challenge.ExpiresAt;return challenge;
   }
   if(action!="archivePlainSave")throw new ArgumentException("Operazione non disponibile.");if(job.Challenge==null)throw new InvalidOperationException("Completa prima la verifica 2FA e richiedi il codice email.");var destination=SaveFile("Salva ZIP in chiaro della revisione corrente","unsc-relay-"+job.Review.ArchiveId.ToString("D")+"-r"+job.Review.Revision+".zip");if(destination==null)return new{cancelled=true,prepared=job.Prepared!=null};CheckNativePlainExport(job);
   if(job.Prepared==null){job.Phase="preparing";var envelope=await cloud.DownloadPlainExport(job.Review,job.Challenge.Id,S(p,"code"),ct);CheckNativePlainExport(job);byte[]? plain=null,key=null,prepared=null;
    try{
     if(job.Review.Format==2){var selected=await FindArchive(job.Review.ArchiveId);CheckNativePlainExport(job);key=await OpenArchiveKey(selected);CheckNativePlainExport(job);var sealedData=envelope.Deserialize<SyncEnvelope>(VaultSyncCrypto.Json)??throw new InvalidDataException("Archivio non valido.");plain=VaultSyncCrypto.Open(sealedData,job.Review.ArchiveOwner,key);var bytes=plain;prepared=await Task.Run(()=>PortablePlainExport.Create(bytes,new(job.Review.ArchiveId,job.Review.ArchiveOwner,job.Review.Name,job.Review.Revision),ct),ct);}
     else{var file=PortablePlainExport.OpenLegacy(envelope,S(p,"password"));plain=file.Bytes;var bytes=plain;prepared=await Task.Run(()=>PortablePlainExport.Legacy(file.Name,bytes,new(job.Review.ArchiveId,job.Review.ArchiveOwner,job.Review.Name,job.Review.Revision),ct),ct);}
     CheckNativePlainExport(job);job.Prepared=prepared;job.Phase="ready";prepared=null;
    }finally{if(plain!=null)CryptographicOperations.ZeroMemory(plain);if(key!=null)CryptographicOperations.ZeroMemory(key);if(prepared!=null)CryptographicOperations.ZeroMemory(prepared);}
   }
   CheckNativePlainExport(job);job.Phase="saving";try{FolderSync.CheckLocalParents(Path.GetDirectoryName(Path.GetFullPath(destination))!);if(File.Exists(destination)&&File.GetAttributes(destination).HasFlag(FileAttributes.ReparsePoint))throw new IOException("La destinazione non può essere un collegamento.");PrivateKeyExport.Write(destination,job.Prepared);job.Phase="saved";return new{saved=true,prepared=true};}catch(IOException){throw new IOException("Salvataggio non riuscito. Il pacchetto rimane solo in memoria fino alla chiusura o scadenza: scegli un’altra posizione e riprova.");}
  }catch{if(nativePlainExport is{} failed)failed.Phase=failed.Prepared!=null?"ready":failed.Challenge!=null?"email":"verification";throw;}finally{if(cloudEntered)cloudGate.Release();plainExportGate.Release();}
 }
}
