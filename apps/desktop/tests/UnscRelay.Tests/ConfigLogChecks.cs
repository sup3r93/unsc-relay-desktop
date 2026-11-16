using System.Text;
using UnscRelay.Core;
static class ConfigLogChecks
{
    public static int Run()
    {
        int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(Exception ex)when(ex is IOException or ArgumentException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
        var root=Path.Combine(Path.GetTempPath(),"relay-config-log-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var profile=new ConnectionProfile{Name="lab",Host="192.0.2.50",Username="fixture",KeepAliveSeconds=0};
            var config=Path.Combine(root,"config");var original=Encoding.UTF8.GetBytes("# personal comment\r\nUser fallback\r\nHost personal\r\n  HostName 192.0.2.10\r\n  User student\r\n");original=[..Encoding.UTF8.Preamble,..original];File.WriteAllBytes(config,original);
            var plan=new OpenSshConfigEdit(config,[profile]);Check(File.ReadAllBytes(config).SequenceEqual(original),"Preview does not write original");
            Check(plan.After.EndsWith(new UTF8Encoding(false,true).GetString(original.AsSpan(3)),StringComparison.Ordinal),"Unmanaged text preserved as exact suffix");
            var backup=plan.Apply();Check(File.ReadAllBytes(backup).SequenceEqual(original),"Backup preserves original bytes and BOM");
            var saved=File.ReadAllBytes(config);Check(saved.AsSpan().StartsWith(Encoding.UTF8.Preamble)&&!plan.After.Replace("\r\n","").Contains('\n'),"UTF8 BOM and CRLF retained");
            var parsed=OpenSshConfig.Parse(File.ReadAllText(config));Check(parsed.Candidates.Single(c=>c.Alias=="lab").Profile?.KeepAliveSeconds==0,"Managed endpoint roundtrips disabled keepalive");
            Check(parsed.Candidates.Single(c=>c.Alias=="personal").Profile?.Username=="fallback","Unmanaged global first-value semantics retained");
            var changed=new OpenSshConfigEdit(config,[profile with {Port=2222}]);var nextBackup=changed.Apply();Check(File.ReadAllBytes(nextBackup).SequenceEqual(saved),"Managed block replacement backs up previous whole file");
            Check(File.ReadAllText(config).Split("# BEGIN UNSC RELAY").Length==2,"Managed block is replaced rather than duplicated");
            var race=new OpenSshConfigEdit(config,[profile]);File.AppendAllText(config,"# user edit");var concurrent=File.ReadAllBytes(config);Reject(()=>race.Apply(),"Concurrent config edit blocks apply");Check(File.ReadAllBytes(config).SequenceEqual(concurrent),"Concurrent changes remain untouched");
            Reject(()=>new OpenSshConfigEdit(config,[profile with {Name="personal"}]),"Existing personal alias cannot be replaced");
            File.WriteAllText(config,"Include other\n");Reject(()=>new OpenSshConfigEdit(config,[profile]),"Includes are preserved by refusing managed edit");
            File.WriteAllText(config,"Match exec never-run\n");Reject(()=>new OpenSshConfigEdit(config,[profile]),"Match commands never evaluated");
            File.WriteAllText(config,"# END UNSC RELAY MANAGED ENDPOINTS\n");Reject(()=>new OpenSshConfigEdit(config,[profile]),"Ambiguous marker rejected");
            File.WriteAllBytes(config,[0xff,0xff]);Reject(()=>new OpenSshConfigEdit(config,[profile]),"Non UTF8 file rejected without reencoding");
            var newPath=Path.Combine(root,"newconfig");var create=new OpenSshConfigEdit(newPath,[profile]);File.WriteAllText(newPath,"created by user");Reject(()=>create.Apply(),"Newly created destination is never overwritten");
            var fresh=new OpenSshConfigEdit(Path.Combine(root,"fresh"),[profile]);Check(fresh.Apply()==""&&File.Exists(fresh.PathName),"New config created without fictitious backup");
            var forwards=OpenSshConfig.Parse("Host lab\n User fixture\n ServerAliveInterval 60\n LocalForward 127.0.0.1:8080 [::1]:80\n LocalForward 9090 db.internal:5432\n RemoteForward 4545 127.0.0.1:22\n DynamicForward 1080\n").Candidates.Single().Profile!;
            Check(forwards.KeepAliveSeconds==60&&forwards.Tunnels.Count==4&&forwards.Tunnels.All(t=>!t.Automatic),"All forwarding directives imported only as inactive rules");
            Check(forwards.Tunnels[0].Destination=="::1"&&forwards.Tunnels[2].Kind=="remote"&&forwards.Tunnels[3].Kind=="socks","IPv6 targets and tunnel types retained");
            foreach(var directive in new[]{"LocalForward 0.0.0.0:8080 host:80","RemoteForward *:8080 host:80","DynamicForward [::1]:1080","LocalForward 0 host:80","LocalForward 8080 /tmp/socket"})Check(OpenSshConfig.Parse("Host bad\n User fixture\n "+directive).Candidates.Single().Profile==null,"Reject unsupported or public bind: "+directive);
            Check(OpenSshConfig.Parse("Host lab\n User fixture\n ServerAliveInterval 4000").Candidates.Single().Profile==null,"Keepalive outside supported bounds rejected");
            var route=OpenSshConfig.Parse("Host bastion\n User fixture\nHost target\n User admin\n ProxyJump bastion\n");
            var imported=OpenSshConfig.SelectForImport(route,["target"]);Check(imported.Length==2&&SshRoutes.Resolve(imported.Single(p=>p.Name=="target"),imported).Select(p=>p.Name).SequenceEqual(new[]{"bastion","target"}),"Jump dependency auto included with remapped IDs");
            Check(imported.All(p=>route.Candidates.All(c=>c.Profile?.Id!=p.Id)),"Imported jump IDs are fresh and self contained");
            Check(OpenSshConfig.Parse("Host a\n User fixture\n ProxyJump b\nHost b\n User fixture\n ProxyJump a\n").Candidates.All(c=>c.Profile==null),"Cyclic jump aliases rejected");
            Check(OpenSshConfig.Parse("Host target\n User fixture\n ProxyJump missing\n").Candidates.Single().Profile==null,"Missing jump alias rejected");
            Check(OpenSshConfig.Parse("Host target\n User fixture\n ProxyJump user@host:22\n").Candidates.Single().Profile==null,"Unrepresentable inline jump rejected");
            Check(OpenSshConfig.Parse("Host target\n User fixture\n ProxyJump none\n").Candidates.Single().Profile?.JumpHost=="","ProxyJump none remains direct");
            var utf=Encoding.UTF8.GetBytes("Città\r\n");var replay=new RecordingReplay("fixture",[new(0,Convert.ToBase64String(utf[..5])),new(1,Convert.ToBase64String([..utf[5..],..Encoding.UTF8.GetBytes("\x1b[31mRED\x1b[0m\x1b]52;c;secret\aok\rprogress\n")]))],1);
            var text=RecordingText.Export(replay);Check(Encoding.UTF8.GetString(text)=="Città\r\nREDok\r\nprogress\r\n","Split UTF8 decoded; CSI OSC clipboard and controls removed");
            var split=new RecordingReplay("fixture",[new(0,Convert.ToBase64String(Encoding.UTF8.GetBytes("before\x1b]0;hid"))),new(1,Convert.ToBase64String(Encoding.UTF8.GetBytes("den\x1b\\after\tend\0")))],1);
            Check(Encoding.UTF8.GetString(RecordingText.Export(split))=="beforeafter\tend","Escape parser spans frames and removes NUL");
            var log=Path.Combine(root,"log.txt");RecordingText.SaveNew(log,text);Reject(()=>RecordingText.SaveNew(log,Encoding.UTF8.GetBytes("overwrite")),"Existing log cannot be overwritten");Check(File.ReadAllBytes(log).SequenceEqual(text),"Existing text remains intact");Reject(()=>RecordingText.SaveNew(Path.Combine(root,"keys.vault"),text),"Only TXT export permitted");
            Console.WriteLine(count+" config/log checks passed");return 0;
        }
        finally{if(Path.GetFileName(root).StartsWith("relay-config-log-fixture-")&&Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
    }
}
