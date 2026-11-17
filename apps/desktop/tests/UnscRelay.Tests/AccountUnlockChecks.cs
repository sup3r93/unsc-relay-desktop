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
   using var helloRsa=RSA.Create(2048);
   var oldHello=vault.PrepareWindowsHello() with{PublicKey=Convert.ToBase64String(helloRsa.ExportSubjectPublicKeyInfo())};
   var oldSignature=helloRsa.SignData(System.Text.Encoding.UTF8.GetBytes(oldHello.Message),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
   vault.EnableWindowsHelloForUnlockedVault(oldHello,oldSignature);store.ConfirmHello(owner,oldHello.KeyName);
   var legacyBytes=File.ReadAllBytes(path);var legacyCapsule=File.ReadAllBytes(Path.Combine(directory,"account-unlock.dpapi"));
   Reject(()=>vault.MigrateAccountUnlock(store,owner,Guid.Empty,remote),"invalid migration rejected before writes");
   Check(legacyBytes.SequenceEqual(File.ReadAllBytes(path)),"invalid migration preserves original vault");
   vault.MigrateAccountUnlock(store,owner,restored.Device,remote);
   using var migrated=store.Read(owner)!;
   Check(migrated.Version==2&&migrated.HelloKey=="","migration rotates key and requires fresh Hello enrollment");
   Check(vault.WindowsHello()==null&&!store.HelloAllowed(owner,oldHello.KeyName),"legacy Hello wrapper and consent are retired together");
   Check(!legacyBytes.SequenceEqual(File.ReadAllBytes(path)),"migration rewrites encrypted payload with random key");
   vault.Lock();Reject(()=>vault.Unlock("Legacy fixture password only!"),"old password cannot unlock migrated vault");
   vault.UnlockAccount(migrated,remote,owner);
   Check(vault.ReadPassword(id)=="fixture-secret-value"&&vault.List().Single().Id==key.Id,"migration preserves secret content and SSH identities");
   var backups=Directory.GetDirectories(Path.Combine(directory,"MigrationBackups"));
   using(var recoveryVault=new KeyVault(Path.Combine(backups.Single(),"keys.vault"))){recoveryVault.Unlock("Legacy fixture password only!");Check(recoveryVault.ReadPassword(id)=="fixture-secret-value","encrypted recovery copy remains usable");}
   var migratedBytes=File.ReadAllBytes(path);var migratedCapsule=File.ReadAllBytes(Path.Combine(directory,"account-unlock.dpapi"));
   SyncLocalBackup.Create(directory,true);
   File.WriteAllBytes(path,legacyBytes);File.WriteAllBytes(Path.Combine(directory,"account-unlock.dpapi"),legacyCapsule);
   vault.Lock();SyncLocalBackup.Recover(directory);
   Check(migratedBytes.SequenceEqual(File.ReadAllBytes(path))&&migratedCapsule.SequenceEqual(File.ReadAllBytes(Path.Combine(directory,"account-unlock.dpapi"))),"interrupted transaction restores matching vault and device capsule");
   using var recovered=store.Read(owner)!;vault.UnlockAccount(recovered,remote,owner);
   Check(vault.Passwords().Count==2,"recovered account unlock preserves all passwords");
   Console.WriteLine($"PASS {count} account unlock checks");return 0;
  }finally{CryptographicOperations.ZeroMemory(remote);vault.Lock();if(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(directory,true);}
 }
}
