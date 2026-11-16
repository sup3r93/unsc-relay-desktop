using System.Text;
using System.Text.Json;
using UnscRelay.Core;
static class StartupMacroChecks
{
    public static async Task<int> Run()
    {
        int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
        StartupMacro Macro(string text="echo {{value}}")=>new("snippet","Fixture",text,new(){{"value","approved"}},true,false);
        var ssh=new ConnectionProfile{Name="Fixture",Host="127.0.0.1",Username="test",StartupMacro=Macro()};ssh.Validate();
        Check(StartupMacros.Prepare(ssh)=="echo approved","Variables expand from explicit saved values");
        Check(StartupMacros.Prepare(ssh,true)==null,"Automatic recovery never prepares a macro");
        Check(StartupMacros.Prepare(ssh with{StartupMacro=Macro() with{Enabled=false}})==null,"Disabled macro does not execute");
        Reject(()=>(ssh with{StartupMacro=Macro() with{Variables=[]}}).Validate(),"Unfilled active variables rejected before connection");
        Reject(()=>(ssh with{StartupMacro=Macro() with{Variables=new(){{"value","a\nb"}}}}).Validate(),"Control characters in variables rejected");
        Reject(()=>(ssh with{StartupMacro=Macro() with{Text="echo\u001b[31m"}}).Validate(),"Terminal control sequences rejected");
        Reject(()=>(ssh with{Protocol="sftp"}).Validate(),"SFTP profiles cannot enable terminal macros");
        Reject(()=>OpenSshConfig.Export([ssh]),"Endpoint export cannot silently drop a startup macro");
        Check(StartupMacros.ApprovalKey(ssh)!=StartupMacros.ApprovalKey(ssh with{Host="192.0.2.1"}),"Changing target requires new approval");
        Check(StartupMacros.ApprovalKey(ssh)!=StartupMacros.ApprovalKey(ssh with{StartupMacro=Macro() with{Text="pwd"}}),"Changing command requires new approval");
        var recursive=ssh with{StartupMacro=Macro() with{Variables=new(){{"value","{{other}}"}}}};
        Check(StartupMacros.Prepare(recursive)=="echo {{other}}","Variable expansion is single pass");
        Check(JsonSerializer.Deserialize<ConnectionProfile>("{\"name\":\"Old\"}",ProfileStore.Json)!.StartupMacro==null,"Legacy profiles have no active macro");
        var native=ssh with{Protocol="native",Host="",Username="",Native=new NativeOptions{Kind="ssh",Target="test@127.0.0.1"}};native.Validate();
        using(var prepared=LocalLaunch.Direct(Environment.ProcessPath!,["test@127.0.0.1"])){
            NativeClient.AddStartupMacro(prepared,native,StartupMacros.Prepare(native));
            Check(prepared.Arguments.Take(3).SequenceEqual(new[]{"-tt","-o","RemoteCommand=none"}),"Native macro requests a remote PTY and replaces config RemoteCommand");
            Check(prepared.Arguments[^1]=="echo approved\nexec \"${SHELL:-/bin/sh}\" -l","Native macro stays in a single remote command argument and opens the shell");
        }
        Reject(()=>(native with{Native=native.Native with{Kind="sftp"}}).Validate(),"Native SFTP cannot execute startup macros");
        Reject(()=>(native with{Native=native.Native with{Executable=Environment.ProcessPath!}}).Validate(),"External executables cannot inherit native macros");
        Check(StartupMacros.ApprovalKey(native)!=StartupMacros.ApprovalKey(native with{Native=native.Native with{Target="other@127.0.0.1"}}),"Native target change requires fresh approval");
        var root=Path.Combine(Path.GetTempPath(),"relay-macro-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            var source=Path.Combine(root,"import.json");File.WriteAllText(source,JsonSerializer.Serialize(new AppData{Profiles=[ssh]},ProfileStore.Json));var data=new AppData();new ProfileStore(Path.Combine(root,"store")).Import(source,data);
            Check(data.Profiles.Single().StartupMacro==null,"Imported profiles do not activate copied startup macros");
            foreach(var shell in new[]{"cmd","powershell"}){
                var output=Path.Combine(root,shell+".txt");var escaped=output.Replace("'","''");var text=shell=="cmd"?"echo {{value}}>>\""+output+"\"":"Add-Content -LiteralPath '"+escaped+"' -Value '{{value}}'";
                var before=shell=="cmd"?"echo before>\""+output+"\"":"Set-Content -LiteralPath '"+escaped+"' -Value 'before'";
                var profile=new ConnectionProfile{Name="Macro "+shell,Protocol="local",LocalShell=shell,StartupDirectory=root,StartupCommands=before,StartupMacro=Macro(text)};
                var launch=StartupMacros.LocalProfile(profile,StartupMacros.Prepare(profile));Check(launch.StartupMacro==null&&profile.StartupCommands==before,"Local preparation does not mutate the saved profile / "+shell);
                await using var terminal=new LocalTerminal();terminal.Output+=_=>{};await terminal.Start(launch,root,100,30);
                var until=DateTime.UtcNow.AddSeconds(15);string[] lines=[];while(DateTime.UtcNow<until){try{if(File.Exists(output))lines=File.ReadAllLines(output);}catch(IOException){}if(lines.Length>=2)break;await Task.Delay(50);}
                Check(lines.Select(x=>x.Trim()).SequenceEqual(new[]{"before","approved"}),"Real local shell executes macro exactly once after initial commands / "+shell);
            }
            Reject(()=>StartupMacros.LocalProfile(new ConnectionProfile{Protocol="local",StartupCommands=new string('x',8192)},"echo x"),"Combined local commands enforce the length limit");
        }finally{var full=Path.GetFullPath(root);if(full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("relay-macro-check-",StringComparison.Ordinal)){for(var attempt=0;;attempt++){try{Directory.Delete(full,true);break;}catch(IOException)when(attempt<15){await Task.Delay(100);}}}}
        Console.WriteLine("Startup macro checks: "+count);return 0;
    }
}
