using System.Security.Cryptography;
namespace UnscRelay.Core;
public static class PersonalArchiveRecovery
{
 public static async Task Commit(AccountSyncState state,SharedArchive target,string password,byte[] verifiedLocal,
  Func<Task<RemoteVault?>> read,Func<SyncEnvelope,Task<long?>> create,Action<string,byte[]> remember,Action save)
 {
  if(!target.Personal||target.Access!="owner"||target.OwnerAccount!=state.Owner)throw new InvalidOperationException("Il recupero richiede l’archivio personale del titolare autenticato.");
  var previous=(state.ArchiveOwner,state.Revision,state.Baseline,state.Header,state.Migrated);byte[]? key=null,opened=null;
  try
  {
   VaultSyncCrypto.Parse(verifiedLocal);var localHash=VaultSyncCrypto.Hash(verifiedLocal);var remote=await read();SyncEnvelope envelope;long revision;
   if(remote==null)
   {
    key=RandomNumberGenerator.GetBytes(32);envelope=VaultSyncCrypto.Create(target.ArchiveOwner,password,key,verifiedLocal);
    revision=await create(envelope)??throw new InvalidOperationException("Un altro dispositivo ha popolato l’archivio. Nessuna copia è stata sovrascritta; ripeti la verifica.");
   }
   else
   {
    // Retry after a successful CAS and a failed local key/checkpoint write: never upload over it.
    envelope=remote.Envelope;revision=remote.Revision;key=VaultSyncCrypto.UnlockKey(envelope,target.ArchiveOwner,password);
    opened=VaultSyncCrypto.Open(envelope,target.ArchiveOwner,key);var canonical=VaultSyncCrypto.Canonicalize(opened);
    try{if(VaultSyncCrypto.Hash(canonical)!=localHash)throw new InvalidOperationException("L’archivio online contiene dati diversi dalla copia locale verificata. Apri Archivi collegati: questo recupero non lo sovrascrive.");}
    finally{CryptographicOperations.ZeroMemory(canonical);}
   }
   if(revision<1)throw new InvalidDataException("Revisione del recupero non valida.");
   remember(target.ArchiveOwner,key);
   state.ArchiveOwner=target.ArchiveOwner;state.Revision=revision;state.Baseline=localHash;
   state.Header=envelope with{Iv=Convert.ToBase64String(new byte[12]),Ciphertext=Convert.ToBase64String(new byte[16])};state.Migrated=true;save();
  }
  catch{(state.ArchiveOwner,state.Revision,state.Baseline,state.Header,state.Migrated)=previous;throw;}
  finally{if(key!=null)CryptographicOperations.ZeroMemory(key);if(opened!=null)CryptographicOperations.ZeroMemory(opened);}
 }
}
