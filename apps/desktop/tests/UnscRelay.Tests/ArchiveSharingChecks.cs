using System.Security.Cryptography;
using UnscRelay.Core;

internal static class ArchiveSharingChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
        void Deny(Action action,string label){try{action();}catch(Exception ex)when(ex is CryptographicException or FormatException){Check(true,label);return;}throw new Exception("Accepted: "+label);}
        var archive=Guid.NewGuid();var device=Guid.NewGuid();var owner=new string('A',64);var account=new string('B',64);
        var keys=ArchiveKeyCrypto.Generate();var other=ArchiveKeyCrypto.Generate();var secret=RandomNumberGenerator.GetBytes(32);
        try
        {
            var grant=ArchiveKeyCrypto.Wrap(archive,owner,account,device,keys.PublicKey,secret);
            var restored=ArchiveKeyCrypto.Unwrap(grant,archive,owner,account,device,keys.PrivateKey);
            try{Check(restored.SequenceEqual(secret),"Authorised device recovers the exact archive data key");}finally{CryptographicOperations.ZeroMemory(restored);}
            Check(!grant.WrappedKey.Contains(Convert.ToBase64String(secret)),"Grant does not contain a plaintext archive key");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant,archive,owner,account,device,other.PrivateKey),"Another device private key cannot decrypt a grant");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant,Guid.NewGuid(),owner,account,device,keys.PrivateKey),"Grant cannot be replayed for another archive");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant,archive,owner,new string('C',64),device,keys.PrivateKey),"Grant cannot be replayed for another account");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant,archive,owner,account,Guid.NewGuid(),keys.PrivateKey),"Grant cannot be replayed for another device ID");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant with {ArchiveOwner=new string('D',64)},archive,new string('D',64),account,device,keys.PrivateKey),"Changing public owner metadata cannot change the encrypted owner binding");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant with {ArchiveId=Guid.NewGuid()},grant.ArchiveId,owner,account,device,keys.PrivateKey),"Conflicting archive metadata is rejected");
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant with {Version=2},archive,owner,account,device,keys.PrivateKey),"Unknown key-grant versions are rejected");
            var corrupted=Convert.FromBase64String(grant.WrappedKey);corrupted[5]^=1;
            Deny(()=>ArchiveKeyCrypto.Unwrap(grant with {WrappedKey=Convert.ToBase64String(corrupted)},archive,owner,account,device,keys.PrivateKey),"Modified wrapped key is rejected");
            Deny(()=>ArchiveKeyCrypto.Wrap(archive,owner,account,device,keys.PublicKey,new byte[16]),"Invalid archive key length is rejected");
            Deny(()=>ArchiveKeyCrypto.Wrap(Guid.Empty,owner,account,device,keys.PublicKey,secret),"Empty archive ID is rejected");
            Deny(()=>ArchiveKeyCrypto.Wrap(archive,owner,account,Guid.Empty,keys.PublicKey,secret),"Empty device ID is rejected");
            using var shortKey=RSA.Create(2048);
            Deny(()=>ArchiveKeyCrypto.Wrap(archive,owner,account,device,Convert.ToBase64String(shortKey.ExportSubjectPublicKeyInfo()),secret),"Unsupported RSA key size is rejected");
            using var nonstandard=RSA.Create();using var sourcePublic=RSA.Create();sourcePublic.ImportSubjectPublicKeyInfo(Convert.FromBase64String(keys.PublicKey),out _);var parameters=sourcePublic.ExportParameters(false);parameters.Exponent=[3];nonstandard.ImportParameters(parameters);
            Deny(()=>ArchiveKeyCrypto.Wrap(archive,owner,account,device,Convert.ToBase64String(nonstandard.ExportSubjectPublicKeyInfo()),secret),"Nonstandard RSA public exponent is rejected");
            var trailing=keys.PrivateKey.Concat(new byte[]{0}).ToArray();
            try{Deny(()=>ArchiveKeyCrypto.Unwrap(grant,archive,owner,account,device,trailing),"Trailing private-key bytes are rejected");}finally{CryptographicOperations.ZeroMemory(trailing);}
            var publicTrailing=Convert.FromBase64String(keys.PublicKey).Concat(new byte[]{0}).ToArray();
            Deny(()=>ArchiveKeyCrypto.Wrap(archive,owner,account,device,Convert.ToBase64String(publicTrailing),secret),"Trailing public-key bytes are rejected");
            var directory=Directory.CreateTempSubdirectory("relay-sharing-device-");
            try
            {
                var path=Path.Combine(directory.FullName,"keys.vault");using var vault=new KeyVault(path);vault.Unlock("Synthetic account vault password");
                using var identity=vault.SharingDevice(account);using var repeated=vault.SharingDevice(account);using var second=vault.SharingDevice(owner);
                Check(identity.Id==repeated.Id&&identity.PrivateKey.SequenceEqual(repeated.PrivateKey),"The same account retains its encrypted device identity across reads");
                Check(identity.Id!=second.Id&&!identity.PrivateKey.SequenceEqual(second.PrivateKey),"Different accounts cannot share a device private key");
                var exported=vault.ExportSyncRecords();try{Check(!System.Text.Encoding.UTF8.GetString(exported).Contains("archive-device")&&!System.Text.Encoding.UTF8.GetString(exported).Contains(Convert.ToBase64String(identity.PrivateKey)),"Device private keys never enter a shared archive snapshot");}finally{CryptographicOperations.ZeroMemory(exported);}
                using(var records=System.Text.Json.JsonDocument.Parse("[]"))vault.ImportSyncRecords(records.RootElement);
                using var retained=vault.SharingDevice(account,false);Check(retained.Id==identity.Id,"Receiving a shared snapshot cannot replace a local device identity");
                Check(!File.ReadAllText(path).Contains(Convert.ToBase64String(identity.PrivateKey)),"The device private key is encrypted at rest inside the account vault");
                vault.Lock();try{using var forbidden=vault.SharingDevice(account,false);throw new Exception("Locked device key was readable");}catch(InvalidOperationException){Check(true,"Locking the account prevents retrieval of its device private key");}
                vault.Unlock("Synthetic account vault password");using var reopened=vault.SharingDevice(account,false);Check(reopened.Id==identity.Id&&reopened.PrivateKey.SequenceEqual(identity.PrivateKey),"The encrypted device identity survives a vault lock and reopen");
                var descriptor=new SharedArchive(archive,"Offline fixture",account,owner,"write",1);vault.RememberArchive(account,descriptor);
                Check(vault.RememberedArchives(account).Single().Id==archive&&vault.RememberedArchives(owner).Count==0,"offline archive references stay bound to their local account");
                var plain=System.Text.Encoding.UTF8.GetBytes("synthetic encrypted payload");SyncEnvelope envelope;
                try{envelope=VaultSyncCrypto.Create(owner,"Synthetic archive recovery",secret,plain);}finally{CryptographicOperations.ZeroMemory(plain);}
                var copies=new ArchiveCopies(directory.FullName,account);copies.Save(descriptor,new(3,DateTime.UtcNow,envelope));Check(copies.Read(descriptor) is {Revision:3,Pending:false},"an encrypted archive copy retains its verified revision");
                copies.Save(descriptor,new(3,DateTime.UtcNow,envelope),true);copies.Save(descriptor,new(4,DateTime.UtcNow,envelope));Check(copies.Read(descriptor) is{Revision:3,Pending:true},"an online refresh never overwrites pending offline changes");
                copies.Save(descriptor,new(4,DateTime.UtcNow,envelope),replacePending:true);Check(copies.Read(descriptor) is{Revision:4,Pending:false},"explicit successful publication can replace the pending copy");
                try{new ArchiveCopies(directory.FullName,owner).Read(descriptor);throw new Exception("Foreign cache accepted");}catch(InvalidDataException){Check(true,"another account cannot open the cached envelope metadata");}
                try{copies.Read(descriptor with{ArchiveOwner=new string('D',64)});throw new Exception("Foreign namespace accepted");}catch(Exception ex)when(ex is InvalidDataException or CryptographicException){Check(true,"cache metadata cannot change the encrypted archive namespace");}
                var backup=UpdateBackup.Create(directory.FullName,"synthetic");Check(File.Exists(Path.Combine(backup,"ArchiveCopies",archive.ToString("N")+".json")),"update backups include additional encrypted archive copies");
                var exportedAfterCache=vault.ExportSyncRecords();try{Check(!System.Text.Encoding.UTF8.GetString(exportedAfterCache).Contains("archive-reference"),"offline archive references never leak into another shared snapshot");}finally{CryptographicOperations.ZeroMemory(exportedAfterCache);}
                var held=reopened.PrivateKey;reopened.Dispose();Check(held.All(b=>b==0),"Disposing a device key zeroes its temporary private-key copy");
            }
            finally{directory.Delete(true);}
            Console.WriteLine("Archive key transport checks: "+count);return 0;
        }
        finally{CryptographicOperations.ZeroMemory(keys.PrivateKey);CryptographicOperations.ZeroMemory(other.PrivateKey);CryptographicOperations.ZeroMemory(secret);}
    }
}
