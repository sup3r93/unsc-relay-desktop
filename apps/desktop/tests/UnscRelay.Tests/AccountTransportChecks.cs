using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using UnscRelay.Core;
static class AccountTransportChecks
{
    sealed class Server(string owner,byte[] half):HttpMessageHandler
    {
        public bool Offline;public HttpStatusCode Status=HttpStatusCode.OK;public int Requests;public bool WrongSize;public bool Malformed;public List<string> Paths=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requests++;Paths.Add(request.RequestUri!.AbsolutePath);
            if(Offline)throw new HttpRequestException("Fixture offline");
            if(request.RequestUri.Host!="unsc.responda.eu"||request.RequestUri.Scheme!="https"||request.Headers.Authorization?.Parameter!="fixture-access")throw new Exception("Invalid authenticated request");
            if(Status!=HttpStatusCode.OK)return Task.FromResult(new HttpResponseMessage(Status));
            object value=request.RequestUri.AbsolutePath=="/api/account"?new OnlineIdentity(owner,"fixture-subject","Fixture user"):new{secret=Malformed?"invalid-base64":Convert.ToBase64String(WrongSize?new byte[3]:half)};
            return Task.FromResult(new HttpResponseMessage(Status){Content=JsonContent.Create(value,options:VaultSyncCrypto.Json)});
        }
    }
    public static async Task<int> Run()
    {
        var directory=Path.Combine(Path.GetTempPath(),"relay-account-http-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var half=RandomNumberGenerator.GetBytes(32);var owner=new string('C',64);var device=Guid.NewGuid();var count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        try
        {
            var store=new AccountSyncStore(directory);store.Save(new(){Owner=owner,Subject="fixture-subject",Name="Fixture",Email="fixture@example.invalid",AccessToken="fixture-access",TokenExpires=DateTimeOffset.UtcNow.AddHours(1),Activated=true});
            Check(CloudAccountClient.RememberedEmail(new AccountSyncState{Name="legacy@example.invalid"})=="legacy@example.invalid","Legacy email-only account names provide a login hint without migration");
            Check(CloudAccountClient.RememberedEmail(new AccountSyncState{Name="Display name",Email="bad\r\nvalue"})=="","Invalid or multiline remembered metadata cannot become a login hint");
            var server=new Server(owner,half);using var client=new CloudAccountClient(directory,server);
            await client.VerifyOnline(CancellationToken.None);Check(server.Paths.Last()=="/api/account","Online access validates the authenticated personal identity");
            var remote=await client.DeviceUnlock(device,true,CancellationToken.None);Check(remote.SequenceEqual(half),"Online device enrollment obtains a separate server key half");
            var vaultPath=Path.Combine(directory,"keys.vault");using var vault=new KeyVault(vaultPath);vault.Unlock("Legacy migration fixture only");var id=vault.StorePassword("fixture","test secret");
            var unlocks=new AccountUnlockStore(directory);vault.MigrateAccountUnlock(unlocks,owner,device,remote);CryptographicOperations.ZeroMemory(remote);vault.Lock();
            var bytes=File.ReadAllBytes(vaultPath);using var binding=unlocks.Read(owner)!;
            remote=await client.DeviceUnlock(device,false,CancellationToken.None);vault.UnlockAccount(binding,remote,owner);CryptographicOperations.ZeroMemory(remote);
            Check(vault.ReadPassword(id)=="test secret","Fresh online access opens the migrated archive without its old password");vault.Lock();
            server.Offline=true;try{await client.DeviceUnlock(device,false,CancellationToken.None);throw new Exception("Offline network accepted");}catch(HttpRequestException){Check(!vault.IsUnlocked&&bytes.SequenceEqual(File.ReadAllBytes(vaultPath)),"Network failure keeps archive locked and unchanged");}
            Check(!unlocks.HelloAllowed(owner,"any-key"),"No offline method is silently enabled after online migration");
            server.Offline=false;server.Status=HttpStatusCode.Forbidden;try{await client.DeviceUnlock(device,false,CancellationToken.None);throw new Exception("Revoked online accepted");}catch(OnlineAccessException){Check(!vault.IsUnlocked,"Server denial cannot unlock the archive");}
            server.Status=HttpStatusCode.NotFound;try{await client.DeviceUnlock(device,false,CancellationToken.None);throw new Exception("Missing half accepted");}catch(OnlineAccessException){Check(bytes.SequenceEqual(File.ReadAllBytes(vaultPath)),"Missing remote half never overwrites local encrypted data");}
            var beforeInvalidDevice=server.Requests;try{await client.DeviceUnlock(Guid.Empty,true,CancellationToken.None);throw new Exception("Empty device accepted");}catch(ArgumentException){Check(server.Requests==beforeInvalidDevice,"Empty device refused before any network or enrollment request");}
            try{await client.DeviceUnlock(device,true,CancellationToken.None);throw new Exception("Missing API accepted");}catch(OnlineAccessException ex){Check(ex.Message.Contains("non aggiornato")&&bytes.SequenceEqual(File.ReadAllBytes(vaultPath)),"Missing enrollment API has an actionable service error and preserves the archive");}
            foreach(var status in new[]{HttpStatusCode.BadGateway,HttpStatusCode.ServiceUnavailable,HttpStatusCode.GatewayTimeout}){server.Status=status;try{await client.DeviceUnlock(device,false,CancellationToken.None);throw new Exception("Unavailable service accepted");}catch(OnlineAccessException ex){Check(ex.Message.Contains("temporaneamente")&&!vault.IsUnlocked&&bytes.SequenceEqual(File.ReadAllBytes(vaultPath)),"Temporary service failure is safe and actionable: "+(int)status);}}
            server.Status=HttpStatusCode.OK;server.Malformed=true;try{await client.DeviceUnlock(device,false,CancellationToken.None);throw new Exception("Malformed half accepted");}catch(CryptographicException){Check(!vault.IsUnlocked&&bytes.SequenceEqual(File.ReadAllBytes(vaultPath)),"Malformed online half produces a safe error without changing the archive");}server.Malformed=false;
            server.Status=HttpStatusCode.OK;server.WrongSize=true;try{await client.DeviceUnlock(device,false,CancellationToken.None);throw new Exception("Invalid half accepted");}catch(CryptographicException){Check(!vault.IsUnlocked,"Invalid remote key size fails closed");}server.WrongSize=false;
            remote=await client.DeviceUnlock(device,false,CancellationToken.None);vault.UnlockAccount(binding,remote,owner);CryptographicOperations.ZeroMemory(remote);
            using var rsa=RSA.Create(2048);var enrollment=vault.PrepareWindowsHello() with{PublicKey=Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())};
            var signature=rsa.SignData(System.Text.Encoding.UTF8.GetBytes(enrollment.Message),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);vault.EnableWindowsHelloForUnlockedVault(enrollment,signature);unlocks.ConfirmHello(owner,enrollment.KeyName);vault.Lock();
            Check(unlocks.HelloAllowed(owner,enrollment.KeyName),"Offline enrollment requires explicit consent for this archive and Hello key");
            server.Offline=true;var requests=server.Requests;var challenge=vault.WindowsHello()!;var proof=rsa.SignData(System.Text.Encoding.UTF8.GetBytes(challenge.Message),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);vault.UnlockWindowsHello(challenge,proof);
            Check(vault.ReadPassword(id)=="test secret"&&server.Requests==requests,"Valid enrolled Hello proof opens archive offline with no network request");vault.Lock();
            proof[0]^=1;try{vault.UnlockWindowsHello(challenge,proof);throw new Exception("Bad proof accepted");}catch(CryptographicException){Check(!vault.IsUnlocked,"Invalid offline Hello proof leaves archive locked");}
            await client.LogoutAsync(CancellationToken.None);Check(!new AccountSyncStore(directory).Load().Activated,"Logout persists deactivation even without network");
            Check(new AccountSyncStore(directory).Load().Email=="fixture@example.invalid","Explicit logout retains only remembered email metadata alongside the locked local archive");
            Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory,"account-sync.dpapi"))).Contains("fixture-access"),"Account transport tokens remain protected at rest");
            Console.WriteLine("Account transport checks: "+count);return 0;
        }
        finally{CryptographicOperations.ZeroMemory(half);Directory.Delete(directory,true);}
    }
}
