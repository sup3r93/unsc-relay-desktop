using System.Diagnostics;
using System.Text;
using UnscRelay.Core;
static class NativeControlChecks
{
    public static async Task<int> Run(string tools,string helper,int port)
    {
        var bin=Path.Combine(Path.GetFullPath(tools),"Cygwin","bin");
        var root=Path.Combine(Path.GetTempPath(),"relay-control-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            int count=0;void Check(bool value,string text){if(!value)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
            var hosts=new HostKeyStore(Path.Combine(root,"hosts.json"));
            await using var bridge=new NativeBridge(req=>Task.FromResult(req.Action switch{
                "host"=>new NativeReply(true,NativeIdentity.KnownHost(req,hosts,_=>true)),
                "ask"=>new NativeReply(true,"relay-test-password"),_=>new NativeReply(false)}));
            var config=Path.Combine(root,"config");await File.WriteAllTextAsync(config,NativePolicy.Configuration(helper,"")+"Host fixture\n HostName 127.0.0.1\n Port "+port+"\n User password-only\n PreferredAuthentications password\n PubkeyAuthentication no\n NumberOfPasswordPrompts 1\n",new UTF8Encoding(false));
            await NativeClient.Capture(Path.Combine(bin,"chmod.exe"),["600",NativeClient.CygwinPath(config)]);
            await using var control=await NativeControlSession.Create(Path.Combine(bin,"ssh.exe"));
            using var launch=LocalLaunch.Direct(Path.Combine(bin,"ssh.exe"),["-F",NativeClient.CygwinPath(config),"-N","fixture"]);
            bridge.Apply(launch.EnvironmentVariables);launch.EnvironmentVariables["SSH_ASKPASS"]=NativeClient.CygwinPath(helper);launch.EnvironmentVariables["SSH_ASKPASS_REQUIRE"]="force";launch.EnvironmentVariables["DISPLAY"]="relay";launch.EnvironmentVariables["RELAY_ASKPASS_HELPER"]="1";launch.EnvironmentVariables["PATH"]=bin+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH");
            control.Configure(launch);
            var psi=new ProcessStartInfo(launch.Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in launch.Arguments)psi.ArgumentList.Add(arg);foreach(var pair in launch.EnvironmentVariables)psi.Environment[pair.Key]=pair.Value;
            using var master=Process.Start(psi)!;var stdout=master.StandardOutput.ReadToEndAsync();var stderr=master.StandardError.ReadToEndAsync();
            try
            {
                await control.WaitReady();Check(await control.ResolveDirectory(".")=="/fixture-root","Native directory resolution uses the same authenticated master");Check(!master.HasExited,"Bundled master authenticates using the app-owned bridge");
                await Task.WhenAll(Enumerable.Range(0,3).Select(_=>control.RunRemoteHook(new("after-connect","remote","fixture-ok",[],true))));Check(!master.HasExited,"Parallel remote hooks reuse the master without terminal input");
                try{await control.RunRemoteHook(new("after-connect","remote","fixture-fail",[],true));throw new Exception("Nonzero ignored");}catch(IOException){Check(true,"Remote hook exit status is propagated without retaining output");}
                try{await control.RunRemoteHook(new("after-connect","remote","fixture-wait",[],true,1));throw new Exception("Timeout ignored");}catch(TimeoutException){Check(true,"Remote hook timeout terminates only its own channel");}
                await control.RunRemoteHook(new("before-disconnect","remote","fixture-ok",[],true));Check(!master.HasExited,"Master remains usable after a timed-out channel");
                var rclone=Path.Combine(Path.GetFullPath(tools),"Rclone","rclone.exe");
                await using(var files=new StorageFiles(rclone,control,"."))
                {
                    var source=Path.Combine(root,"binary spaced source.bin");var target=Path.Combine(root,"download.bin");
                    await File.WriteAllBytesAsync(source,System.Security.Cryptography.RandomNumberGenerator.GetBytes(131073));
                    await files.Change("mkdir","native files");await files.Transfer("native files/payload.bin",source,true,false);
                    var entries=await files.List("native files");Check(entries.Any(e=>e.Name=="payload.bin"&&e.Size==131073),"Graphical SFTP engine lists uploaded files through native proxy");
                    await files.Transfer("native files/payload.bin",target,false,false);var sourceBytes=await File.ReadAllBytesAsync(source);var targetBytes=await File.ReadAllBytesAsync(target);Check(sourceBytes.SequenceEqual(targetBytes),"Native SFTP binary transfer preserves exact bytes with spaced paths");
                    var textSource=Path.Combine(root,"comparison.txt");
                    var utf8=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("caffè · 日本語\r\nseconda riga\r\n")).ToArray();
                    await File.WriteAllBytesAsync(textSource,utf8);await files.Transfer("native files/comparison.txt",textSource,true,false);
                    var text=await files.ReadText("native files/comparison.txt");
                    Check(text.Bom&&text.Text=="caffè · 日本語\r\nseconda riga\r\n","Native comparison preserves Unicode, BOM and CRLF");
                    Check(text.Hash==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(utf8)),"Comparison hashes original bytes including BOM");
                    await files.Transfer("native files/comparison.txt",target,false,true);Check((await File.ReadAllBytesAsync(target)).SequenceEqual(utf8),"Comparison leaves remote contents unchanged");
                    await File.WriteAllBytesAsync(textSource,[]);await files.Transfer("native files/empty.txt",textSource,true,false);Check((await files.ReadText("native files/empty.txt")).Text=="","Native comparison accepts empty text");
                    foreach(var sample in new[]{("binary.txt",new byte[]{65,0,66}),("invalid.txt",new byte[]{0xc3,0x28}),("large.txt",new byte[2097153])}){
                        await File.WriteAllBytesAsync(textSource,sample.Item2);await files.Transfer("native files/"+sample.Item1,textSource,true,false);
                        try{await files.ReadText("native files/"+sample.Item1);throw new Exception("Unsafe comparison accepted");}catch(IOException){Check(true,"Comparison rejects "+sample.Item1);}
                        await files.Change("deletefile","native files/"+sample.Item1);
                    }
                    try{await files.ReadText("native files");throw new Exception("Directory comparison accepted");}catch(IOException){Check(true,"Comparison rejects directories");}
                    await files.Change("deletefile","native files/comparison.txt");await files.Change("deletefile","native files/empty.txt");
                    await files.Change("moveto","native files/payload.bin","native files/renamed.bin");await files.Change("deletefile","native files/renamed.bin");Check((await files.List("native files")).Length==0,"Native SFTP rename and delete operate on the authenticated channel");
                }
                await using(var custom=new StorageFiles(rclone,control,".","sudo -n fixture-sftp")){Check((await custom.List("")).Any(e=>e.Name=="native files"),"Custom SFTP server command uses a separate native exec channel");}
                var nativeProfile=new ConnectionProfile{Name="Native fixture",Host="127.0.0.1",Port=port,Username="password-only",Protocol="native",Native=new(){Kind="ssh"},RemoteDirectory="/"};
                using(var advanced=await SshSession.OpenNativeFiles(nativeProfile,control))
                {
                    try{await advanced.ConnectAsync(80,24);throw new Exception("Native file transport reopened");}catch(InvalidOperationException){Check(true,"Native file sessions cannot start an independent SSH transport");}
                    Check(advanced.FilesAvailable,"Advanced native file engine uses the authenticated subsystem");
                    var local=Path.Combine(root,"advanced.txt");var downloaded=Path.Combine(root,"advanced-download.txt");
                    var original=Encoding.UTF8.GetBytes("native editor · 日本語\r\n");await File.WriteAllBytesAsync(local,original);
                    var upload=new TransferRequest(Guid.NewGuid().ToString("N"),local,"/advanced.txt",true,"new");
                    Check(await advanced.TransferAdvancedAsync(upload,(_,_)=>{},CancellationToken.None),"Advanced native upload commits a verified staged file");
                    var opened=await advanced.ReadTextAsync(upload.Remote);Check(opened.Text==Encoding.UTF8.GetString(original),"Native editor reads Unicode through an independent master channel");
                    await File.WriteAllTextAsync(local,"changed editor text\n",new UTF8Encoding(false));
                    try{await advanced.TransferAdvancedAsync(upload with{Id=Guid.NewGuid().ToString("N"),Conflict="replace",ExpectedHash="wrong"},(_,_)=>{},CancellationToken.None);throw new Exception("Editor conflict accepted");}catch(IOException){Check((await advanced.ReadTextAsync(upload.Remote)).Hash==opened.Hash,"Native editor conflict preserves the remote original");}
                    var download=new TransferRequest(Guid.NewGuid().ToString("N"),downloaded,upload.Remote,false,"new");
                    Check(await advanced.TransferAdvancedAsync(download,(_,_)=>{},CancellationToken.None)&&(await File.ReadAllBytesAsync(downloaded)).SequenceEqual(original),"Advanced native download preserves exact bytes");
                    var big=Path.Combine(root,"resume.bin");await File.WriteAllBytesAsync(big,System.Security.Cryptography.RandomNumberGenerator.GetBytes(524289));
                    var paused=new TransferRequest(Guid.NewGuid().ToString("N"),big,"/resume.bin",true,"new");TransferCheckpoint? saved=null;
                    using(var cancel=new CancellationTokenSource())
                    {
                        try{await advanced.TransferAdvancedAsync(paused,(done,_)=>{if(done>=65536)cancel.Cancel();},cancel.Token,c=>saved=c);throw new Exception("Pause ignored");}catch(OperationCanceledException){Check(saved!=null,"Native transfer pause retains the source and target checkpoint");}
                    }
                    Check(await advanced.TransferAdvancedAsync(paused with{Resume=true,Checkpoint=saved},(_,_)=>{},CancellationToken.None),"Native verified resume completes its own partial file");
                    await advanced.DiscardPartialAsync(paused);
                    await advanced.FileOperationAsync("delete",upload.Remote,"");await advanced.FileOperationAsync("delete",paused.Remote,"");
                }
                using(var customAdvanced=await SshSession.OpenNativeFiles(nativeProfile with{Subsystem="sudo -n fixture-sftp"},control))Check(customAdvanced.FilesAvailable,"Advanced native engine supports the configured subsystem command");
                var auth=Path.Combine(Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT")!,"control-auth.txt");Check(File.ReadAllLines(auth).Length==1,"All channels share exactly one SSH authentication");
                master.Kill(true);await master.WaitForExitAsync();await Task.WhenAll(stdout,stderr);
                try{await control.RunRemoteHook(new("after-connect","remote","fixture-ok",[],true));throw new Exception("Disconnected master accepted");}catch(IOException){Check(true,"Missing master fails closed without a new connection");}
                try{using var closed=await control.OpenSftp();throw new Exception("Closed subsystem accepted");}catch(Renci.SshNet.Common.SshConnectionException){Check(true,"Advanced native subsystem fails closed after master loss");}
                Check(File.ReadAllLines(auth).Length==1,"No authentication fallback after transport loss");
                Check(control.ProxyArguments.Contains("proxy")&&control.ProxyArguments.Contains("-O"),"Proxy mode never requests unsupported descriptor passing");
            }
            finally{if(!master.HasExited){master.Kill(true);await master.WaitForExitAsync();}await Task.WhenAll(stdout,stderr);}
            Console.WriteLine("Native control checks: "+count);return 0;
        }
        finally{Directory.Delete(root,true);}
    }
}
