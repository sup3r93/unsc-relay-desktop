using System.Security.Cryptography;
using UnscRelay.Core;
internal static class ConversionChecks
{
    public static int Run(string fixture)
    {
        var root=Path.GetFullPath(fixture);var original=Path.Combine(root,"original.key");var ppk=Path.Combine(root,"converted.ppk");var converted=Path.Combine(root,"roundtrip.openssh");const string pass="relay-conversion-fixture-2026";int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        void Reject(Action fn,string name){bool rejected=false;try{fn();}catch(Exception ex)when(ex is IOException or ArgumentException or Renci.SshNet.Common.SshException){rejected=true;}Check(rejected,name);}
        var sourceBytes=File.ReadAllBytes(original);var old=KeyConversion.Inspect(sourceBytes,"");var result=KeyConversion.Inspect(File.ReadAllBytes(ppk),pass);var modern=KeyConversion.Inspect(File.ReadAllBytes(converted),pass);
        Check(old.Algorithm=="ssh-ed25519"&&old.Format=="openssh","OpenSSH original validated by existing SSH parser");
        Check(result.Format=="ppk3"&&result.Fingerprint==old.Fingerprint,"Official PuTTYgen encrypted PPK preserves public identity");
        Check(modern.Format=="openssh"&&modern.Fingerprint==old.Fingerprint,"Official PuTTYgen encrypted OpenSSH preserves identity");
        Reject(()=>KeyConversion.Inspect(File.ReadAllBytes(ppk),""),"Encrypted PPK requires passphrase");Reject(()=>KeyConversion.Inspect(File.ReadAllBytes(ppk),"wrong"),"Wrong PPK passphrase rejected");Reject(()=>KeyConversion.Inspect(File.ReadAllBytes(converted),"wrong"),"Wrong OpenSSH passphrase rejected");
        using(var conversion=new KeyConversion(original,"ppk",""))
        {
            Reject(()=>{using var write=new FileStream(original,FileMode.Open,FileAccess.Write);},"Original protected against writes while conversion active");
            var info=conversion.VerifyResult(ppk,pass,out var bytes);try{Check(info.Fingerprint==old.Fingerprint&&bytes.Length>0,"PPK output verified before import");}finally{CryptographicOperations.ZeroMemory(bytes);}
            Reject(()=>conversion.VerifyResult(original,"",out _),"Same source/output path refused");Reject(()=>conversion.VerifyResult(converted,pass,out _),"Incorrect selected output format refused");
        }
        using(var writable=new FileStream(original,FileMode.Open,FileAccess.Write)){Check(writable.CanWrite,"Source lock released on disposal");}
        using(var conversion=new KeyConversion(ppk,"openssh",pass))
        {
            conversion.VerifyResult(converted,pass,out var bytes);
            var vaultPath=Path.Combine(root,"conversion-fixture.vault");using var vault=new KeyVault(vaultPath);vault.Unlock("Conversion test vault only 2026!");
            try{var key=vault.Import("Converted fixture",bytes,pass,"Custom comment");Check(key.Fingerprint==old.Fingerprint&&key.Name=="Converted fixture"&&key.Comment=="Custom comment","Verified conversion imports with chosen name and comment");}finally{CryptographicOperations.ZeroMemory(bytes);}
            Reject(()=>conversion.VerifyResult(Path.Combine(root,"other.key"),"",out _),"Unrelated private key rejected without import");
        }
        Check(File.ReadAllBytes(original).SequenceEqual(sourceBytes),"Original bytes unchanged throughout roundtrip");
        var tool=Path.Combine(Directory.GetCurrentDirectory(),"src","UnscRelay.App","Tools","PuTTYgen","puttygen.exe");Check(KeyConversion.VerifyTool(tool)==Path.GetFullPath(tool),"Official pinned PuTTYgen binary accepted");
        var fake=Path.Combine(root,"fake-tool.exe");File.WriteAllText(fake,"not executable");Reject(()=>KeyConversion.VerifyTool(fake),"Different executable rejected before launch");
        Reject(()=>new KeyConversion(original,"invalid",""),"Unsupported format rejected");Reject(()=>KeyConversion.Inspect([],""),"Empty file rejected");
        Console.WriteLine($"{count} conversion checks passed");return 0;
    }
}
