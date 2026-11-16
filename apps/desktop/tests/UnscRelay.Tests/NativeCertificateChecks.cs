using UnscRelay.Core;
static class NativeCertificateChecks
{
    public static int Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"relay certificate checks "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);int count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is IOException or InvalidOperationException){rejected=true;}Check(rejected,name);}
        try{
            Check(NativeCertificates.LoginUser("host fixture\nuser alice\nport 22\n")=="alice","Effective username read from original client configuration");
            Reject(()=>NativeCertificates.LoginUser("host fixture\n"),"Missing effective username rejected");
            Reject(()=>NativeCertificates.LoginUser("user alice\nuser bob\n"),"Ambiguous effective username rejected");
            Reject(()=>NativeCertificates.LoginUser("user alice\ncertificatefile /another/certificate.pub\n"),"Additional certificate source rejected instead of fallback");
            Check(NativeCertificates.LoginUser("user alice\ncertificatefile none\n")=="alice","Explicit none allows the selected archive certificate");
            using var vault=new KeyVault(Path.Combine(root,"vault"));vault.Unlock("Native certificate fixture password");
            var key=KeyGeneration.Generate(vault,"User","ed25519");var ca=KeyGeneration.Generate(vault,"CA","ed25519");key=vault.IssueCertificate(key.Id,ca.Id,"alice",1);
            var cert=key.Certificate!;vault.ValidateCertificateAccess(cert,"alice");Check(NativeCertificates.Algorithms(cert,"default")=="ssh-ed25519-cert-v01@openssh.com","Certificate-only algorithm selected");
            Reject(()=>NativeCertificates.Algorithms(cert,"ssh-mldsa44-ed25519"),"Incompatible signature requirement rejected");
            Reject(()=>vault.ValidateCertificateAccess(cert,"bob"),"Wrong login principal rejected");
            Reject(()=>vault.ValidateCertificateAccess(cert with{ValidUntil=DateTimeOffset.UtcNow.AddMinutes(-1)},"alice"),"Expired certificate rejected");
            Reject(()=>vault.ValidateCertificateAccess(cert with{ValidFrom=DateTimeOffset.UtcNow.AddHours(1)},"alice"),"Not-yet-valid certificate rejected");
            var path=Path.Combine(root,"public certificate.pub");File.WriteAllText(path,cert.Text);
            using var launch=LocalLaunch.Direct(Environment.ProcessPath!,["fixture"]);NativeCertificates.Apply(launch,path,"alice",NativeCertificates.Algorithms(cert,"default"));
            Check(launch.Arguments.Contains("CertificateFile=\""+NativeClient.CygwinPath(path)+"\"")&&launch.Arguments.Contains("PubkeyAcceptedAlgorithms=ssh-ed25519-cert-v01@openssh.com"),"Public certificate path quoted inside OpenSSH option; no raw key algorithms");
            vault.RevokeCertificate(key.Id,"Fixture revoked");Reject(()=>vault.ValidateCertificateAccess(cert,"alice"),"Revoked certificate blocked by the same vault registry");
            vault.Lock();Reject(()=>vault.ValidateCertificateAccess(cert,"alice"),"Locked archive cannot authorize certificate use");
            Console.WriteLine(count+" native certificate checks passed");return 0;
        }finally{Directory.Delete(root,true);}
    }
}
