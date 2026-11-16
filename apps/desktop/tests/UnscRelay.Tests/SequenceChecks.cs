using UnscRelay.Core;
static class SequenceChecks
{
    public static int Run()
    {
        int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        var fixedSettings=SequencePolicy.Apply(new(false,false,0,0,17));
        Check(fixedSettings.StartupVideo&&fixedSettings.ShutdownVideo,"Old disabled sequence preferences are ignored");
        Check(fixedSettings.StartupVolume==0&&fixedSettings.ShutdownVolume==0,"Zero volumes are preserved without disabling sequences");
        var independent=SequencePolicy.Apply(new(StartupVolume:.23,ShutdownVolume:.61,AppMusicVolume:.42));
        Check(independent.StartupVolume==.23&&independent.ShutdownVolume==.61&&independent.AppMusicVolume==.42,"Three independent volumes survive normalization");
        var invalid=SequencePolicy.Apply(new(StartupVolume:double.NaN,ShutdownVolume:double.PositiveInfinity,AppMusicVolume:-1));
        Check(invalid.StartupVolume==.8&&invalid.ShutdownVolume==1&&invalid.AppMusicVolume==0,"Invalid stored volumes normalize safely");
        var legacy=System.Text.Json.JsonSerializer.Deserialize<UserSettings>("{}",ProfileStore.Json)!;
        Check(legacy.StartupVolume==.8&&legacy.ShutdownVolume==1&&legacy.AppMusicVolume==.35,"Old settings retain prior default levels");
        Check(fixedSettings.AutoLockMinutes==17,"Unrelated vault preference preserved");
        Check(!SequencePolicy.CanFinish(TimeSpan.FromSeconds(1),false),"Early completion cannot skip normal playback");
        Check(SequencePolicy.CanFinish(TimeSpan.FromSeconds(5),false),"Five-second completed playback accepted");
        Check(SequencePolicy.CanFinish(TimeSpan.Zero,true),"Real media failure does not trap user indefinitely");
        Console.WriteLine("Sequence policy checks: "+count);return 0;
    }
}
