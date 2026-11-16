using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
static class AccountUnlockChecks
{
 public static int Run(){
  var count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
  void Reject(Action action,string name){try{action();throw new Exception("ACCEPTED "+name);}catch(Exception ex)when(ex is CryptographicException or InvalidDataException or ArgumentException){Check(true,name);}}
  var directory=Path.Combine(Path.GetTempPath(),"relay-account-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  var path=Path.Combine(directory,"keys.vault");var owner=new string('A',64);var other=new string('B',64);
  var remote=RandomNumberGenerator.GetBytes(32);using var vault=new KeyVault(path);
  try{
   vault.Unlock("Legacy fixture password only!");var id=vault.StorePassword("fixture","fixture-secret-value");var key=KeyGeneration.Generate(vault,"fixture SSH","ed25519");
   var before=File.ReadAllBytes(path);using var binding=vault.BindAccount(owner,Guid.NewGuid(),remote);
   Check(before.SequenceEqual(File.ReadAllBytes(path)),"binding never rewrites existing encrypted vault");
   var store=new AccountUnlockStore(directory);store.Save(binding);using var restored=store.Read(owner)!;
   Check(restored.Device==binding.Device,"DPAPI capsule survives restart");
   Check(!store.HelloAllowed(owner,"existing-hello-key"),"existing Hello does not imply consent");
   vault.Lock();vault.UnlockAccount(restored,remote,owner);
   Check(vault.ReadPassword(id)=="fixture-secret-value"&&vault.List().Single().Id==key.Id,"password and SSH key identities preserved");
   vault.StorePassword("second","second-value");vault.Lock();vault.UnlockAccount(restored,remote,owner);
   Check(vault.Passwords().Count==2,"capsule opens later revisions without re-enrollment");
   var current=File.ReadAllBytes(path);var bad=remote.ToArray();bad[0]^=1;
   Reject(()=>vault.UnlockAccount(restored,bad,owner),"wrong server half denied");Check(!vault.IsUnlocked,"failed decryption leaves vault locked");
   Reject(()=>vault.UnlockAccount(restored,remote,other),"different account denied");
   Reject(()=>vault.UnlockAccount(restored with{Device=Guid.NewGuid()},remote,owner),"different device denied");
   var corrupted=restored.Ciphertext.ToArray();corrupted[0]^=1;Reject(()=>vault.UnlockAccount(restored with{Ciphertext=corrupted},remote,owner),"tampered wrapping rejected");
   Reject(()=>vault.UnlockAccount(restored with{Salt=null!},remote,owner),"null packet fields rejected");
   Reject(()=>store.Read(other),"DPAPI packet cannot be reassigned to another account");
   Check(current.SequenceEqual(File.ReadAllBytes(path)),"every failed unlock preserves exact vault bytes");
   store.ConfirmHello(owner,"hello-key-1");Check(store.HelloAllowed(owner,"hello-key-1")&&!store.HelloAllowed(owner,"hello-key-2"),"manual Hello consent bound to exact key");
   store.ConfirmHello(owner,"");Check(!store.HelloAllowed(owner,"hello-key-1"),"disabling consent removes offline unlock");
   vault.UnlockAccount(restored,remote,owner);Check(vault.ReadPassword(id)=="fixture-secret-value","online remains available after disabling Hello");
   vault.Lock();vault.Unlock("Legacy fixture password only!");Check(vault.List().Single().Id==key.Id,"legacy recovery retained without data destruction");
   Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory,"account-unlock.dpapi"))).Contains("fixture-secret-value"),"device capsule contains no plaintext vault secrets");
   Console.WriteLine($"PASS {count} account unlock checks");return 0;
  }finally{CryptographicOperations.ZeroMemory(remote);vault.Lock();Directory.Delete(directory,true);}
 }
}
