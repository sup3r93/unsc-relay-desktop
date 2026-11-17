using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
internal static class PersonalArchiveRecoveryChecks
{
 public static async Task<int> Run()
 {
  var root=Directory.CreateTempSubdirectory("relay-personal-recovery-");var count=0;var account=new string('A',64);var target=new SharedArchive(Guid.NewGuid(),"Personal fixture",account,new string('C',64),"owner",1,Personal:true);const string password="Verified recovery fixture 036!";
  void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS personal recovery: "+label);}
  try
  {
   var vault=new KeyVault(Path.Combine(root.FullName,"keys.vault"));vault.Unlock("Local fixture master 036!");var data=new AppData();var plain=VaultSyncCrypto.Capture(vault,data,[]);RemoteVault? remote=null;var writes=0;var failRemember=false;var failSave=false;
   var state=new AccountSyncState{Owner=account,ArchiveOwner=new string('B',64),Revision=99,Baseline="prior-baseline",Migrated=true};
   Task<RemoteVault?> Read()=>Task.FromResult(remote);
   Task<long?> Create(SyncEnvelope envelope){writes++;if(remote!=null)return Task.FromResult<long?>(null);remote=new(1,DateTime.UtcNow,envelope);return Task.FromResult<long?>(1);}
   void Remember(string owner,byte[] key){if(failRemember)throw new IOException("Injected local key failure");vault.StoreSyncKey(owner,key);}
   void Save(){if(failSave)throw new IOException("Injected checkpoint failure");new AccountSyncStore(root.FullName).Save(state);}
   try
   {
    failRemember=true;try{await PersonalArchiveRecovery.Commit(state,target,password,plain,Read,Create,Remember,Save);throw new Exception("Missing injected key failure");}catch(IOException){}
    Check(remote!=null&&writes==1,"remote CAS may succeed before a local key write fails");Check(state.ArchiveOwner==new string('B',64)&&state.Revision==99&&state.Baseline=="prior-baseline"&&state.Header==null,"failure restores all prior synchronization fields");
    failRemember=false;failSave=true;try{await PersonalArchiveRecovery.Commit(state,target,password,plain,Read,Create,Remember,Save);throw new Exception("Missing injected checkpoint failure");}catch(IOException){}
    Check(writes==1&&state.Revision==99&&state.Header==null,"failure after remembering the key still preserves prior checkpoint and does not upload again");
    failSave=false;await PersonalArchiveRecovery.Commit(state,target,password,plain,Read,Create,Remember,Save);
    Check(writes==1&&state.Revision==1&&state.ArchiveOwner==target.ArchiveOwner&&state.Owner==account,"retry completes the existing matching ciphertext without replacing it");
    Check(new AccountSyncStore(root.FullName).Load().ArchiveOwner==target.ArchiveOwner,"successful completion persists the recovered namespace protected at rest");
    var stored=remote;try{await PersonalArchiveRecovery.Commit(state,target,"Incorrect recovery phrase",plain,Read,Create,Remember,Save);throw new Exception("Wrong password accepted");}catch(CryptographicException){}
    Check(ReferenceEquals(remote,stored)&&writes==1,"wrong recovery password cannot overwrite existing ciphertext");
    var changed=VaultSyncCrypto.Capture(vault,new AppData{Folders=["Different verified local data"]},[]);
    try{try{await PersonalArchiveRecovery.Commit(state,target,password,changed,Read,Create,Remember,Save);throw new Exception("Different local data overwrote remote");}catch(InvalidOperationException){}Check(ReferenceEquals(remote,stored)&&writes==1,"different verified local data never replaces a populated online archive");}finally{CryptographicOperations.ZeroMemory(changed);}
    try{await PersonalArchiveRecovery.Commit(state,target with{Access="write"},password,plain,Read,Create,Remember,Save);throw new Exception("Member recovered ownership");}catch(InvalidOperationException){}Check(writes==1,"recovery requires the actual personal archive owner");
   }
   finally{CryptographicOperations.ZeroMemory(plain);vault.Lock();}
   Console.WriteLine("Personal archive recovery checks: "+count);return 0;
  }
  finally{Directory.Delete(root.FullName,true);}
 }
}
