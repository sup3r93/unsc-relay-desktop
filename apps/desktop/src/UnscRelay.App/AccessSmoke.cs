using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    Func<string,JsonElement,bool>? accessTestConsent;
    Func<Task>? accessTestIdentity;
    async Task CheckApplicationAccess()
    {
        var checks=new Dictionary<string,bool>();
        try{
            if(!accessSmokeTest)throw new InvalidOperationException();
            checks["testAudioMuted"]=Browser.CoreWebView2.IsMuted;
            checks["automaticEditorPreferenceApplied"]=data.Settings.EditorPreference=="auto"&&data.Settings.ExternalEditor==EditorDiscovery.FindNotepadPlusPlus();
            var empty=JsonSerializer.SerializeToElement(new{});const string master="Access smoke password 024!";vault.Unlock(master);vault.Lock();
            data.Settings=data.Settings with{RequireAppAuthentication=true,AssistantApiEnabled=false};
            checks["startupBootstrapRedacted"]=AppLocked&&!JsonSerializer.Serialize(AccessBootstrap()).Contains("dataPath");
            try{await Execute("openData",empty);checks["lockedConsoleRejectsRpc"]=false;}catch(InvalidOperationException){checks["lockedConsoleRejectsRpc"]=true;}
            var loginDialog=new AccessApprovalDialog(this,"Login fixture","Attesa senza autenticazione",false,true);
            var apiDialog=new AccessApprovalDialog(this,"API fixture","Richiesta temporanea",false);
            checks["loginHasNoTimeout"]=!loginDialog.HasExpiration;checks["apiRetainsTimeout"]=apiDialog.HasExpiration;
            if(Environment.GetEnvironmentVariable("RELAY_VERIFY_LOGIN_WAIT")=="1"){
                loginDialog.Opacity=0;loginDialog.ShowActivated=false;loginDialog.ShowInTaskbar=false;loginDialog.WindowStartupLocation=System.Windows.WindowStartupLocation.Manual;loginDialog.Left=-20000;loginDialog.Top=-20000;loginDialog.Show();await Task.Delay(TimeSpan.FromSeconds(125));checks["loginRemainsLockedAfterTwoMinutes"]=loginDialog.IsVisible&&AppLocked&&!exitAllowed;loginDialog.Close();
            }
            try{await AccessAction("applicationAuthenticate",JsonSerializer.SerializeToElement(new{method="password",password="Incorrect fixture password"}));checks["inlineWrongPasswordFailsClosed"]=false;}catch(System.Security.Cryptography.CryptographicException){checks["inlineWrongPasswordFailsClosed"]=AppLocked&&!vault.IsUnlocked;}
            await AccessAction("applicationAuthenticate",JsonSerializer.SerializeToElement(new{method="password",password=master}));
            checks["inlinePasswordUnlocksAppAndVault"]=!AppLocked&&vault.IsUnlocked;
            checks["lockedBootstrapNeverContainsPrivateKey"]=!JsonSerializer.Serialize(AccessBootstrap()).Contains("PRIVATE KEY");
            appAuthenticated=true;
            checks["existingMethodsPasswordAndHello"]=vault.Exists&&vault.WindowsHello()==null;
            var exportedKey=UnscRelay.Core.KeyGeneration.Generate(vault,"Export fixture","ed25519");var exportBytes=vault.Read(exportedKey.Id);var exportPath=Path.Combine(dataPath,"export-fixture.key");
            try{PrivateKeyExport.Write(exportPath,exportBytes);checks["privateExportPreservesExactBytes"]=File.ReadAllBytes(exportPath).SequenceEqual(exportBytes);var exportAcl=System.IO.FileSystemAclExtensions.GetAccessControl(new FileInfo(exportPath));var owner=System.Security.Principal.WindowsIdentity.GetCurrent().User!;var exportRules=exportAcl.GetAccessRules(true,true,typeof(System.Security.Principal.SecurityIdentifier)).Cast<System.Security.AccessControl.FileSystemAccessRule>().ToArray();checks["privateExportIsRestrictedToCurrentUser"]=exportAcl.AreAccessRulesProtected&&exportRules.Length==1&&exportRules[0].IdentityReference.Equals(owner)&&exportRules[0].AccessControlType==System.Security.AccessControl.AccessControlType.Allow;}
            finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(exportBytes);if(File.Exists(exportPath))File.Delete(exportPath);}

            bool Denies(string name,JsonElement p){try{AssistantPolicy.Validate(name,p);return false;}catch(ArgumentException){return true;}}
            checks["noArbitraryRpc"]=Denies("vaultUnlock",empty)&&Denies("vaultExportPrivate",empty)&&Denies("vaultRevealPrivate",empty)&&Denies("vaultCopyPrivate",empty)&&Denies("vaultExport",empty)&&Denies("settings",empty);
            checks["rejectExtraFields"]=Denies("relay_status",JsonSerializer.SerializeToElement(new{password="never"}));
            checks["rejectUnboundWrites"]=Denies("relay_sftp_write",JsonSerializer.SerializeToElement(new{sessionId=Guid.NewGuid().ToString(),path="/tmp/test",text="abc",expectedHash=""}));
            checks["rejectInvalidSession"]=Denies("relay_disconnect",JsonSerializer.SerializeToElement(new{sessionId="*"}));
            checks["rejectControlPaths"]=Denies("relay_sftp_read",JsonSerializer.SerializeToElement(new{sessionId=Guid.NewGuid().ToString(),path="/etc/x\n"}));
            try{await ApproveAssistant("relay_status",empty,CancellationToken.None);checks["disabledApiRejectsWithoutConsent"]=false;}catch{checks["disabledApiRejectsWithoutConsent"]=true;}
            data.Settings=data.Settings with{AssistantApiEnabled=true};consoleReady=false;
            try{await ApproveAssistant("relay_status",empty,CancellationToken.None);checks["startupBlocksApi"]=false;}catch{checks["startupBlocksApi"]=true;}consoleReady=true;
            var proofs=0;accessTestConsent=(_,_)=>false;accessTestIdentity=()=>{proofs++;return Task.CompletedTask;};
            try{await ApproveAssistant("relay_status",empty,CancellationToken.None);checks["denialReturnsNoData"]=false;}catch(OperationCanceledException){checks["denialReturnsNoData"]=proofs==0;}
            accessTestConsent=(_,_)=>true;accessTestIdentity=()=>Task.FromException(new System.Security.Cryptography.CryptographicException());
            try{await ApproveAssistant("relay_status",empty,CancellationToken.None);checks["failedIdentityReturnsNoData"]=false;}catch(System.Security.Cryptography.CryptographicException){checks["failedIdentityReturnsNoData"]=true;}
            accessTestIdentity=()=>{proofs++;return Task.CompletedTask;};
            await ApproveAssistant("relay_status",empty,CancellationToken.None);await ApproveAssistant("relay_profiles",empty,CancellationToken.None);
            checks["freshProofForEveryRequest"]=proofs==2;
            var profile=new ConnectionProfile{Name="fixture",Host="127.0.0.1",Username="fixture",PasswordId="SECRET-ID",Notes="SECRET-NOTES",KeyId="SECRET-KEY"};data.Profiles.Add(profile);
            var profiles=JsonSerializer.Serialize(await ApproveAssistant("relay_profiles",empty,CancellationToken.None));checks["profileResponseOmitsCredentialsAndNotes"]=!profiles.Contains("SECRET");
            accessTestIdentity=()=>{data.Settings=data.Settings with{AssistantApiEnabled=false};return Task.CompletedTask;};
            try{await ApproveAssistant("relay_status",empty,CancellationToken.None);checks["disableDuringApprovalRevokesRequest"]=false;}catch{checks["disableDuringApprovalRevokesRequest"]=true;}
            data.Settings=data.Settings with{AssistantApiEnabled=true};accessTestIdentity=()=>Task.CompletedTask;using var canceled=new CancellationTokenSource();canceled.Cancel();
            try{await ApproveAssistant("relay_status",empty,canceled.Token);checks["expiredRequestDenied"]=false;}catch(OperationCanceledException){checks["expiredRequestDenied"]=true;}
            checks["audioDefault"]=new UserSettings().AppMusicVolume==.35;
            await Execute("audioSettings",JsonSerializer.SerializeToElement(new{enabled=true,volume=0d}));
            checks["audioZeroPersists"]=store.Load().Settings.AppMusicVolume==0&&store.Load().Settings.AppMusicEnabled;
            await Execute("settings",JsonSerializer.SerializeToElement(new{autoLockMinutes=27}));
            checks["lockSavePreservesAudioAndAccess"]=data.Settings.AppMusicVolume==0&&data.Settings.AppMusicEnabled&&data.Settings.RequireAppAuthentication&&data.Settings.AssistantApiEnabled;
            checks["sequenceAudioUnchanged"]=data.Settings.StartupVolume==.8&&data.Settings.ShutdownVolume==1&&data.Settings.StartupVideo&&data.Settings.ShutdownVideo;
            try{await Execute("audioSettings",JsonSerializer.SerializeToElement(new{enabled=false,volume=1.01}));checks["invalidAudioRejected"]=false;}catch(ArgumentException){checks["invalidAudioRejected"]=data.Settings.AppMusicEnabled&&data.Settings.AppMusicVolume==0;}
            await Execute("audioSettings",JsonSerializer.SerializeToElement(new{enabled=false,volume=1d}));
            checks["audioDisabledPersists"]=!store.Load().Settings.AppMusicEnabled&&store.Load().Settings.AppMusicVolume==1;
            await Execute("audioSettings",JsonSerializer.SerializeToElement(new{enabled=true,volume=.42,startupVolume=.23,shutdownVolume=.61}));
            await Execute("settings",JsonSerializer.SerializeToElement(new{autoLockMinutes=28}));
            var savedAudio=store.Load().Settings;
            checks["threeVolumesPersistIndependently"]=savedAudio.StartupVolume==.23&&savedAudio.ShutdownVolume==.61&&savedAudio.AppMusicVolume==.42;
            try{await Execute("audioSettings",JsonSerializer.SerializeToElement(new{enabled=false,volume=0d,startupVolume=-.1,shutdownVolume=.9}));checks["invalidIntroAtomic"]=false;}catch(ArgumentException){checks["invalidIntroAtomic"]=data.Settings==savedAudio;}
            appAuthenticated=false;var protectedState=JsonSerializer.Serialize(AccessBootstrap(),ProfileStore.Json);checks["lockedStartupReceivesOnlyAudioLevel"]=protectedState.Contains("0.23")&&!protectedState.Contains("dataPath");appAuthenticated=true;
            var savedSnippet=new Snippet("shortcut-one","Fixture","echo fixture","Test","Ctrl+Alt+Shift+A");
            await Execute("saveSnippet",JsonSerializer.SerializeToElement(savedSnippet,ProfileStore.Json));
            try{await Execute("saveSnippet",JsonSerializer.SerializeToElement(savedSnippet with{Id="shortcut-two"},ProfileStore.Json));checks["shortcutConflictPreservesLibrary"]=false;}catch(ArgumentException){checks["shortcutConflictPreservesLibrary"]=store.Load().Snippets.Count==1;}
            checks["introHasNativeCover"]=StartupCover!=null;
            var geometry=await Browser.CoreWebView2.ExecuteScriptAsync("({loaded:typeof accessSettingsDialog==='function',hidden:!document.body.classList.contains('startup-covered')})");checks["accessUiLoaded"]=geometry.Contains("true");
            if(Environment.GetEnvironmentVariable("RELAY_FILE_PORT") is string portText){
                var fixture=Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")!;
                if(!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new IOException("Fixture non valida.");
                vault.Unlock(master);data.Settings=data.Settings with{AssistantApiEnabled=true};
                var profile2=new ConnectionProfile{Name="API loopback",Host="127.0.0.1",Port=int.Parse(portText),Username="test",Authentication="password",PasswordId=vault.StorePassword("API fixture","relay-test-password"),StartupMacro=new("fixture","Fixture macro","echo MACRO-FIXTURE",[],true,false)};data.Profiles=[profile2];
                var host=NativeIdentity.Parse(Environment.GetEnvironmentVariable("RELAY_FIXTURE_HOST_PUBLIC")!);hosts.Accept(new(profile2.Host,profile2.Port,host.Algorithm,host.Fingerprint[7..],DateTime.UtcNow,host.PublicKey));
                var id=Guid.NewGuid().ToString("N");JsonElement P(object o)=>JsonSerializer.SerializeToElement(o,ProfileStore.Json);
                await Execute("connect",P(new{profileId=profile2.Id,sessionId=id}));
                accessTestConsent=(_,_)=>true;accessTestIdentity=()=>Task.CompletedTask;
                File.WriteAllText(Path.Combine(fixture,"assistant.txt"),"original fixture");
                var trace=Path.Combine(fixture,"shell-input.bin");
                for(var n=0;n<80&&(!File.Exists(trace)||!File.ReadAllText(trace).Contains("MACRO-FIXTURE"));n++)await Task.Delay(25);
                checks["startupMacroSentOnceAfterSshAuthentication"]=File.Exists(trace)&&File.ReadAllText(trace).Split("MACRO-FIXTURE").Length==2;
                var filesId=Guid.NewGuid().ToString("N");await Execute("connect",P(new{profileId=profile2.Id,sessionId=filesId,filesOnly=true}));
                var autoId=Guid.NewGuid().ToString("N");await Execute("connect",P(new{profileId=profile2.Id,sessionId=autoId,automatic=true}));await Task.Delay(100);
                checks["sftpAndAutomaticConnectDoNotRepeatMacro"]=File.ReadAllText(trace).Split("MACRO-FIXTURE").Length==2;
                var text=(RemoteText)(await ApproveAssistant("relay_sftp_read",P(new{sessionId=id,path="/assistant.txt"}),CancellationToken.None))!;
                checks["apiReadsRealSftpText"]=text.Text=="original fixture";
                await ApproveAssistant("relay_sftp_write",P(new{sessionId=id,path="/assistant.txt",text="approved change",expectedHash=text.Hash}),CancellationToken.None);
                checks["apiWritesApprovedSftpText"]=File.ReadAllText(Path.Combine(fixture,"assistant.txt"))=="approved change";
                try{await ApproveAssistant("relay_sftp_write",P(new{sessionId=id,path="/assistant.txt",text="stale change",expectedHash=text.Hash}),CancellationToken.None);checks["apiRejectsStaleRemoteFile"]=false;}catch(IOException){checks["apiRejectsStaleRemoteFile"]=File.ReadAllText(Path.Combine(fixture,"assistant.txt"))=="approved change";}
                accessTestConsent=(_,_)=>false;
                try{await ApproveAssistant("relay_sftp_write",P(new{sessionId=id,path="/assistant.txt",text="denied change",expectedHash=text.Hash}),CancellationToken.None);checks["deniedWriteLeavesRealFileIntact"]=false;}catch(OperationCanceledException){checks["deniedWriteLeavesRealFileIntact"]=File.ReadAllText(Path.Combine(fixture,"assistant.txt"))=="approved change";}
                accessTestConsent=(_,_)=>true;
                var upload=Path.Combine(fixture,"assistant-local.txt");File.WriteAllText(upload,"queue fixture");
                await ApproveAssistant("relay_transfer",P(new{sessionId=id,local=upload,remote="/assistant-upload.txt",direction="upload"}),CancellationToken.None);
                for(var i=0;i<200&&fileJobs.Values.Any(j=>j.Status!="Completato");i++)await Task.Delay(50);
                checks["apiQueuesRealTransfer"]=fileJobs.Values.All(j=>j.Status=="Completato")&&File.ReadAllText(Path.Combine(fixture,"assistant-upload.txt"))=="queue fixture";
                await ApproveAssistant("relay_send_input",P(new{sessionId=id,text="echo fixture\r"}),CancellationToken.None);checks["apiInputReachesLiveSession"]=sessions[id].Connected;
                await ApproveAssistant("relay_disconnect",P(new{sessionId=id}),CancellationToken.None);checks["apiDisconnectsSelectedSession"]=!sessions.ContainsKey(id);
                var localId=Guid.NewGuid().ToString("N");var localFixture=new ConnectionProfile{Name="Update local fixture",Protocol="local",LocalShell="cmd"};
                await ConnectLocal(localFixture,localId,P(new{cols=80,rows=24}));
                var confirmations=0;var closed=await CloseSessionsForUpdate(_=>{confirmations++;return false;});
                checks["decliningUpdateKeepsSshSftpAndLocalSessions"]=!closed&&confirmations==1&&sessions.Count==2&&localSessions[localId].Running;
                closed=await CloseSessionsForUpdate(_=>{confirmations++;return true;});
                checks["updateClosesAllSessionsWithSingleConfirmation"]=closed&&confirmations==2&&sessions.Count==0&&localSessions.Count==0&&desktopSessions.Count==0;

            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"access-smoke-result.json"),JsonSerializer.Serialize(new{success=checks.Values.All(x=>x),checks},ProfileStore.Json));
        }catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"access-smoke-result.json"),JsonSerializer.Serialize(new{success=false,error=ex.ToString(),checks},ProfileStore.Json));}
        finally{foreach(var session in sessions.Values)session.Dispose();sessions.Clear();exitAllowed=true;Browser.Dispose();Close();}
    }
}
