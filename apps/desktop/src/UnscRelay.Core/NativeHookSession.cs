namespace UnscRelay.Core;

// A local process starting is not evidence that SSH authentication succeeded.
public sealed class NativeHookSession
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly ConnectionProfile profile;
    readonly Func<ConnectionHook, Task> execute;
    readonly Action<string> report;
    bool started, authenticated, closed, suppressed;
    public NativeHookSession(ConnectionProfile profile, Action<string> report, Func<ConnectionHook, Task>? execute = null, bool recovering = false)
    {
        profile.Validate();
        if (profile.Protocol != "native" || profile.Native.Kind is not ("ssh" or "sftp") || profile.Native.Executable.Length != 0)
            throw new ArgumentException("Gli hook nativi richiedono OpenSSH incluso.");
        this.profile = profile with { Hooks = recovering ? [] : [.. profile.Hooks] };
        this.report = report;
        this.execute = execute ?? (hook => hook.Kind == "local" ? ConnectionHooks.RunLocal(hook) : Task.FromException(new IOException("Canale remoto OpenSSH non configurato.")));
    }
    async Task Run(string phase, bool required = false)
    {
        foreach (var hook in profile.Hooks.Where(h => h.Enabled && h.Phase == phase))
            try { await execute(hook); }
            catch (Exception ex) { if (required) throw; report("Hook " + phase + ": " + ex.Message); }
    }
    public async Task Start()
    {
        await gate.WaitAsync();
        try { if (closed || started) throw new InvalidOperationException("Ciclo hook gia avviato o chiuso."); started = true; await Run("before-connect", true); }
        finally { gate.Release(); }
    }
    public async Task<bool> Authenticated()
    {
        await gate.WaitAsync();
        try { if (!started || closed) return false; if (authenticated) return true; authenticated = true; await Run("after-connect"); return true; }
        finally { gate.Release(); }
    }
    public async Task BeforeClose(bool runHooks)
    {
        await gate.WaitAsync();
        try { suppressed |= !runHooks; if (!closed && authenticated && runHooks) await Run("before-disconnect"); closed = true; }
        finally { gate.Release(); }
    }
    bool finished;
    public async Task Finished(bool runHooks)
    {
        await gate.WaitAsync();
        try { closed = true; if (finished) return; finished = true; if (authenticated && runHooks && !suppressed) await Run("after-disconnect"); }
        finally { gate.Release(); }
    }
    public static void ConfigureSftpTransport(LocalLaunch launch, string helper)
    {
        var index = launch.Arguments.IndexOf("-S");
        if(index < 0 || index + 1 >= launch.Arguments.Count) throw new ArgumentException("Trasporto SFTP incluso non disponibile.");
        var ssh = Path.Combine(Path.GetDirectoryName(launch.Executable)!, "ssh.exe");
        if(!File.Exists(ssh)) throw new IOException("Client SSH incluso mancante.");
        launch.EnvironmentVariables["RELAY_SFTP_TRANSPORT"] = ssh;
        launch.Arguments[index+1] = NativeClient.CygwinPath(helper);
    }
    public static void Configure(LocalLaunch launch, string helper, string? posixShell = null)
    {
        // The inherited Windows SHELL may be PowerShell. Use our included POSIX shell explicitly.
        var shell = posixShell ?? Path.Combine(Path.GetDirectoryName(launch.Executable)!, "bash.exe");
        if (!File.Exists(shell)) throw new IOException("Shell inclusa mancante: ripara il pacchetto OpenSSH.");
        launch.EnvironmentVariables["SHELL"] = NativeClient.CygwinPath(shell);
        // LocalCommand is invoked by OpenSSH after authentication; no remote output is parsed.
        // Escape percent before OpenSSH token expansion and shell-quote the complete helper path.
        var path = NativeClient.CygwinPath(helper).Replace("%", "%%");
        var quoted = "'" + path.Replace("'", "'\\''") + "'";
        launch.Arguments.InsertRange(0, ["-o", "PermitLocalCommand=yes", "-o", "LocalCommand=" + quoted + " --native-bridge authenticated >/dev/null"]);
    }
}
