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
            var managedId=Guid.NewGuid().ToString("N");
            var managed=profile with{Id=Guid.NewGuid().ToString("N"),Native=profile.Native with{ConfigFile="",Port=port},Hooks=[new("after-connect","remote","fixture-ok",[],true)]};
            data.Profiles.Add(managed);
            try
            {
                await ConnectLocal(managed,managedId,JsonSerializer.SerializeToElement(new{cols=100,rows=30}));
                for(int i=0;i<240&&!nativeAuthenticated.Contains(managedId);i++)await Task.Delay(50);
                checks["managedNativeAuthenticationSignal"]=nativeAuthenticated.Contains(managedId);
                var originalPublicFolder=nativePublicFolders[managedId];
                var browserId=Guid.NewGuid().ToString("N");
                await StorageAction("storageNativeOpen",JsonSerializer.SerializeToElement(new{sessionId=browserId,sourceSessionId=managedId,profileId=managed.Id}));
                checks["managedNativeSftpChannel"]=storageSessions.ContainsKey(browserId);
                var advancedId=Guid.NewGuid().ToString("N");
                await OpenNativeFileWorkspace(JsonSerializer.SerializeToElement(new{sessionId=advancedId,sourceSessionId=managedId,profileId=managed.Id}));
                checks["nativeAdvancedWorkspaceConnected"]=sessions[advancedId].FilesAvailable;
                var input=Path.Combine(dataPath,"native-queue.txt");await File.WriteAllTextAsync(input,"native queue original\r\n",new UTF8Encoding(false));
                EnqueueFiles(JsonSerializer.SerializeToElement(new{sessionId=advancedId,direction="upload",conflict="new",items=new[]{new{local=input,remote="/native-queue.txt"}}}));
                var nativeJob=fileJobs.Values.Single(j=>j.SessionId==advancedId);
                if(nativeJob.Running!=null)await nativeJob.Running;
                checks["nativeAdvancedQueueUpload"]=nativeJob.Status=="Completato"&&nativeJob.NativeSnapshot!=null;
                var opened=JsonSerializer.SerializeToElement(await OpenRemoteEditor(JsonSerializer.SerializeToElement(new{sessionId=advancedId,path="/native-queue.txt"})),ProfileStore.Json);
                var edit=editBuffers[opened.GetProperty("id").GetString()!];
                await SaveRemoteEditor(edit,"native integrated editor\r\n",false);
                checks["nativeIntegratedEditorSaves"]= (await sessions[advancedId].ReadTextAsync("/native-queue.txt")).Text=="native integrated editor\r\n";
                PrepareExternalEditor(edit);edit.AutoUpload=true;await File.WriteAllTextAsync(edit.Local,"native external editor\r\n",new UTF8Encoding(false));
                for(int i=0;i<100&&edit.Original.Text!="native external editor\r\n";i++)await Task.Delay(50);
                checks["nativeExternalEditorAutoUpload"]=edit.Original.Text=="native external editor\r\n";
                CloseEditor(edit.Id);
                var bulk=Path.Combine(dataPath,"native-resume.bin");await File.WriteAllBytesAsync(bulk,System.Security.Cryptography.RandomNumberGenerator.GetBytes(1048577));
                EnqueueFiles(JsonSerializer.SerializeToElement(new{sessionId=advancedId,direction="upload",conflict="new",bandwidthKiB=256,items=new[]{new{local=bulk,remote="/native-resume.bin"},new{local=bulk,remote="/native-paused.bin"}}}));
                var interrupted=fileJobs.Values.Single(j=>j.Request.Remote=="/native-resume.bin");var manuallyPaused=fileJobs.Values.Single(j=>j.Request.Remote=="/native-paused.bin");
                for(int i=0;i<200&&(interrupted.Progress==0||manuallyPaused.Progress==0);i++)await Task.Delay(25);
                manuallyPaused.Cancellation!.Cancel();await manuallyPaused.Running!;
                checks["nativeManualPauseBeforeOutage"]=manuallyPaused.Status=="In pausa";
                var fixtureRoot=Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")!;
                File.WriteAllText(Path.Combine(fixtureRoot,"drop-network.request"),"fixture");
                for(int i=0;i<300&&(nativeControls.ContainsKey(managedId)||nativeAuthenticated.Contains(managedId));i++)await Task.Delay(50);
                checks["nativeWireLossRetainsBrowserIdentity"]=!nativeAuthenticated.Contains(managedId)&&!nativeControls.ContainsKey(managedId)&&nativeStorageParents.GetValueOrDefault(browserId)==managedId&&!storageSessions.ContainsKey(browserId);
                await Task.Delay(600);
                var recovered=await RecoverNativeProfile(managed,JsonSerializer.SerializeToElement(new{cols=100,rows=30}));
                var recoveredJson=JsonSerializer.SerializeToElement(recovered,ProfileStore.Json);
                checks["managedNativeRecoveryRebindsSftp"]=recoveredJson.GetProperty("success").GetBoolean()&&nativeAuthenticated.Contains(managedId)&&storageSessions.ContainsKey(browserId);
                await storageSessions[browserId].List("");
                checks["nativeAdvancedWorkspaceRestored"]=sessions.TryGetValue(advancedId,out var restoredFiles)&&restoredFiles.FilesAvailable;
                checks["nativeAdvancedEditorFileSurvivesRecovery"]=(await sessions[advancedId].ReadTextAsync("/native-queue.txt")).Text=="native external editor\r\n";
                if(interrupted.Running!=null)await interrupted.Running;
                checks["nativeInterruptedQueueResumesAfterRecovery"]=interrupted.Status=="Completato";if(interrupted.Status!="Completato")error="Native queue: "+interrupted.Status+" / "+interrupted.Error;
                checks["nativeManualPauseNotResumedByRecovery"]=manuallyPaused.Status=="In pausa";
                checks["nativeRecoveryUsesNewPublicDirectory"]=nativePublicFolders[managedId]!=originalPublicFolder;
                checks["nativeRecoveryRefreshesFollowRoot"]=recoveredJson.GetProperty("sessions").EnumerateArray().Any(item=>item.GetProperty("sessionId").GetString()==browserId&&item.GetProperty("followRoot").GetString()=="/fixture-root");
                await Task.Delay(200);
                checks["nativeWireRecoveryDoesNotReplayHook"]=File.ReadAllLines(Path.Combine(fixtureRoot,"hook-count.txt")).Length==1;
            }
            finally{await CloseLocal(managedId,false);data.Profiles.Remove(managed);}
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
        bool success = error == null && checks.Count == 30 && checks.Values.All(x => x);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "native-integration-smoke-result.json"), JsonSerializer.Serialize(new { success, checks, error }, ProfileStore.Json)); CompleteSmoke(success);
    }
}
