using System.Security.Cryptography;
using UnscRelay.Core;
using Renci.SshNet;
using Renci.SshNet.Security;
using SshNet.Keygen;
static class NativeHostCertificateChecks
{
    public static int Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"relay-host-cert-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); int count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is IOException or CryptographicException or ArgumentException or OperationCanceledException){rejected=true;}Check(rejected,name);}
        try{
            using var vault=new KeyVault(Path.Combine(root,"vault"));vault.Unlock("Host certificate fixture password");
            var ca=KeyGeneration.Generate(vault,"CA","ed25519");var server=KeyGeneration.Generate(vault,"Server","ed25519");var otherCa=KeyGeneration.Generate(vault,"Other CA","ed25519");var renewed=KeyGeneration.Generate(vault,"Renewed server","ed25519");
            string Certificate(string signer,string subject,string host,DateTime begin,DateTime end,bool user=false,bool critical=false){
                var privateBytes=vault.Read(subject);var caBytes=vault.Read(signer);
                try{using var privateStream=new MemoryStream(privateBytes);using var caStream=new MemoryStream(caBytes);using var key=new PrivateKeyFile(privateStream);using var authority=new PrivateKeyFile(caStream);
                    var builder=new SshCertificateBuilder(key).WithType(user?SshCertificateType.User:SshCertificateType.Host).WithKeyId("host fixture").WithPrincipal(host).WithValidity(begin,end);
                    if(critical)builder.WithCriticalOption("fixture-option","value");
                    return builder.SignWith((KeyHostAlgorithm)authority.HostKeyAlgorithms.First(a=>a.Name!="ssh-rsa")).ToOpenSshPublicFormat();
                }finally{CryptographicOperations.ZeroMemory(privateBytes);CryptographicOperations.ZeroMemory(caBytes);}
            }
            var now=DateTime.UtcNow;string Valid(string signer,string subject)=>Certificate(signer,subject,"fixture.invalid",now.AddMinutes(-1),now.AddHours(1));
            var text=Valid(ca.Id,server.Id);var entry=NativeHostCertificates.Validate(text,"fixture.invalid",48221,DateTimeOffset.UtcNow);
            Check(entry.IsCertificateAuthority&&entry.PublicKey==ca.PublicKey&&entry.Fingerprint==ca.Fingerprint.Replace("SHA256:",""),"Validated host certificate identifies its CA");
            Reject(()=>NativeHostCertificates.Validate(text,"other.invalid",48221,DateTimeOffset.UtcNow),"Wrong host rejected");
            Reject(()=>NativeHostCertificates.Validate(Certificate(ca.Id,server.Id,"fixture.invalid",now.AddHours(-2),now.AddHours(-1)),"fixture.invalid",48221,DateTimeOffset.UtcNow),"Expired host certificate rejected");
            Reject(()=>NativeHostCertificates.Validate(Certificate(ca.Id,server.Id,"fixture.invalid",now.AddHours(1),now.AddHours(2)),"fixture.invalid",48221,DateTimeOffset.UtcNow),"Future host certificate rejected");
            Reject(()=>NativeHostCertificates.Validate(Certificate(ca.Id,server.Id,"fixture.invalid",now.AddMinutes(-1),now.AddHours(1),user:true),"fixture.invalid",48221,DateTimeOffset.UtcNow),"User certificate cannot authorize a host");
            Reject(()=>NativeHostCertificates.Validate(Certificate(ca.Id,server.Id,"fixture.invalid",now.AddMinutes(-1),now.AddHours(1),critical:true),"fixture.invalid",48221,DateTimeOffset.UtcNow),"Unknown critical option rejected");
            var pieces=text.Split(' ');var bytes=Convert.FromBase64String(pieces[1]);bytes[^1]^=1;var forged=pieces[0]+" "+Convert.ToBase64String(bytes);
            Reject(()=>NativeHostCertificates.Validate(forged,"fixture.invalid",48221,DateTimeOffset.UtcNow),"Tampered CA signature rejected");
            var path=Path.Combine(root,"hosts.json");var store=new HostKeyStore(path);int prompts=0;
            NativeRequest Request(string certificate,string port="48221") {var p=certificate.Split(' ');return new("","host",["HOSTNAME","fixture.invalid",port,"[fixture.invalid]:"+port,p[0],p[1]]);}
            var line=NativeIdentity.KnownHost(Request(text),store,k=>{prompts++;return k.IsCertificateAuthority;});
            Check(prompts==1&&line=="@cert-authority [fixture.invalid]:48221 "+ca.PublicKey,"First CA trust is explicit and endpoint-scoped");
            NativeIdentity.KnownHost(Request(Valid(ca.Id,renewed.Id)),store,_=>throw new Exception("Renewal prompted"));
            Check(store.List().Count==1,"Renewed server key reuses only the trusted CA");
            Reject(()=>NativeIdentity.KnownHost(Request(Valid(otherCa.Id,server.Id)),store,_=>throw new Exception("Changed CA prompted")),"Changed authority rejected without a new trust prompt");
            Check(new HostKeyStore(path).List().Single().IsCertificateAuthority,"CA trust survives reloading the host manager");
            Check(store.Check("fixture.invalid",48221,ca.Algorithm,ca.Fingerprint)==HostKeyState.Changed,"CA pin never authorizes an ordinary host key");
            Reject(()=>NativeIdentity.KnownHost(Request(text,"48222"),store,_=>false),"Different port requires separate trust");
            Reject(()=>NativeIdentity.KnownHost(Request(text) with{Arguments=["HOSTNAME","fixture.invalid","48221","*.invalid",pieces[0],pieces[1]]},store,_=>true),"Wildcard lookup cannot widen CA trust");
            var order=Request(text) with{Arguments=["ORDER","fixture.invalid","48221","[fixture.invalid]:48221","","" ]};
            Check(NativeIdentity.KnownHost(order,store,_=>false)==line,"Algorithm ordering retains the endpoint-scoped CA marker");
            var snapshot=VaultSyncCrypto.Parse(VaultSyncCrypto.Capture(vault,new AppData(),store.List()));
            Check(snapshot.HostKeys.Single().IsCertificateAuthority,"Archive synchronization preserves the CA trust type");
            var unknown=new HostKeyStore(Path.Combine(root,"unknown.json"));
            Reject(()=>NativeIdentity.KnownHost(Request(forged),unknown,_=>throw new Exception("Invalid signature prompted")),"Invalid certificate never reaches trust confirmation");
            Check(unknown.List().Count==0,"Invalid certificate does not persist a CA");
            var userText=Certificate(ca.Id,server.Id,"alice",now.AddMinutes(-1),now.AddHours(1),user:true);
            var userParts=userText.Split(' ');var userBytes=Convert.FromBase64String(userParts[1]);userBytes[^1]^=1;
            Reject(()=>vault.AttachCertificate(server.Id,userParts[0]+" "+Convert.ToBase64String(userBytes)),"Forged user certificate cannot be attached to a saved key");
            Check(vault.List().Single(k=>k.Id==server.Id).Certificate==null,"Failed certificate import preserves the original key");
            store.Forget("fixture.invalid",48221);Check(store.List().Count==0,"Forgetting endpoint revokes its CA trust");
            Console.WriteLine(count+" native host certificate checks passed");return 0;
        }finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
    }
}
