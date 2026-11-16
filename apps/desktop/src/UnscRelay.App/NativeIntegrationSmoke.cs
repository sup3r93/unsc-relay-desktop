using System.IO;
using System.Text;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class MainWindow
{
    async Task CheckNativeIntegration()
    {
        var checks = new Dictionary<string, bool>(); string? error = null; string id = Guid.NewGuid().ToString("N");
        try
        {
            int port = int.Parse(Environment.GetEnvironmentVariable("RELAY_FILE_PORT")!);
            if (port is < 1 or > 65535) throw new IOException("Porta fixture non valida.");
            vault.Unlock("Native integration fixture archive password");
            var key = KeyGeneration.Generate(vault, "App fixture key", "ed25519");
            var publicHost = NativeIdentity.Parse(Environment.GetEnvironmentVariable("RELAY_FIXTURE_HOST_PUBLIC")!);
            hosts.Accept(new HostKey("127.0.0.1", port, publicHost.Algorithm, publicHost.Fingerprint[7..], DateTime.UtcNow, publicHost.PublicKey));
            var profile = new ConnectionProfile { Name = "Native app fixture", Protocol = "native", Authentication = "key", KeyId = key.Id, Native = new NativeOptions { Kind = "ssh", Target = "test@127.0.0.1" } };
            var config = Path.Combine(dataPath, "fixture-config"); File.WriteAllText(config, "Host *\n Port " + port + "\n"); profile.Native.ConfigFile = config; await NativeClient.Capture(Path.Combine(Path.GetDirectoryName(NativeClient.Find("ssh")!)!, "chmod.exe"), ["600", NativeClient.CygwinPath(config)]);
            var output = new StringBuilder(); await using (var terminal = new LocalTerminal())
            {
                terminal.Output += b => { lock (output) output.Append(Encoding.UTF8.GetString(b)); }; using var launch = await PrepareNative(profile, id,"echo NATIVE-MACRO-FIXTURE");
                checks["selectedIdentityUsesOwnedAgent"] = launch.Arguments.Contains("ForwardAgent=no") && launch.Arguments.Any(a => a.StartsWith("IdentityAgent="));
                checks["onlyPublicIdentityAndConfigOnDisk"] = Directory.GetFiles(nativePublicFolders[id]).Select(Path.GetFileName).Order().SequenceEqual(new[] { "config", "identity.pub" });
                await terminal.StartExecutable(launch, Path.Combine(dataPath, "launch"), 100, 30);
                for (int i = 0; i < 160; i++) { lock (output) if (output.ToString().Contains("UNSC loopback fixture")) break; await Task.Delay(50); }
                lock (output) { checks["vaultKeyAuthenticatesInRealConPty"] = output.ToString().Contains("UNSC loopback fixture"); if (!checks["vaultKeyAuthenticatesInRealConPty"]) error = output.ToString(); }
            }
            var macroTrace=Path.Combine(Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")!,"native-macro.txt");checks["nativeMacroArrivesAsAuthenticatedExec"]=File.Exists(macroTrace)&&File.ReadAllText(macroTrace)=="echo NATIVE-MACRO-FIXTURE\nexec \"${SHELL:-/bin/sh}\" -l";
            var certificateKey=KeyGeneration.Generate(vault,"Certificate fixture user","ed25519");var ca=KeyGeneration.Generate(vault,"Certificate fixture CA","ed25519");certificateKey=vault.IssueCertificate(certificateKey.Id,ca.Id,"test-cert",1);
            var certId="certificate fixture "+Guid.NewGuid().ToString("N");var certificateProfile=profile with{Id=Guid.NewGuid().ToString("N"),Name="Native certificate fixture",KeyId=certificateKey.Id,Native=profile.Native with{Target="test-cert@127.0.0.1"}};
            try{
                var certificateOutput=new StringBuilder();await using(var terminal=new LocalTerminal()){
                    terminal.Output+=b=>{lock(certificateOutput)certificateOutput.Append(Encoding.UTF8.GetString(b));};using var launch=await PrepareNative(certificateProfile,certId);
                    checks["certificateOnlyAlgorithms"]=launch.Arguments.Contains("PubkeyAcceptedAlgorithms=ssh-ed25519-cert-v01@openssh.com");
                    checks["onlyPublicCertificateWritten"]=Directory.GetFiles(nativePublicFolders[certId]).Select(Path.GetFileName).Order().SequenceEqual(new[]{"config","identity-cert.pub","identity.pub"});
                    await terminal.StartExecutable(launch,Path.Combine(dataPath,"certificate-launch"),100,30);
                    for(int i=0;i<200;i++){lock(certificateOutput)if(certificateOutput.ToString().Contains("UNSC loopback fixture"))break;await Task.Delay(50);}
                    lock(certificateOutput){checks["certificateAuthenticatesInRealConPty"]=certificateOutput.ToString().Contains("UNSC loopback fixture");if(!checks["certificateAuthenticatesInRealConPty"])error=certificateOutput.ToString();}
                }
                var trace=Path.Combine(Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")!,"certificate-auth.txt");checks["serverReceivedCertificateNotRawKey"]=File.Exists(trace)&&File.ReadAllLines(trace).All(line=>line=="ssh-ed25519-cert-v01@openssh.com");
            }finally{await EndNative(certId);}
            checks["publicCertificateCleaned"]=!nativePublicFolders.ContainsKey(certId);
            var sftpId="sftp certificate fixture "+Guid.NewGuid().ToString("N");
            try
            {
                var sftpOutput=new StringBuilder();await using var terminal=new LocalTerminal();
                terminal.Output+=b=>{lock(sftpOutput)sftpOutput.Append(Encoding.UTF8.GetString(b));};
                using var launch=await PrepareNative(certificateProfile with{Native=certificateProfile.Native with{Kind="sftp"}},sftpId);
                checks["sftpUsesUserOptionNotBandwidthFlag"]=launch.Arguments.Contains("User=test-cert")&&!launch.Arguments.Contains("-l");
                await terminal.StartExecutable(launch,Path.Combine(dataPath,"sftp-certificate-launch"),100,30);
                for(int i=0;i<200;i++){lock(sftpOutput)if(sftpOutput.ToString().Contains("sftp>"))break;await Task.Delay(50);}
                lock(sftpOutput){checks["sftpCertificateAuthenticatesInRealConPty"]=sftpOutput.ToString().Contains("sftp>");if(!checks["sftpCertificateAuthenticatesInRealConPty"])error=sftpOutput.ToString();}
            }finally{await EndNative(sftpId);}

            vault.RevokeCertificate(certificateKey.Id,"Fixture revocation");var rejected=false;var rejectedId=Guid.NewGuid().ToString("N");try{await PrepareNative(certificateProfile,rejectedId);}catch(InvalidOperationException){rejected=true;}finally{await EndNative(rejectedId);}checks["revokedNativeCertificateBlocked"]=rejected;
            StopBundledAgent(); checks["lockingRemovesOwnedAgent"] = bundledAgent == null; vault.Lock();
            await EndNative(id); checks["sessionPublicFilesAndBridgeCleaned"] = !nativePublicFolders.ContainsKey(id) && !nativeBridges.ContainsKey(id);
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally { StopBundledAgent(); await EndNative(id); }
        bool success = error == null && checks.Count == 14 && checks.Values.All(x => x);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "native-integration-smoke-result.json"), JsonSerializer.Serialize(new { success, checks, error }, ProfileStore.Json)); CompleteSmoke(success);
    }
}
