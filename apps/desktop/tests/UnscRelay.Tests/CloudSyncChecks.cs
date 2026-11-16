using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnscRelay.Core;

static class CloudSyncChecks
{
    public static int Run()
    {
        var count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        void Reject(Action action,string name){try{action();throw new Exception("ACCEPTED "+name);}catch(Exception e)when(e is CryptographicException or InvalidDataException or ArgumentException or JsonException){Check(true,name);}}
        var root=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","cloud-sync-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        const string password="Cloud migration fixture only!";
        var owner=Convert.ToHexString(SHA256.HashData("issuer-fixture\nsubject-A"u8));var other=Convert.ToHexString(SHA256.HashData("issuer-fixture\nsubject-B"u8));
        using var source=new KeyVault(Path.Combine(root,"keys.vault"));source.Unlock(password);
        var info=KeyGeneration.Generate(source,"Portable fixture","ed25519");var id=source.StorePassword("Server fixture","Fixture server password");
        var key=RandomNumberGenerator.GetBytes(32);source.StoreSyncKey(owner,key);
        var data=new AppData{Profiles=[new ConnectionProfile{Id="profile-fixture",Name="Profile fixture",Host="fixture.invalid",Username="fixture",KeyId=info.Id,PasswordId=id}],Folders=["Connessioni","Fixture/Subfolder"],Machines=[new("machine-fixture","Fixture machine","fixture.invalid",22)],Snippets=[new("snippet-fixture","Fixture command","pwd")],Settings=new(ExternalEditor:"C:\\PrivateDevicePath\\editor.exe")};
        var plain=VaultSyncCrypto.Capture(source,data,[]);var snapshot=VaultSyncCrypto.Parse(plain);
        Check(snapshot.Records.GetArrayLength()==2,"all portable keys and passwords exported, device sync key excluded");
        Check(!Encoding.UTF8.GetString(plain).Contains("PrivateDevicePath"),"device editor preferences excluded");
        Check(snapshot.Data.Profiles[0].KeyId==info.Id&&snapshot.Data.Profiles[0].PasswordId==id,"profile credential identifiers preserved");
        var canonical=VaultSyncCrypto.Canonicalize(plain);Check(VaultSyncCrypto.Hash(canonical)==VaultSyncCrypto.Hash(plain),"canonical snapshot stable");
        var envelope=VaultSyncCrypto.Create(owner,password,key,plain);VaultSyncCrypto.Validate(envelope,owner);
        var encoded=JsonSerializer.Serialize(envelope,VaultSyncCrypto.Json);Check(!encoded.Contains(info.Name)&&!encoded.Contains("Fixture server password")&&!encoded.Contains(Convert.ToBase64String(key)),"online envelope contains no plaintext secrets or data key");
        var recovered=VaultSyncCrypto.UnlockKey(envelope,owner,password);Check(recovered.SequenceEqual(key),"online key unwrapped locally");
        var opened=VaultSyncCrypto.Open(envelope,owner,recovered);Check(opened.SequenceEqual(plain),"all snapshot bytes recovered");
        Reject(()=>VaultSyncCrypto.UnlockKey(envelope,owner,"Incorrect fixture password"),"wrong archive password rejected");
        Reject(()=>VaultSyncCrypto.Open(envelope,other,key),"different account cannot open envelope");
        var cipher=Convert.FromBase64String(envelope.Ciphertext);cipher[0]^=1;Reject(()=>VaultSyncCrypto.Open(envelope with{Ciphertext=Convert.ToBase64String(cipher)},owner,key),"tampered payload rejected");
        var second=VaultSyncCrypto.Seal(envelope,key,plain);Check(second.Iv!=envelope.Iv&&second.Ciphertext!=envelope.Ciphertext,"new nonce for every synchronized revision");
        using var target=new KeyVault(Path.Combine(root,"target.vault"));target.Unlock("Different local fixture password");target.StoreSyncKey(owner,key);target.ImportSyncRecords(snapshot.Records);
        Check(target.List().Single().Id==info.Id&&target.List().Single().PublicKey==info.PublicKey,"restored key retains identity and public key");
        var original=source.Read(info.Id);var copied=target.Read(info.Id);Check(original.SequenceEqual(copied),"private key bytes survive migration");CryptographicOperations.ZeroMemory(original);CryptographicOperations.ZeroMemory(copied);
        Check(target.ReadPassword(id)=="Fixture server password","password retains identifier and value");
        Check(target.SyncKey(owner)!.SequenceEqual(key),"device sync key retained during remote import");
        target.Lock();target.Unlock("Different local fixture password");Check(target.List().Count==1,"offline local vault remains usable without online identity service");
        Check(VaultSyncPolicy.Decide("base","base","remote")==SyncDecision.Pull,"remote-only changes pulled");
        Check(VaultSyncPolicy.Decide("base","local","base")==SyncDecision.Push,"offline-only changes uploaded after reconnection");
        Check(VaultSyncPolicy.Decide("base","local","remote")==SyncDecision.Conflict,"two changed copies never auto-overwrite");
        Check(VaultSyncPolicy.Decide("base","same","same")==SyncDecision.Unchanged,"identical concurrent copies converge");
        Check(VaultSyncPolicy.Decide("","existing-local","existing-online")==SyncDecision.Conflict,"first migration into a nonempty online vault needs a choice");
        File.WriteAllText(Path.Combine(root,"profiles.json"),"original profile fixture");
        var before=File.ReadAllBytes(Path.Combine(root,"keys.vault"));SyncLocalBackup.Create(root,true);File.WriteAllText(Path.Combine(root,"keys.vault"),"interrupted migration");File.WriteAllText(Path.Combine(root,"profiles.json"),"partial profile fixture");
        SyncLocalBackup.Recover(root);Check(File.ReadAllBytes(Path.Combine(root,"keys.vault")).SequenceEqual(before)&&File.ReadAllText(Path.Combine(root,"profiles.json"))=="original profile fixture","interrupted migration restores all original local files");
        Check(!File.Exists(Path.Combine(root,"sync-transaction.json")),"successful recovery clears transaction journal");
        var storage=new AccountSyncStore(root);Check(!storage.Load().Activated,"new installation requires initial account activation");
        storage.Save(new AccountSyncState{Activated=true,Owner=owner,Subject="fixture-subject",Name="Fixture",RefreshToken="fixture-refresh-token-not-real",AccessToken="fixture-access-token-not-real"});
        Check(storage.Load().Activated,"verified account activation persists for offline access");
        var file=File.ReadAllBytes(Path.Combine(root,"account-sync.dpapi"));Check(!Encoding.UTF8.GetString(file).Contains("fixture-refresh-token-not-real"),"refresh token protected for Windows account");
        var logoutRoot=Path.Combine(root,"logout");Directory.CreateDirectory(logoutRoot);
        using(var account=new CloudAccountClient(logoutRoot)){
            account.State.Owner=owner;account.State.Subject="fixture-admin";account.State.Activated=true;account.Save();
            account.LogoutAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(!account.State.Activated&&account.State.Owner=="","unmigrated wrong account can be unlinked without changing the local vault");
            account.State.Owner=owner;account.State.Subject="fixture-user";account.State.Activated=true;account.State.Migrated=true;account.State.Revision=2;account.Save();
            account.LogoutAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(!account.State.Activated&&account.State.Owner==owner&&account.State.Revision==2,"migrated archive remains bound to its original owner after logout");
        }
        file[file.Length/2]^=1;File.WriteAllBytes(Path.Combine(root,"account-sync.dpapi"),file);Reject(()=>storage.Load(),"tampered local activation does not unlock application");
        Directory.CreateDirectory("artifacts");File.WriteAllText(Path.Combine("artifacts","sync-interop.json"),JsonSerializer.Serialize(new{owner,password,envelope,plaintext=Convert.ToBase64String(plain)},VaultSyncCrypto.Json));
        foreach(var bytes in new[]{plain,canonical,key,recovered,opened})CryptographicOperations.ZeroMemory(bytes);
        Console.WriteLine(count+" cloud sync checks passed.");return 0;
    }
    public static int Interop()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine("artifacts","sync-js-interop.json")));var value=document.RootElement;
        var envelope=value.GetProperty("envelope").Deserialize<SyncEnvelope>(VaultSyncCrypto.Json)!;var password=value.GetProperty("password").GetString()!;
        var key=VaultSyncCrypto.UnlockKey(envelope,envelope.Owner,password);var bytes=VaultSyncCrypto.Open(envelope,envelope.Owner,key);
        try{var snapshot=VaultSyncCrypto.Parse(bytes);if(snapshot.Data.Folders.Single()!="Browser fixture")throw new Exception("Browser snapshot differs");Console.WriteLine("PASS browser-encrypted vault opened by desktop");return 0;}finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(bytes);}
    }
}
