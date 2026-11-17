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
            var sid = Guid.NewGuid().ToString(); var session = new SshSession(profile, "relay-test-password", null, "", hosts, _ => true, _ => "") { Id = sid }; sessions.Add(sid, session); await session.ConnectAsync(100, 30);
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
            PrepareExternalEditor(editor); checks["externalEditorCopyUsesSafeLeaf"] = Path.GetFileName(editor.Local) == "relay-editor.txt"; editor.AutoUpload = true; File.WriteAllText(editor.Local, "external saved fixture\r\n");
            for (var i = 0; i < 80 && File.ReadAllText(Path.Combine(root, "editor.txt")) != "external saved fixture\r\n"; i++) await Task.Delay(50);
            checks["externalEditorWatcherPublishesSavedContent"] = File.ReadAllText(Path.Combine(root, "editor.txt")) == "external saved fixture\r\n"; editor.AutoUpload = false;
            File.WriteAllText(Path.Combine(root, "editor.txt"), "outside change");
            try { await SaveRemoteEditor(editor, "do not overwrite", false); } catch (IOException) { }
            checks["nativeEditorConflictPreservesRemote"] = File.ReadAllText(Path.Combine(root, "editor.txt")) == "outside change";
            CloseEditor(editor.Id); checks["editorCanClose"] = editBuffers.Count == 0;
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
            var restored = fileJobs.Values.Single();
            using (var different = new SshSession(profile with { Port = profile.Port == 65535 ? 22 : profile.Port + 1 }, "fixture", null, "", hosts, _ => true, _ => ""))
            {
                try { CheckJobEndpoint(restored, different); } catch (IOException) { checks["restoredQueueRejectsChangedEndpoint"] = true; }
            }
            sessions.Remove(sid); session.Dispose(); var newId = Guid.NewGuid().ToString(); RebindFileQueue(profile.Id, newId); checks["restoredJobsBindToNewSftpTab"] = fileJobs.Values.Single().SessionId == newId;
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally { foreach (var session in sessions.Values) session.Dispose(); sessions.Clear(); foreach (var id in editBuffers.Keys.ToArray()) CloseEditor(id); }
        var success = error == null && checks.Count == 26 && checks.Values.All(v => v);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "files-smoke-result.json"), JsonSerializer.Serialize(new { success, checks, error }, ProfileStore.Json)); CompleteSmoke(success);
    }
}
