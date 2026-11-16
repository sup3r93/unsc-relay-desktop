using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class MainWindow
{
    async Task CheckCoordinatedRecovery()
    {
        var checks = new Dictionary<string, bool>(); string? error = null;
        JsonElement P(object value) => JsonSerializer.SerializeToElement(value, ProfileStore.Json);
        void Check(bool value, string name) { if (!value) throw new IOException(name); checks[name] = true; }
        async Task Wait(Func<bool> condition) { for (int i = 0; i < 600; i++) { if (condition()) return; await Task.Delay(50); } throw new TimeoutException("Condizione di recupero non raggiunta."); }
        try
        {
            int port = int.Parse(Environment.GetEnvironmentVariable("RELAY_FILE_PORT")!); string fixture = Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")!;
            if (port is < 1 or > 65535 || !Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new IOException("Fixture non valida.");
            fileJobs.Clear(); vault.Unlock("Recovery fixture archive password");
            var afterTrace = Path.Combine(dataPath, "after-disconnect-trace.txt"); File.WriteAllText(afterTrace, ""); var helper = Environment.GetEnvironmentVariable("RELAY_HOOK_EXE")!;
            var profile = new ConnectionProfile { Name = "Recovery fixture", Protocol = "ssh", Host = "127.0.0.1", Port = port, Username = "test", Authentication = "password", PasswordId = vault.StorePassword("Recovery fixture", "relay-test-password"), Hooks = [new("after-connect", "remote", "fixture-ok", [], true, 10), new("after-disconnect", "local", helper, ["--hook-trace", afterTrace, "disconnected"], true, 10)] }; data.Profiles = [profile];
            var publicHost = NativeIdentity.Parse(Environment.GetEnvironmentVariable("RELAY_FIXTURE_HOST_PUBLIC")!); hosts.Accept(new(profile.Host, port, publicHost.Algorithm, publicHost.Fingerprint[7..], DateTime.UtcNow, publicHost.PublicKey));
            string sshId = Guid.NewGuid().ToString("N"), filesId = Guid.NewGuid().ToString("N");
            await Execute("connect", P(new { profileId = profile.Id, sessionId = sshId })); await Execute("connect", P(new { profileId = profile.Id, sessionId = filesId, filesOnly = true }));
            SetRecoveryPolicy(P(new { profileId = profile.Id, enabled = true })); var oldSsh = sessions[sshId]; var oldFiles = sessions[filesId];
            File.WriteAllText(Path.Combine(fixture, "editor.txt"), "original editor fixture"); var editor = JsonSerializer.SerializeToElement(await OpenRemoteEditor(P(new { sessionId = filesId, path = "/editor.txt" })), ProfileStore.Json); var editorId = editor.GetProperty("id").GetString()!; editBuffers[editorId].Dirty = true;
            var local = Path.Combine(dataPath, "fixture-files"); Directory.CreateDirectory(local); var bytes = RandomNumberGenerator.GetBytes(768 * 1024);
            var items = new List<object>(); for (int i = 0; i < 3; i++) { var path = Path.Combine(local, "source-" + i + ".bin"); File.WriteAllBytes(path, bytes); File.WriteAllText(Path.Combine(fixture, "target-" + i + ".bin"), "original target " + i); items.Add(new { local = path, remote = "/target-" + i + ".bin" }); }
            EnqueueFiles(P(new { sessionId = filesId, direction = "upload", conflict = "replace", bandwidthKiB = 256, items })); var jobs = fileJobs.Values.ToArray();
            await Wait(() => jobs[0].Progress > 0 && jobs[1].Request.Checkpoint != null && jobs[1].Cancellation != null); await Execute("filePause", P(new { id = jobs[1].Id })); await Wait(() => jobs[1].Cancellation == null); Check(jobs[1].Status == "In pausa", "manualPauseBeforeOutage");
            File.WriteAllText(Path.Combine(fixture, "drop-network.request"), "fixture"); await Wait(() => !sessions[sshId].Connected && jobs[0].Status == "Attesa connessione" && jobs.All(j => j.Cancellation == null));
            Check(File.ReadAllText(Path.Combine(fixture, "target-0.bin")) == "original target 0", "originalDestinationPreservedDuringOutage");
            Check(jobs[0].Request.Checkpoint != null && File.ReadAllText(Path.Combine(dataPath, "transfers.json")).Contains("checkpoint"), "checkpointPersistsInQueueJournal");
            await Task.Delay(600); var request = P(new { profileId = profile.Id, automatic = true }); var first = RecoverProfile(request); var second = RecoverProfile(request); Check(ReferenceEquals(first, second), "duplicateRecoverySharesSingleTask");
            var result = JsonSerializer.SerializeToElement(await first, ProfileStore.Json); Check(result.GetProperty("success").GetBoolean(), "coordinatedSshSftpRecoverySucceeded");
            Check(!ReferenceEquals(oldSsh, sessions[sshId]) && !ReferenceEquals(oldFiles, sessions[filesId]), "lostSessionsReplacedWithSameIds");
            await Wait(() => jobs[0].Status == "Completato" && jobs[2].Status == "Completato"); Check(jobs[1].Status == "In pausa", "userPausedTransferNeverAutoResumes");
            Check(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture, "target-0.bin"))).SequenceEqual(SHA256.HashData(bytes)), "resumedUploadMatchesOriginalBytes");
            Check(!File.Exists(Path.Combine(fixture, "target-0.bin.relay-" + jobs[0].Id + ".partial")), "completedPartialRemoved");
            Check(editBuffers[editorId].Dirty && editBuffers[editorId].SessionId == filesId, "unsavedEditorRetainsBufferAndOwner");
            Check(File.ReadAllLines(Path.Combine(fixture, "hook-count.txt")).Length == 2 && File.ReadAllText(afterTrace).Length == 0, "recoveryDoesNotReplayHooks");
            var liveSsh = sessions[sshId]; File.WriteAllText(Path.Combine(fixture, "drop-sftp.request"), "fixture"); await Wait(() => sessions[sshId].Connected && !sessions[sshId].FilesAvailable && !sessions[filesId].Connected);
            result = JsonSerializer.SerializeToElement(await RecoverProfile(request), ProfileStore.Json); Check(result.GetProperty("success").GetBoolean() && ReferenceEquals(liveSsh, sessions[sshId]) && liveSsh.CanWrite, "sftpOnlyOutagePreservesLiveSshShell");
            SetRecoveryPolicy(P(new { profileId = profile.Id, enabled = false })); bool rejected = false; try { await RecoverProfile(request); } catch (OperationCanceledException) { rejected = true; }
            Check(rejected, "disabledAutoRecoveryDoesNotConnect");
            SetRecoveryPolicy(P(new { profileId = profile.Id, enabled = true })); vault.Lock(); rejected = false; try { await RecoverProfile(request); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "lockedVaultStopsAutomaticRecovery"); vault.Unlock("Recovery fixture archive password");
            data.Profiles[0] = profile with { Username = "changed" }; rejected = false; try { await RecoverProfile(request); } catch (IOException) { rejected = true; }
            Check(rejected, "changedProfileStopsRecoveryAndRebinding"); data.Profiles[0] = profile;
            var sourceTime = File.GetLastWriteTimeUtc(jobs[1].Request.Local); var changedSource = File.ReadAllBytes(jobs[1].Request.Local); changedSource[^1] ^= 1; File.WriteAllBytes(jobs[1].Request.Local, changedSource); File.SetLastWriteTimeUtc(jobs[1].Request.Local, sourceTime); await Execute("fileResume", P(new { id = jobs[1].Id })); await Wait(() => jobs[1].Cancellation == null); Check(jobs[1].Status == "Errore" && File.ReadAllText(Path.Combine(fixture, "target-1.bin")) == "original target 1", "actualResumeRejectsChangedSource");
            var targetChanged = Path.Combine(fixture, "target-changed.bin"); File.WriteAllText(targetChanged, "original second destination"); EnqueueFiles(P(new { sessionId = filesId, direction = "upload", conflict = "replace", bandwidthKiB = 256, items = new[] { new { local = jobs[0].Request.Local, remote = "/target-changed.bin" } } })); var changedJob = fileJobs.Values.Single(j => j.Request.Remote == "/target-changed.bin"); await Wait(() => changedJob.Progress > 0); await Execute("filePause", P(new { id = changedJob.Id })); await Wait(() => changedJob.Cancellation == null); var targetTime = File.GetLastWriteTimeUtc(targetChanged); File.WriteAllText(targetChanged, "another  second destination"); File.SetLastWriteTimeUtc(targetChanged, targetTime); await Execute("fileResume", P(new { id = changedJob.Id })); await Wait(() => changedJob.Cancellation == null); Check(changedJob.Status == "Errore" && File.ReadAllText(targetChanged) == "another  second destination", "actualResumeRejectsChangedDestination");
            EmitQueue(); fileJobs.Clear(); LoadFileQueue(); Check(fileJobs.Values.Any(j => j.Id == jobs[1].Id && j.Request.Checkpoint == jobs[1].Request.Checkpoint && j.Status == "In pausa" && j.Cancellation == null), "journalReloadPreservesCheckpointWithoutStartingTransfers");
            hosts.Forget(profile.Host, profile.Port); rejected = false; try { await sessions[filesId].TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), jobs[0].Request.Local, "/untrusted-transfer.bin", true, AutomaticRecovery: true), (_, _) => { }, CancellationToken.None); } catch (Renci.SshNet.Common.SshConnectionException ex) when (!NetworkReconnect.IsTransient(ex)) { rejected = true; }
            Check(rejected && !File.Exists(Path.Combine(fixture, "untrusted-transfer.bin")), "automaticTransferRejectsUnknownHostWithoutPrompt"); hosts.Accept(new(profile.Host, port, publicHost.Algorithm, publicHost.Fingerprint[7..], DateTime.UtcNow, publicHost.PublicKey));
            var checkpoint = jobs[0].Request.Checkpoint!; rejected = false; try { checkpoint.Validate(checkpoint with { Target = new(123, DateTime.UtcNow) }); } catch (IOException) { rejected = true; }
            Check(rejected, "changedDestinationCheckpointRejected");
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            foreach (var job in fileJobs.Values) job.Cancellation?.Cancel(); var pending = fileJobs.Values.Where(j => j.Running != null).Select(j => j.Running!).ToArray(); if (pending.Length > 0) try { await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(50)); } catch { }
            foreach (var session in sessions.Values) session.Dispose(); sessions.Clear(); foreach (var id in editBuffers.Keys.ToArray()) CloseEditor(id);
        }
        bool success = error == null && checks.Count == 20 && checks.Values.All(x => x); File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "recovery-smoke-result.json"), JsonSerializer.Serialize(new { success, checks, error }, ProfileStore.Json)); CompleteSmoke(success);
    }
}
