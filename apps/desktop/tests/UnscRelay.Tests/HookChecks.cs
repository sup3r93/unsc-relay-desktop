using System.Text.Json;
using UnscRelay.Core;
static class HookChecks
{
    public static async Task<int> Run()
    {
        int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Reject(Action f,string name){try{f();}catch(ArgumentException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
        var folder=Path.Combine(Path.GetTempPath(),"relay-hooks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var path=Path.Combine(folder,"literal args.json");var exe=Environment.ProcessPath!;var hook=new ConnectionHook("before-connect","local",exe,["--hook-helper",path,"literal & $() ` quote \" value"],true);
            await ConnectionHooks.RunLocal(hook);Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(path))!.Single()==hook.Arguments[2],"Local hook passes literal arguments without shell evaluation");
            try{await ConnectionHooks.RunLocal(hook with {Arguments=["--hook-fail"]});throw new Exception("Failure ignored");}catch(IOException){Check(true,"Local nonzero exit reported");}
            try{await ConnectionHooks.RunLocal(hook with {Arguments=["--hook-delay"],TimeoutSeconds=1});throw new Exception("Timeout ignored");}catch(TimeoutException){Check(true,"Local timeout kills only owned hook process tree");}
            File.Delete(path);await ConnectionHooks.RunLocal(hook with {Enabled=false});Check(!File.Exists(path),"Disabled hook does not launch anything");
            Reject(()=>new ConnectionHook("before-connect","remote","command",[],true).Validate(),"Remote pre-connect hook rejected");
            Reject(()=>new ConnectionHook("after-disconnect","remote","command",[],true).Validate(),"Remote hook cannot run after transport closes");
            Reject(()=>(hook with {Command="relative.exe"}).Validate(),"Local executable requires full path");
            Reject(()=>(hook with {Command="C:\\file.cmd"}).Validate(),"CMD file cannot be invoked implicitly");
            Reject(()=>(hook with {TimeoutSeconds=31}).Validate(),"Hook timeout bounded");
            var data=new AppData{Profiles=[new(){Name="fixture",Host="192.0.2.1",Username="test",Subsystem="fixture-sftp",Hooks=[hook]}]};var import=Path.Combine(folder,"profiles-import.json");File.WriteAllText(import,JsonSerializer.Serialize(data,ProfileStore.Json));var store=new ProfileStore(folder);var target=new AppData();store.Import(import,target);
            Check(!target.Profiles.Single().Hooks.Single().Enabled&&target.Profiles.Single().Subsystem=="","Profile import disables hooks and custom remote command before any connection");
            Check(target.Profiles.Single().Id!=data.Profiles.Single().Id,"Imported automation keeps new profile identity");
            Console.WriteLine("Hook checks: "+count);return 0;
        }
        finally{if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(folder).StartsWith("relay-hooks-"))Directory.Delete(folder,true);}
    }
}
