using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;

public partial class MainWindow
{
    readonly HashSet<string> automaticRecoveryProfiles = [];
    readonly HashSet<string> brokenSessions = [];
    readonly Dictionary<string, string> sessionRouteSnapshots = [];
    readonly HashSet<string> recoveringProfiles = [];
    readonly Dictionary<string, Task<object>> recoveryTasks = [];
    string RouteSnapshot(ConnectionProfile profile) => JsonSerializer.Serialize(SshRoutes.Resolve(profile, data.Profiles), ProfileStore.Json);
    void WireManagedSession(SshSession session)
    {
        sessionRouteSnapshots.TryAdd(session.Id, RouteSnapshot(session.Profile));
        session.Output += bytes => { BufferAssistantOutput(session.Id, bytes); RecordOutput(session.Id, bytes); Emit("output", new { sessionId = session.Id, data = Convert.ToBase64String(bytes) }); };
        session.Status += text => Dispatcher.BeginInvoke(() =>
        {
            if (!sessions.TryGetValue(session.Id, out var live) || !ReferenceEquals(live, session)) return;
            if (!session.Connected) { StopRecordingSession(session.Id); SessionEnded(session.Id); }
            Emit("status", new { sessionId = session.Id, text, connected = session.Connected, filesAvailable = session.FilesAvailable });
        });
        session.TransportInterrupted += error => Dispatcher.BeginInvoke(() =>
        {
            if (sessions.TryGetValue(session.Id, out var live) && ReferenceEquals(live, session)) MarkRecoveryNeeded(session.Id, error);
        });
    }
    void PauseRecoveryJobs(string profileId)
    {
        foreach (var job in fileJobs.Values.Where(j => j.ProfileId == profileId && j.Status is "In coda" or "Trasferimento"))
        {
            job.RecoveryPaused = true; job.Status = job.Cancellation == null ? "Attesa connessione" : "Pausa richiesta"; job.Cancellation?.Cancel();
        }
        EmitQueue();
    }
    void MarkRecoveryNeeded(string sessionId, Exception error)
    {
        if (closing || updating || !sessions.TryGetValue(sessionId, out var session) || recoveringProfiles.Contains(session.Profile.Id)) return;
        brokenSessions.Add(sessionId); PauseRecoveryJobs(session.Profile.Id);
        Emit("recoveryNeeded", new { profileId = session.Profile.Id, sessionId, retryable = true, text = "Connessione interrotta: coda in attesa. " + error.Message });
    }
    object SetRecoveryPolicy(JsonElement payload)
    {
        var profileId = S(payload, "profileId"); var profile = data.Profiles.Single(p => p.Id == profileId);
        if (profile.Protocol is not ("ssh" or "sftp")&&!NativeRecovery.Supported(profile)) throw new ArgumentException("Ripristino coordinato disponibile per SSH e SFTP integrati.");
        bool enabled = payload.GetProperty("enabled").GetBoolean();
        if (enabled) automaticRecoveryProfiles.Add(profileId); else automaticRecoveryProfiles.Remove(profileId);
        return new { enabled };
    }
    void ValidateRecovery(SshSession previous, bool automatic)
    {
        var saved = data.Profiles.SingleOrDefault(p => p.Id == previous.Profile.Id) ?? throw new IOException("Profilo eliminato: apri una nuova connessione.");
        var current = saved with { Protocol = previous.Profile.Protocol };
        if (!sessionRouteSnapshots.TryGetValue(previous.Id, out var snapshot) || snapshot != RouteSnapshot(current)) throw new IOException("Profilo o jump host modificato: apri una nuova connessione. La coda resta in pausa.");
        if (automatic && !automaticRecoveryProfiles.Contains(saved.Id)) throw new OperationCanceledException("Riconnessione automatica disattivata.");
        foreach (var hop in SshRoutes.Resolve(current, data.Profiles))
        {
            if ((hop.KeyId.Length > 0 || hop.PasswordId.Length > 0) && !vault.IsUnlocked) throw new InvalidOperationException("Archivio bloccato: sbloccalo e riprendi manualmente.");
            if (hop.KeyId.Length > 0) { var key = vault.List().SingleOrDefault(k => k.Id == hop.KeyId) ?? throw new InvalidOperationException("Chiave non disponibile nell’archivio."); if (key.Certificate != null) vault.ValidateCertificateAccess(key.Certificate, hop.Username); }
        }
    }
    Task<object> RecoverProfile(JsonElement payload)
    {
        var profileId = S(payload, "profileId");
        if (recoveryTasks.TryGetValue(profileId, out var running)) return running;
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously); recoveryTasks[profileId] = completion.Task;
        _ = Run(); return completion.Task;
        async Task Run() { try { completion.SetResult(await RecoverProfileCore(profileId, payload)); } catch (Exception error) { completion.SetException(error); } finally { recoveryTasks.Remove(profileId); } }
    }
    async Task<object> RecoverProfileCore(string profileId, JsonElement payload)
    {
        bool automatic = payload.TryGetProperty("automatic", out var a) && a.GetBoolean();
        var savedNative=data.Profiles.SingleOrDefault(p=>p.Id==profileId);
        if(savedNative!=null&&NativeRecovery.Supported(savedNative))return await RecoverNativeProfile(savedNative,payload);
        var group = sessions.Values.Where(s => s.Profile.Id == profileId && s.Profile.Protocol is "ssh" or "sftp").ToArray();
        if (group.Length == 0) throw new IOException("Nessuna sessione da ripristinare. Riapri la connessione.");
        foreach (var previous in group) ValidateRecovery(previous, automatic);
        if (EditorsBusy || ExportBusy || transferActive) throw new IOException("Attendi la fine delle operazioni dell’editor o dell’esportazione e riprova.");
        recoveringProfiles.Add(profileId); PauseRecoveryJobs(profileId);
        try
        {
            var active = fileJobs.Values.Where(j => j.ProfileId == profileId && j.Running is { IsCompleted: false }).Select(j => j.Running!).ToArray();
            if (active.Length > 0) await Task.WhenAll(active).WaitAsync(TimeSpan.FromSeconds(60));
            var failures = new List<Exception>(); var restarted = new HashSet<string>();
            foreach (var previous in group)
            {
                try
                {
                    ValidateRecovery(previous, automatic);
                    if (previous.Connected && previous.FilesAvailable && !brokenSessions.Contains(previous.Id)) continue;
                    if (previous.Connected)
                    {
                        await Task.Run(() => previous.ReconnectFilesAsync(automatic)); ValidateRecovery(previous, automatic); brokenSessions.Remove(previous.Id);
                    }
                    else
                    {
                        SshSession? replacement = null;
                        try
                        {
                            replacement = await SshRoutes.CreateAsync(previous.Profile, data.Profiles, (p, host, port) => CreateAuthenticatedSession(p, host, port, p.Id == profileId ? previous.Id : Guid.NewGuid().ToString("N"), automatic));
                            // A recovered shell never replays startup input or hooks. tmux attachment remains explicit in the profile.
                            WireManagedSession(replacement);
                            await Task.Run(() => replacement.ConnectAsync(I(payload, "cols", 100), I(payload, "rows", 30))); ValidateRecovery(previous, automatic);
                            sessions[previous.Id] = replacement; StopRecordingSession(previous.Id); await Task.Run(previous.Dispose); brokenSessions.Remove(previous.Id); restarted.Add(previous.Id); replacement = null;
                            if (!sessions[previous.Id].FilesAvailable) throw sessions[previous.Id].LastFilesError ?? new IOException("SSH ripristinato, ma SFTP non disponibile: riconnetti manualmente.");
                        }
                        finally { replacement?.Dispose(); }
                    }
                }
                catch (Exception error) { failures.Add(error); }
            }
            bool success = failures.Count == 0;
            if (success)
            {
                foreach (var previous in group) ValidateRecovery(sessions[previous.Id], automatic);
                foreach (var job in fileJobs.Values.Where(j => j.ProfileId == profileId && j.RecoveryPaused && j.Status == "Attesa connessione")) if (sessions.TryGetValue(job.SessionId, out var owner)) CheckJobEndpoint(job, owner);
                foreach (var job in fileJobs.Values.Where(j => j.ProfileId == profileId && j.RecoveryPaused && j.Cancellation == null && j.Status == "Attesa connessione").ToArray())
                {
                    if (!sessions.TryGetValue(job.SessionId, out var owner) || !owner.FilesAvailable) continue;
                    CheckJobEndpoint(job, owner); job.RecoveryPaused = false; job.Running = RunJob(job, true, automatic);
                }
            }
            var result = new { success, retryable = failures.Count > 0 && failures.All(NetworkReconnect.IsTransient), error = string.Join(" / ", failures.Select(e => e.Message).Distinct()), sessions = group.Select(old => { var current = sessions[old.Id]; return new { sessionId = old.Id, connected = current.Connected, filesAvailable = current.FilesAvailable, terminalRestarted = restarted.Contains(old.Id) }; }).ToArray() };
            EmitQueue(); Emit("recoveryResult", new { profileId, result }); return result;
        }
        catch (TimeoutException) { throw new IOException("I trasferimenti non si sono ancora arrestati. Attendi e riprova; i file parziali sono conservati."); }
        finally { recoveringProfiles.Remove(profileId); }
    }
}
