using System.IO;
using System.Text.Json;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckFileWorkspace()
    {
        var checks = new Dictionary<string, bool>(); string? error = null;
        JsonElement P(object value) => JsonSerializer.SerializeToElement(value, ProfileStore.Json);
        try
        {
            fileJobs.Clear(); EmitQueue();
            var root = Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT") ?? throw new IOException("Fixture missing");
            var profile = new ConnectionProfile { Id = "files-native-smoke", Name = "Files fixture", Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("RELAY_FILE_PORT")!), Username = "test", Protocol = "sftp", Authentication = "password" };
            SaveConnectionProfile(P(new { profile.Id, profile.Name, profile.Host, profile.Port, profile.Username, profile.Protocol, profile.Authentication, addToAddressBook = true, machineName = "Native loopback" }));
            await FileAction("machineSave",P(new {id="manual-address-fixture",name="Manual address",host="192.0.2.200",port=2222}));
            checks["manualAddressPersistsWithoutProfile"] = store.Load().Machines.Any(m=>m.Id=="manual-address-fixture"&&m.Port==2222)&&!data.Profiles.Any(p=>p.Host=="192.0.2.200");
            checks["namedAddressSaved"] = data.Machines.Any(m => m.Name == "Native loopback" && m.Port == profile.Port);
            connectionPasswords[profile.Id] = "relay-test-password";
            var sid = Guid.NewGuid().ToString(); using var session = new SshSession(profile, "relay-test-password", null, "", hosts, _ => true, _ => "") { Id = sid }; sessions.Add(sid, session); await session.ConnectAsync(100, 30);
            try { await Execute("connect", P(new { profileId=profile.Id,sessionId=Guid.NewGuid().ToString(),automatic=true })); } catch(InvalidOperationException) { checks["automaticSftpRejectedBeforeNetwork"] = true; }
            try { await CreateAuthenticatedSession(profile with { Id="no-credential-fixture", Protocol="ssh" },null,0,Guid.NewGuid().ToString(),true); } catch(InvalidOperationException) { checks["automaticMissingPasswordNeverPrompts"] = true; }
            try { WorkflowAction("multiSend", P(new { sessionIds=new[]{sid},text="never sent" })); } catch(InvalidOperationException) { checks["multiSendRejectsSftpBeforeConfirmation"] = true; }
            var trace=Path.Combine(dataPath,"hook-lifecycle.txt");File.WriteAllText(trace,"");var helper=Environment.GetEnvironmentVariable("RELAY_HOOK_EXE")??throw new IOException("Hook helper missing");
            var hookProfile=profile with {Id="hook-lifecycle-fixture",Protocol="ssh",Subsystem="fixture-sftp",Hooks=new[]{"before-connect","after-connect","before-disconnect","after-disconnect"}.Select(phase=>new ConnectionHook(phase,"local",helper,["--hook-trace",trace,phase],true)).ToList()};
            data.Profiles.RemoveAll(p=>p.Id==hookProfile.Id);data.Profiles.Add(hookProfile);connectionPasswords[hookProfile.Id]="relay-test-password";
            var hookSession=Guid.NewGuid().ToString();await Execute("connect",P(new{profileId=hookProfile.Id,sessionId=hookSession}));
            checks["nativeCustomSubsystemAvailable"] = sessions[hookSession].FilesAvailable;
            await Execute("disconnect",P(new{sessionId=hookSession}));
            checks["nativeHooksRunInLifecycleOrderOnce"] = File.ReadAllLines(trace).SequenceEqual(new[]{"before-connect","after-connect","before-disconnect","after-disconnect"});
            File.WriteAllText(trace,"");var automaticSession=Guid.NewGuid().ToString();await Execute("connect",P(new{profileId=hookProfile.Id,sessionId=automaticSession,automatic=true}));await Execute("disconnect",P(new{sessionId=automaticSession,automatic=true}));
            checks["automaticReconnectDoesNotExecuteHooks"] = File.ReadAllText(trace).Length==0;
            var local = Path.Combine(dataPath, "FileFixture"); Directory.CreateDirectory(local); Directory.CreateDirectory(Path.Combine(local, "tree")); File.WriteAllText(Path.Combine(local, "tree", "nested.txt"), "nested fixture");
            var plan = JsonSerializer.SerializeToElement(await PlanFiles(P(new { sessionId = sid, direction = "upload", paths = new[] { Path.Combine(local, "tree") }, localDirectory = local, remoteDirectory = "/" })), ProfileStore.Json);
            checks["directoryPlanIncludesFiles"] = plan.GetProperty("items").GetArrayLength() == 2;
            EnqueueFiles(P(new { sessionId = sid, direction = "upload", items = plan.GetProperty("items"), conflict = "replace" }));
            for (var i = 0; QueueBusy && i < 150; i++) await Task.Delay(50);
            checks["nativeParallelQueuePublishesTree"] = fileJobs.Values.All(j => j.Status == "Completato") && File.ReadAllText(Path.Combine(root, "tree", "nested.txt")) == "nested fixture";
            File.WriteAllText(Path.Combine(root, "editor.txt"), "before\r\n");
            var localCompare = Path.Combine(local, "compare.txt"); File.WriteAllText(localCompare, "local comparison\r\n");
            var comparison = JsonSerializer.SerializeToElement(await FileAction("fileCompare", P(new { sessionId = sid, local = localCompare, remote = "/editor.txt" })), ProfileStore.Json);
            checks["comparisonReadsBothFilesWithoutWriting"] = comparison.GetProperty("local").GetProperty("text").GetString() == "local comparison\r\n" && comparison.GetProperty("remote").GetProperty("text").GetString() == "before\r\n" && File.ReadAllText(Path.Combine(root, "editor.txt")) == "before\r\n";
            await FileAction("fileLocation", P(new { sessionId = sid, local, remote = "/tree" }));
            checks["lastDirectoriesPersistByEndpoint"] = store.Load().FileLocations[profile.Id].Remote == "/tree" && store.Load().FileLocations[profile.Id].Host == profile.Host;
            var opened = JsonSerializer.SerializeToElement(await OpenRemoteEditor(P(new { sessionId = sid, path = "/editor.txt" })), ProfileStore.Json); var editor = editBuffers[opened.GetProperty("id").GetString()!];
            await SaveRemoteEditor(editor, "after\r\n", false); checks["nativeEditorSavesText"] = File.ReadAllText(Path.Combine(root, "editor.txt")) == "after\r\n";
            BeginExternalEdit(editor, () => { }); checks["externalEditorCopyUsesSafeLeaf"] = Path.GetFileName(editor.Local) == "relay-editor.txt" && editor.AutoUpload && editor.ExternalOnly; File.WriteAllText(editor.Local, "external saved fixture\r\n");
            for (var i = 0; i < 80 && File.ReadAllText(Path.Combine(root, "editor.txt")) != "external saved fixture\r\n"; i++) await Task.Delay(50);
            checks["externalEditorWatcherPublishesSavedContent"] = File.ReadAllText(Path.Combine(root, "editor.txt")) == "external saved fixture\r\n"; editor.AutoUpload = false;
            for(var i=0;i<80&&editor.AutoScheduled;i++)await Task.Delay(50);
            BeginExternalEdit(editor,()=>{}); checks["externalReopenKeepsExistingCopy"] = File.ReadAllText(editor.Local)=="external saved fixture\r\n";
            try{await FileAction("editorSave",P(new{id=editor.Id,text="must not overwrite"}));}catch(IOException){checks["externalModeRejectsIntegratedWrites"]=File.ReadAllText(Path.Combine(root,"editor.txt"))=="external saved fixture\r\n";}
            var externalReplacement=editor.Local+".replace";File.WriteAllText(externalReplacement,"atomic editor save\r\n");File.Move(externalReplacement,editor.Local,true);
            for(var i=0;i<80&&(editor.AutoScheduled||File.ReadAllText(Path.Combine(root,"editor.txt"))!="atomic editor save\r\n");i++)await Task.Delay(50);
            checks["externalAtomicSavePublishesAutomatically"] = File.ReadAllText(Path.Combine(root,"editor.txt"))=="atomic editor save\r\n"&&!editor.Dirty;
            File.WriteAllText(Path.Combine(root, "editor.txt"), "outside change");
            File.WriteAllText(editor.Local,"preserved local conflict");for(var i=0;i<80&&(editor.AutoUpload||editor.AutoScheduled);i++)await Task.Delay(50);
            checks["externalConflictStopsAndKeepsLocalCopy"] = !editor.AutoUpload&&editor.Dirty&&File.ReadAllText(editor.Local)=="preserved local conflict"&&File.ReadAllText(Path.Combine(root,"editor.txt"))=="outside change";
            try { await SaveRemoteEditor(editor, "do not overwrite", false); } catch (IOException) { }
            checks["nativeEditorConflictPreservesRemote"] = File.ReadAllText(Path.Combine(root, "editor.txt")) == "outside change";
            CloseEditor(editor.Id); checks["editorCanClose"] = editBuffers.Count == 0; checks["externalClosePreservesLocalCopy"] = File.ReadAllText(editor.Local)=="preserved local conflict";
            var backgroundOpened=JsonSerializer.SerializeToElement(await OpenRemoteEditor(P(new{sessionId=sid,path="/editor.txt"})),ProfileStore.Json);var background=editBuffers[backgroundOpened.GetProperty("id").GetString()!];BeginExternalEdit(background,()=>{});PrepareCloseSessionEditors(sid);checks["externalSessionCanCloseWithoutHiddenEditorTab"]=editBuffers.Count==0&&File.Exists(background.Local);
            vault.Unlock("Desktop smoke fixture master!"); var key = KeyGeneration.Generate(vault, "No passphrase fixture", "ed25519");
            var keyProfile = profile with { Id = "no-phrase", Authentication = "key", KeyId = key.Id };
            using (var noPhrase = await CreateAuthenticatedSession(keyProfile, null, 0, Guid.NewGuid().ToString()))
            {
                checks["unprotectedKeyDoesNotPromptForPassphrase"] = true;
                await noPhrase.ConnectAsync(80, 24);
                await noPhrase.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), Path.Combine(local, "tree", "nested.txt"), "/key-upload.txt", true, "replace"), (_, _) => { }, CancellationToken.None);
                checks["keyChannelsWorkAfterRawKeyIsCleared"] = File.ReadAllText(Path.Combine(root, "key-upload.txt")) == "nested fixture";
            }
            var syncLocal = Path.Combine(local, "SyncFixture"); Directory.CreateDirectory(syncLocal);
            var syncRemote = Path.Combine(root, "SyncRemote"); Directory.CreateDirectory(syncRemote);
            File.WriteAllText(Path.Combine(syncLocal,"same.txt"),"same"); File.WriteAllText(Path.Combine(syncRemote,"same.txt"),"same");
            File.WriteAllText(Path.Combine(syncLocal,"changed.txt"),"left"); File.WriteAllText(Path.Combine(syncRemote,"changed.txt"),"past");
            File.WriteAllText(Path.Combine(syncLocal,"new.txt"),"new file"); Directory.CreateDirectory(Path.Combine(syncLocal,"empty"));
            var sync = JsonSerializer.SerializeToElement(await FileAction("folderCompare",P(new{sessionId=sid,scanId=Guid.NewGuid().ToString(),localDirectory=syncLocal,remoteDirectory="/SyncRemote"})),ProfileStore.Json);
            checks["folderComparisonHashesWithoutWrites"] = sync.GetProperty("items").EnumerateArray().Single(i=>S(i,"relative")=="same.txt").GetProperty("status").GetString()=="Identico" && File.ReadAllText(Path.Combine(syncRemote,"changed.txt"))=="past";
            using(var replacement = new SshSession(profile with {Host="192.0.2.200"},"fixture",null,"",hosts,_=>true,_=>"") {Id=sid})
            {
                sessions[sid]=replacement;
                try {await EnqueueFolderPlan(P(new{sessionId=sid,id=sync.GetProperty("id").GetString(),direction="upload",paths=new[]{"new.txt"}}),confirm:false);}catch(IOException){checks["folderPlanRejectsReplacedSession"] = !File.Exists(Path.Combine(syncRemote,"new.txt"));}
                finally {sessions[sid]=session;}
            }
            await EnqueueFolderPlan(P(new{sessionId=sid,id=sync.GetProperty("id").GetString(),direction="upload",paths=new[]{"changed.txt","new.txt","empty"}}),confirm:false);
            for(var i=0;QueueBusy&&i<150;i++)await Task.Delay(50);
            checks["folderSyncUsesGuardedQueue"] = File.ReadAllText(Path.Combine(syncRemote,"changed.txt"))=="left" && File.ReadAllText(Path.Combine(syncRemote,"new.txt"))=="new file" && Directory.Exists(Path.Combine(syncRemote,"empty")) && File.Exists(Path.Combine(syncRemote,"same.txt"));
            File.WriteAllText(Path.Combine(syncLocal,"new.txt"),"different before preview");
            sync=JsonSerializer.SerializeToElement(await CompareFolders(P(new{sessionId=sid,scanId=Guid.NewGuid().ToString(),localDirectory=syncLocal,remoteDirectory="/SyncRemote"})),ProfileStore.Json);
            File.WriteAllText(Path.Combine(syncLocal,"new.txt"),"changed after preview");
            try {await EnqueueFolderPlan(P(new{sessionId=sid,id=sync.GetProperty("id").GetString(),direction="upload",paths=new[]{"new.txt"}}),confirm:false);}catch(IOException){checks["staleFolderPlanRefused"] = File.ReadAllText(Path.Combine(syncRemote,"new.txt"))=="new file";}
            var persistId = Guid.NewGuid().ToString("N"); var persistRequest = new TransferRequest(persistId, Path.Combine(local, "tree", "nested.txt"), "/restart.txt", true, "new");
            fileJobs.Add(persistId, new() { Id = persistId, ProfileId = profile.Id, SessionId = sid, Host = profile.Host, Port = profile.Port, Username = profile.Username, Request = persistRequest, Status = "In pausa" }); EmitQueue(); fileJobs.Clear(); LoadFileQueue();
            checks["pausedQueueRestoresWithoutRunning"] = fileJobs.Count == 1 && fileJobs.Values.Single().Status == "In pausa" && fileJobs.Values.Single().Cancellation == null;
            var legacyQueue=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dataPath,"transfers.json")))!.AsArray();legacyQueue[0]!["version"]=1;legacyQueue[0]!["request"]!["permissions"]=Convert.ToInt16("640",8);File.WriteAllText(Path.Combine(dataPath,"transfers.json"),legacyQueue.ToJsonString());fileJobs.Clear();LoadFileQueue();checks["legacyQueueRestoresIntendedOctalPermissions"]=fileJobs.Values.Single().Request.Permissions==640;
            var restored = fileJobs.Values.Single();
            using (var different = new SshSession(profile with { Port = profile.Port == 65535 ? 22 : profile.Port + 1 }, "fixture", null, "", hosts, _ => true, _ => ""))
            {
                try { CheckJobEndpoint(restored, different); } catch (IOException) { checks["restoredQueueRejectsChangedEndpoint"] = true; }
            }
            sessions.Remove(sid); session.Dispose(); var newId = Guid.NewGuid().ToString(); RebindFileQueue(profile.Id, newId); checks["restoredJobsBindToNewSftpTab"] = fileJobs.Values.Single().SessionId == newId;
            using var bridgeSourceSession=new SshSession(profile,"relay-test-password",null,"",new(Path.Combine(dataPath,"bridge-hosts.json")),_=>true,_=>throw new Exception("Unexpected challenge"));await bridgeSourceSession.ConnectAsync(100,30);sessions.Add(bridgeSourceSession.Id,bridgeSourceSession);
            var bridgeProfile=profile with{Id="bridge-recipient-fixture",Name="Bridge recipient"};using var bridgeTarget=new SshSession(bridgeProfile,"relay-test-password",null,"",new(Path.Combine(dataPath,"bridge-hosts.json")),_=>true,_=>throw new Exception("Unexpected challenge"));await bridgeTarget.ConnectAsync(100,30);sessions.Add(bridgeTarget.Id,bridgeTarget);data.Profiles.RemoveAll(item=>item.Id==bridgeProfile.Id);data.Profiles.Add(bridgeProfile);store.Save(data);await File.WriteAllTextAsync(Path.Combine(root,"bridge-app-source.txt"),"bridge app fixture");
            async Task<JsonElement> BridgePlan()=>JsonSerializer.SerializeToElement(await FileAction("fileBridgePlan",P(new{sessionId=bridgeSourceSession.Id,targetSessionId=bridgeTarget.Id,sourcePath="/bridge-app-source.txt",targetPath="/bridge-app-target.txt"})),ProfileStore.Json);
            var bridgePreview=await BridgePlan();checks["bridgeNativePreviewDeclaresMemoryTransit"]=bridgePreview.GetProperty("preview").GetProperty("temporaryDiskBytes").GetInt64()==0;
            await FileAction("fileBridgeCancel",P(new{id=bridgePreview.GetProperty("id").GetGuid()}));try{await FileAction("fileBridgeEnqueue",P(new{id=bridgePreview.GetProperty("id").GetGuid(),replace=false}));checks["bridgeCancelledPreviewCannotEnqueue"]=false;}catch(IOException){checks["bridgeCancelledPreviewCannotEnqueue"]=true;}
            bridgePreview=await BridgePlan();await File.AppendAllTextAsync(Path.Combine(root,"bridge-app-source.txt")," changed");try{await FileAction("fileBridgeEnqueue",P(new{id=bridgePreview.GetProperty("id").GetGuid(),replace=false}));checks["bridgeStalePreviewCannotEnqueue"]=false;}catch(IOException){checks["bridgeStalePreviewCannotEnqueue"]=true;}
            bridgePreview=await BridgePlan();var countBeforeBridge=fileJobs.Count;bridgeTestConsent=_=>false;try{await FileAction("fileBridgeEnqueue",P(new{id=bridgePreview.GetProperty("id").GetGuid(),replace=false}));checks["bridgeExplicitDenialLeavesQueueUnchanged"]=false;}catch(OperationCanceledException){checks["bridgeExplicitDenialLeavesQueueUnchanged"]=fileJobs.Count==countBeforeBridge&&!File.Exists(Path.Combine(root,"bridge-app-target.txt"));}
            bridgeTestConsent=_=>true;await FileAction("fileBridgeEnqueue",P(new{id=bridgePreview.GetProperty("id").GetGuid(),replace=false}));var bridgeJob=fileJobs.Values.Single(job=>job.Origin!=null);if(bridgeJob.Running!=null)await bridgeJob.Running;checks["bridgeQueuedCopyCompletes"]=bridgeJob.Status=="Completato"&&File.ReadAllText(Path.Combine(root,"bridge-app-target.txt"))==File.ReadAllText(Path.Combine(root,"bridge-app-source.txt"));checks["bridgeSourceRemainsIntact"]=File.ReadAllText(Path.Combine(root,"bridge-app-source.txt"))=="bridge app fixture changed";
            bridgeJob.Status="In pausa";EmitQueue();fileJobs.Clear();LoadFileQueue();var restoredBridge=fileJobs.Values.Single(job=>job.Origin!=null);checks["bridgeJournalRestoresBothEndpointBindings"]=restoredBridge.Status=="In pausa"&&restoredBridge.Origin!.ProfileId==profile.Id&&restoredBridge.ProfileId==bridgeProfile.Id&&restoredBridge.Origin.Path=="/bridge-app-source.txt";checks["bridgeRestoredSourceRequiresOriginalConnection"]=BridgeSource(restoredBridge).Id==bridgeSourceSession.Id;bridgeTestConsent=null;
            var installProfile=profile with{Id="native-key-install-fixture",Name="Key installation fixture",Username="install-test"};data.Profiles.RemoveAll(item=>item.Id==installProfile.Id);data.Profiles.Add(installProfile);store.Save(data);using var installSession=new SshSession(installProfile,"relay-test-password",null,"",hosts,_=>true,_=>throw new Exception("Unexpected interaction"));await installSession.ConnectAsync(100,30);sessions.Add(installSession.Id,installSession);var installKey=KeyGeneration.Generate(vault,"Native key installation fixture","ed25519");var profileBeforeInstall=InstallationProfileHash(installProfile);
            async Task<JsonElement> KeyPlan()=>JsonSerializer.SerializeToElement(await PublicKeyInstallationAction("keyInstallPreview",P(new{sessionId=installSession.Id,keyId=installKey.Id})),ProfileStore.Json);
            var installPlan=await KeyPlan();checks["nativeKeyInstallationPreviewBindsFingerprint"]=installPlan.GetProperty("preview").GetProperty("fingerprint").GetString()==NativeIdentity.Parse(installKey.PublicKey).Fingerprint&&!File.Exists(Path.Combine(root,".ssh","authorized_keys"));await PublicKeyInstallationAction("keyInstallCancel",P(new{id=installPlan.GetProperty("id").GetGuid()}));try{await PublicKeyInstallationAction("keyInstallApply",P(new{id=installPlan.GetProperty("id").GetGuid(),passphrase=""}));checks["nativeKeyInstallationCancelledPreviewCannotApply"]=false;}catch(IOException){checks["nativeKeyInstallationCancelledPreviewCannotApply"]=true;}
            installPlan=await KeyPlan();keyInstallationTestConsent=_=>false;try{await PublicKeyInstallationAction("keyInstallApply",P(new{id=installPlan.GetProperty("id").GetGuid(),passphrase=""}));checks["nativeKeyInstallationDenialDoesNotWrite"]=false;}catch(OperationCanceledException){checks["nativeKeyInstallationDenialDoesNotWrite"]=!File.Exists(Path.Combine(root,".ssh","authorized_keys"));}keyInstallationTestConsent=_=>true;var installedKey=JsonSerializer.SerializeToElement(await PublicKeyInstallationAction("keyInstallApply",P(new{id=installPlan.GetProperty("id").GetGuid(),passphrase=""})),ProfileStore.Json);checks["nativeKeyInstallationVerifiesNewKeyWithoutShell"]=installedKey.GetProperty("verified").GetBoolean()&&File.ReadAllLines(Path.Combine(root,"install-auth.log")).Last()=="publickey";checks["nativeKeyInstallationPreservesProfileAndOldSession"]=InstallationProfileHash(data.Profiles.Single(item=>item.Id==installProfile.Id))==profileBeforeInstall&&installSession.FilesAvailable;keyInstallationTestConsent=null;


        }
        catch (Exception ex) { error = ex.ToString(); }
        finally { keyInstallationTestConsent=null;bridgeTestConsent=null;foreach (var session in sessions.Values) session.Dispose(); sessions.Clear(); foreach (var id in editBuffers.Keys.ToArray()) CloseEditor(id); }
        var success = error == null && checks.Count == 46 && checks.Values.All(v => v);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "files-smoke-result.json"), JsonSerializer.Serialize(new { success, checks, error }, ProfileStore.Json)); CompleteSmoke(success);
    }
}
