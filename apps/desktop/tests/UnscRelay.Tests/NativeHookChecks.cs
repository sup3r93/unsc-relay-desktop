using UnscRelay.Core;
static class NativeHookChecks
{
    public static async Task<int> Run()
    {
        int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        var events=new List<string>();
        var profile=new ConnectionProfile{Protocol="native",Native=new(){Kind="ssh",Target="fixture"},Hooks=[..new[]{"before-connect","after-connect","before-disconnect","after-disconnect"}.Select(phase=>new ConnectionHook(phase,"local",@"C:\fixture.exe",[],true))]};
        NativeHookSession Create(Func<ConnectionHook,Task>? run=null)=>new(profile,_=>{},run??(hook=>{events.Add(hook.Phase);return Task.CompletedTask;}));
        var hooks=Create();await hooks.Start();Check(events.SequenceEqual(new[]{"before-connect"}),"Starting a process never triggers authenticated hooks");
        await Task.WhenAll(Enumerable.Range(0,8).Select(_=>hooks.Authenticated()));Check(events.Count(x=>x=="after-connect")==1,"Concurrent authenticated notifications execute hook exactly once");
        await hooks.BeforeClose(true);await hooks.BeforeClose(true);await Task.WhenAll(hooks.Finished(true),hooks.Finished(true));
        Check(events.SequenceEqual(new[]{"before-connect","after-connect","before-disconnect","after-disconnect"}),"Graceful close runs each local phase once in order");
        Check(!await hooks.Authenticated(),"Late notification cannot reopen closed lifecycle");
        events.Clear();hooks=Create();await hooks.Start();await hooks.Finished(true);Check(events.SequenceEqual(new[]{"before-connect"}),"Failed authentication cannot execute after-connect or disconnect hooks");
        events.Clear();hooks=Create();await hooks.Start();await hooks.Authenticated();await hooks.Finished(true);Check(events.SequenceEqual(new[]{"before-connect","after-connect","after-disconnect"}),"Unexpected network or process exit skips before-disconnect");
        events.Clear();hooks=Create();await hooks.Start();await hooks.Authenticated();await hooks.BeforeClose(false);await hooks.Finished(true);Check(events.SequenceEqual(new[]{"before-connect","after-connect"}),"Update suppression survives process-exit callback races");
        var notice=new List<string>();hooks=new(profile,notice.Add,hook=>hook.Phase=="before-connect"?Task.CompletedTask:Task.FromException(new IOException("fixture")));await hooks.Start();Check(await hooks.Authenticated(),"Post-connect hook failure does not undo successful authentication");await hooks.BeforeClose(true);await hooks.Finished(true);Check(notice.Count==3,"Hook failures reported for every nonfatal phase");
        hooks=Create(_=>Task.FromException(new IOException("fixture")));try{await hooks.Start();throw new Exception("Pre-hook failure ignored");}catch(IOException){Check(true,"Pre-connect failure aborts launch");}await hooks.Finished(false);Check(!await hooks.Authenticated(),"Aborted launch rejects authentication callback");
        profile.Validate();Check(true,"Bundled OpenSSH permits local lifecycle hooks");
        var remoteTrace=new List<string>();var remoteProfile=profile with{Hooks=[new("after-connect","remote","true",[],true),new("before-disconnect","remote","true",[],true)]};
        hooks=new(remoteProfile,_=>{},h=>{remoteTrace.Add(h.Phase+":"+h.Kind);return Task.CompletedTask;});await hooks.Start();await hooks.Authenticated();await hooks.BeforeClose(true);await hooks.Finished(true);Check(remoteTrace.SequenceEqual(new[]{"after-connect:remote","before-disconnect:remote"}),"Remote native hooks execute only during authenticated lifecycle");
        foreach(var invalid in new[]{profile with{Native=profile.Native with{Executable=@"C:\custom-ssh.exe"}},profile with{Native=profile.Native with{Kind="telnet"}}})
            try{invalid.Validate();throw new Exception("Invalid native automation accepted");}catch(ArgumentException){Check(true,"Unsupported native automation rejected before launch");}
        using var launch=LocalLaunch.Direct(Environment.ProcessPath!,["fixture"],null);NativeHookSession.Configure(launch,@"C:\user's 100%\Relay.exe",Environment.ProcessPath!);
        Check(launch.Arguments.Contains("PermitLocalCommand=yes"),"Authentication signal explicitly enabled");
        var command=launch.Arguments.Single(x=>x.StartsWith("LocalCommand="));Check(command.Contains("'\\''")&&command.Contains("100%%")&&command.EndsWith("--native-bridge authenticated >/dev/null"),"Helper path safely quoted for shell and OpenSSH token expansion");
        Check(launch.EnvironmentVariables["SHELL"]==NativeClient.CygwinPath(Environment.ProcessPath!),"Hook shell is explicit instead of inherited PowerShell");
        Check(launch.Arguments.Last()=="fixture","Authentication signal preserves target argument");
        Console.WriteLine("Native hook checks: "+count);return 0;
    }
}
