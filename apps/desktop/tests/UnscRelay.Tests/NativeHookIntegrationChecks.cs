using System.Security.Cryptography;
using System.Text;
using UnscRelay.Core;
static class NativeHookIntegrationChecks
{
    public static async Task<int> Run(string tools,string helper,int port)
    {
        var bin=Path.Combine(Path.GetFullPath(tools),"Cygwin","bin");helper=Path.GetFullPath(helper);
        var root=Path.Combine(Path.GetTempPath(),"relay-native-hooks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            int count=0;void Check(bool value,string text){if(!value)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
            foreach(var kind in new[]{"ssh","sftp"})
            foreach(var valid in new[]{true,false})
            {
                var trace=new List<string>();var profile=new ConnectionProfile{Protocol="native",Native=new(){Kind=kind,Target="fixture"},Hooks=[..new[]{"before-connect","after-connect","after-disconnect"}.Select(phase=>new ConnectionHook(phase,"local",@"C:\fixture.exe",[],true))]};
                var lifecycle=new NativeHookSession(profile,_=>{},h=>{trace.Add(h.Phase);return Task.CompletedTask;});
                var requests=new List<string>();
                var hosts=new HostKeyStore(Path.Combine(root,"hosts.json"));
                await using var bridge=new NativeBridge(async req=>{requests.Add(req.Action);return req.Action switch{
                    "authenticated" when req.Arguments.Length==0=>new NativeReply(await lifecycle.Authenticated()),
                    "host"=>new NativeReply(true,NativeIdentity.KnownHost(req,hosts,_=>true)),
                    "ask"=>new NativeReply(true,valid?"relay-test-password":"wrong-fixture-password"),
                    _=>new NativeReply(false)};});
                var config=Path.Combine(root,"config");await File.WriteAllTextAsync(config,NativePolicy.Configuration(helper,"")+"Host fixture\n HostName 127.0.0.1\n Port "+port+"\n User password-only\n PreferredAuthentications password\n PubkeyAuthentication no\n NumberOfPasswordPrompts 1\n",new UTF8Encoding(false));
                await NativeClient.Capture(Path.Combine(bin,"chmod.exe"),["600",NativeClient.CygwinPath(config)]);
                var arguments=kind=="ssh"?new[]{"-F",NativeClient.CygwinPath(config),"fixture","fixture-ok"}:new[]{"-S",NativeClient.CygwinPath(Path.Combine(bin,"ssh.exe")),"-F",NativeClient.CygwinPath(config),"fixture"};
                using var launch=LocalLaunch.Direct(Path.Combine(bin,kind+".exe"),arguments);
                NativeHookSession.Configure(launch,helper);if(kind=="sftp")NativeHookSession.ConfigureSftpTransport(launch,helper);bridge.Apply(launch.EnvironmentVariables);
                launch.EnvironmentVariables["SSH_ASKPASS"]=NativeClient.CygwinPath(helper);launch.EnvironmentVariables["SSH_ASKPASS_REQUIRE"]="force";launch.EnvironmentVariables["DISPLAY"]="relay";launch.EnvironmentVariables["RELAY_ASKPASS_HELPER"]="1";launch.EnvironmentVariables["PATH"]=bin+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH");
                var upload=Path.Combine(root,"binary source.bin");var download=Path.Combine(root,"binary downloaded.bin");
                if(kind=="sftp"&&valid)await File.WriteAllBytesAsync(upload,RandomNumberGenerator.GetBytes(131073));
                string SftpPath(string path)=>"\""+NativeClient.CygwinPath(path).Replace("\"","\\\"")+"\"";
                await lifecycle.Start();bool success=false;
                try{var psi=new System.Diagnostics.ProcessStartInfo(launch.Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in launch.Arguments)psi.ArgumentList.Add(arg);foreach(var pair in launch.EnvironmentVariables)psi.Environment[pair.Key]=pair.Value;using var process=System.Diagnostics.Process.Start(psi)!;if(kind=="sftp")await process.StandardInput.WriteAsync("pwd\n"+(valid?"put "+SftpPath(upload)+" relay-native-hook-binary.bin\nget relay-native-hook-binary.bin "+SftpPath(download)+"\n":"")+"quit\n");process.StandardInput.Close();var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));}catch(TimeoutException){try{process.Kill(true);await process.WaitForExitAsync();}catch(InvalidOperationException){}throw;}var output=await stdout;var diagnostic=await stderr;if(valid&&requests.All(x=>x!="authenticated"))Console.WriteLine("Fixture diagnostic: "+diagnostic);success=process.ExitCode==0&&output.Contains(kind=="ssh"?"fixture hook output":"Remote working directory:");}catch(IOException)when(!valid){}
                await lifecycle.Finished(true);
                Check(success==valid,valid?"Original bundled "+kind+" authenticates and completes remote request":"Original bundled "+kind+" rejects incorrect password");
                if(kind=="sftp"&&valid){var sourceHash=SHA256.HashData(await File.ReadAllBytesAsync(upload));var destinationHash=File.Exists(download)?SHA256.HashData(await File.ReadAllBytesAsync(download)):[];Check(sourceHash.SequenceEqual(destinationHash),"SFTP transport preserves binary upload/download content with spaced paths");}
                Check(trace.SequenceEqual(valid?new[]{"before-connect","after-connect","after-disconnect"}:new[]{"before-connect"}),valid?"Real OpenSSH LocalCommand invokes authenticated app bridge exactly once: "+string.Join(",",trace)+" / "+string.Join(",",requests):"Failed real authentication cannot invoke post-login hooks");
            }
            Console.WriteLine("Native hook integration checks: "+count);return 0;
        }
        finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("relay-native-hooks-"))Directory.Delete(root,true);}
    }
}
