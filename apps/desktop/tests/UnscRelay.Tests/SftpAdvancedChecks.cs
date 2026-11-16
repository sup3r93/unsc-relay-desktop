using System.Security.Cryptography;
using System.Text;
using UnscRelay.Core;

internal static class SftpAdvancedChecks
{
    public static async Task<int> Run(int port)
    {
        var root = Environment.GetEnvironmentVariable("RELAY_FIXTURE_ROOT") ?? throw new Exception("Missing loopback fixture");
        var local = Path.Combine(Path.GetTempPath(), "relay-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(local); var passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
        async Task Reject(Func<Task> action, string name) { try { await action(); } catch (IOException) { Check(true, name); return; } throw new Exception("Accepted: " + name); }
        try
        {
            var data = new AppData { Machines = [new("fixture", "Loopback", "127.0.0.1", port)] }; data.Machines[0].Validate();
            var store = new ProfileStore(local); store.Save(data); Check(store.Load().Machines.Single().Port == port, "Named endpoint survives profile persistence");
            var profile = new ConnectionProfile { Host = "127.0.0.1", Port = port, Username = "test", Protocol = "sftp", Authentication = "password" };
            using var session = new SshSession(profile, "relay-test-password", null, "", new(Path.Combine(local, "hosts.json")), _ => true, _ => throw new Exception("No challenge expected"));
            await session.ConnectAsync(100, 30);
            File.WriteAllText(Path.Combine(root, "metadata-fixture.txt"), "metadata fixture");
            var metadata = System.Text.Json.JsonSerializer.SerializeToElement(await session.ListAsync("."));
            Check(metadata.EnumerateArray().Any(), "SFTP metadata fixture has entries");
            foreach (var entry in metadata.EnumerateArray()) {
                Check(entry.GetProperty("permissions").GetString() is { Length: 10 }, "SFTP listing includes symbolic permissions");
                Check(entry.TryGetProperty("owner", out _) && entry.TryGetProperty("group", out _), "SFTP listing includes owner and group identifiers");
            }
            Check(FilePresentation.WindowsAttributes(FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.Archive)=="RHA", "Windows file attributes use compact flags");
 Check(session.Connected && session.FilesAvailable, "Standalone SFTP session connects without terminal");
            using (var invalid = new SshSession(profile, "wrong fixture password", null, "", new(Path.Combine(local, "hosts.json")), _ => true, _ => ""))
            {
                try { await invalid.ConnectAsync(80, 24); throw new Exception("Invalid SFTP authentication accepted"); }
                catch (Renci.SshNet.Common.SshAuthenticationException) { Check(!invalid.Connected, "Standalone SFTP reports authentication failure"); }
            }
            using (var subsystem = new SshSession(profile with { Subsystem = "unsupported" }, "relay-test-password", null, "", new(Path.Combine(local, "hosts.json")), _ => true, _ => ""))
            {
                try { await subsystem.ConnectAsync(80, 24); throw new Exception("Unsupported subsystem accepted"); }
                catch (Renci.SshNet.Common.SshException) { Check(!subsystem.Connected, "Rejected custom SFTP command fails without standard fallback"); }
            }
            using(var custom=new SshSession(profile with {Subsystem="sudo -n fixture-sftp"},"relay-test-password",null,"",new(Path.Combine(local,"hosts.json")),_=>true,_=>""))
            {
                await custom.ConnectAsync(80,24);Check(custom.FilesAvailable,"Custom remote exec starts the existing SFTP engine");
                var customSource=Path.Combine(local,"custom.txt");File.WriteAllText(customSource,"custom subsystem fixture");
                await custom.TransferAdvancedAsync(new TransferRequest(Guid.NewGuid().ToString("N"),customSource,"/custom.txt",true,"replace"),(_,_)=>{},CancellationToken.None);
                Check(File.ReadAllText(Path.Combine(root,"custom.txt"))=="custom subsystem fixture","Additional transfer channel also uses custom SFTP command");
                Check(File.ReadAllLines(Path.Combine(root,"exec-count.txt")).Length>=2,"Browser and transfer both issue remote exec, not standard subsystem");
                await custom.RunRemoteHook(new("after-connect","remote","fixture-ok",[],true));Check(true,"Remote hook drains stdout and stderr through existing SSH transport");
                try{await custom.RunRemoteHook(new("after-connect","remote","fixture-fail",[],true));throw new Exception("Hook failure accepted");}catch(IOException){Check(true,"Nonzero remote hook exit reported");}
                try{await custom.RunRemoteHook(new("after-connect","remote","fixture-wait",[],true,1));throw new Exception("Hook timeout ignored");}catch(Exception e)when(e is OperationCanceledException or Renci.SshNet.Common.SshOperationTimeoutException){Check(true,"Remote hook timeout cancels execution");}
            }
            var bytes = RandomNumberGenerator.GetBytes(1024 * 1024); var source = Path.Combine(local, "source.bin"); File.WriteAllBytes(source, bytes);
            var id = Guid.NewGuid().ToString("N"); var request = new TransferRequest(id, source, "/large.bin", true, "replace");
            using var cancel = new CancellationTokenSource();
            try { await session.TransferAdvancedAsync(request, (done, _) => { if (done >= 131072) cancel.Cancel(); }, cancel.Token); throw new Exception("Cancellation failed"); } catch (OperationCanceledException) { }
            Check(!File.Exists(Path.Combine(root, "large.bin")) && File.Exists(Path.Combine(root, "large.bin.relay-" + id + ".partial")), "Pause retains owned partial without publishing destination");
            await session.TransferAdvancedAsync(request with { Resume = true }, (_, _) => { }, CancellationToken.None);
            Check(File.ReadAllBytes(Path.Combine(root, "large.bin")).SequenceEqual(bytes), "Upload resumes after verified prefix");
            var download = Path.Combine(local, "download.bin"); var downloadId = Guid.NewGuid().ToString("N"); File.WriteAllText(download, "original"); using var pause = new CancellationTokenSource();
            var down = new TransferRequest(downloadId, download, "/large.bin", false, "replace");
            try { await session.TransferAdvancedAsync(down, (done, _) => { if (done >= 131072) pause.Cancel(); }, pause.Token); } catch (OperationCanceledException) { }
            Check(File.ReadAllText(download) == "original", "Interrupted download preserves existing destination");
            await session.TransferAdvancedAsync(down with { Resume = true }, (_, _) => { }, CancellationToken.None); Check(File.ReadAllBytes(download).SequenceEqual(bytes), "Download resumes with verified prefix");
            var badId = Guid.NewGuid().ToString("N"); File.WriteAllBytes(download + ".relay-" + badId + ".partial", new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(root, "existing.bin"), bytes[..131072]);
            await session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), source, "/existing.bin", true, "resume"), (_, _) => { }, CancellationToken.None);
            Check(File.ReadAllBytes(Path.Combine(root, "existing.bin")).SequenceEqual(bytes), "Resume existing destination verifies before staging");
            File.WriteAllText(Path.Combine(root, "wrong.bin"), "different content");
            await Reject(() => session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), source, "/wrong.bin", true, "resume"), (_, _) => { }, CancellationToken.None), "Resume refuses unrelated existing destination");
            Check(File.ReadAllText(Path.Combine(root, "wrong.bin")) == "different content", "Rejected resume preserves existing remote file");
            var skipped = await session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), source, "/wrong.bin", true, "skip"), (_, _) => { }, CancellationToken.None);
            Check(!skipped && File.ReadAllText(Path.Combine(root, "wrong.bin")) == "different content", "Skip reports no transfer and preserves destination");
            var limitedSource = Path.Combine(local, "limited.bin"); File.WriteAllBytes(limitedSource, bytes[..131072]); var clock = System.Diagnostics.Stopwatch.StartNew();
            await session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), limitedSource, "/limited.bin", true, "new", BandwidthKiB: 128), (_, _) => { }, CancellationToken.None);
            Check(clock.ElapsedMilliseconds >= 850 && File.ReadAllBytes(Path.Combine(root, "limited.bin")).SequenceEqual(bytes[..131072]), "Bandwidth cap paces copied data without changing content");
            await Reject(() => session.TransferAdvancedAsync(down with { Id = badId, Resume = true }, (_, _) => { }, CancellationToken.None), "Corrupted partial is rejected");
            await Reject(() => session.TransferAdvancedAsync(down with { Id = Guid.NewGuid().ToString("N"), Conflict = "new" }, (_, _) => { }, CancellationToken.None), "Do-not-overwrite preserves existing local file");
            var textPath = Path.Combine(root, "edit.txt"); File.WriteAllText(textPath, "one\r\ntwo\r\n", new UTF8Encoding(true));
            var edit = await session.ReadTextAsync("/edit.txt"); Check(edit.Bom && edit.Text.Contains("\r\n"), "Editor preserves UTF-8 BOM and CRLF");
            File.WriteAllText(textPath, "changed externally"); var draft = Path.Combine(local, "draft.txt"); File.WriteAllText(draft, "replacement");
            await Reject(() => session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), draft, "/edit.txt", true, "replace", ExpectedHash: edit.Hash), (_, _) => { }, CancellationToken.None), "Editor refuses stale remote content");
            Check(File.ReadAllText(textPath) == "changed externally", "Conflict leaves remote changes intact");
            edit = await session.ReadTextAsync("/edit.txt"); await session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), draft, "/edit.txt", true, "replace", ExpectedHash: edit.Hash), (_, _) => { }, CancellationToken.None);
            Check(File.ReadAllText(textPath) == "replacement", "Fresh editor revision publishes atomically");
            await session.FileOperationAsync("mkdir", "/folder", ""); Check(Directory.Exists(Path.Combine(root, "folder")), "Remote folder creation");
            await session.FileOperationAsync("rename", "/edit.txt", "/renamed.txt"); Check(File.Exists(Path.Combine(root, "renamed.txt")), "Remote rename");
            await session.FileOperationAsync("delete", "/renamed.txt", ""); Check(!File.Exists(Path.Combine(root, "renamed.txt")), "Remote explicit deletion");
            await Task.WhenAll(Enumerable.Range(0, 2).Select(index => session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"), source, "/parallel-" + index, true, "new"), (_, _) => { }, CancellationToken.None)));
            Check(Enumerable.Range(0, 2).All(index => File.ReadAllBytes(Path.Combine(root, "parallel-" + index)).SequenceEqual(bytes)), "Independent SFTP channels transfer in parallel");
            File.WriteAllBytes(Path.Combine(root, "binary.dat"), [0, 1, 2]); await Reject(() => session.ReadTextAsync("/binary.dat"), "Editor rejects binary files");
            var guardSource=Path.Combine(local,"guard-source");var guardTarget=Path.Combine(local,"guard-target");
            File.WriteAllText(guardSource,"new data");File.WriteAllText(Path.Combine(root,"guard.txt"),"old data");
            string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
            var sourceHash=Hash(guardSource);var targetHash=Hash(Path.Combine(root,"guard.txt"));
            await Reject(()=>session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"),guardSource,"/guard.txt",true,"replace",ExpectedHash:targetHash,ExpectedSourceHash:"wrong"),(_,_)=>{},CancellationToken.None),"Sync rejects changed source before publishing");
            Check(File.ReadAllText(Path.Combine(root,"guard.txt"))=="old data","Rejected sync preserves target bytes");
            await Reject(()=>session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"),guardSource,"/guard.txt",true,"replace",ExpectedSourceHash:sourceHash,ExpectedMissingTarget:true),(_,_)=>{},CancellationToken.None),"Sync rejects destination appearing after preview");
            File.WriteAllText(guardTarget,"local change");
            await Reject(()=>session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"),guardTarget,"/guard.txt",false,"replace",ExpectedHash:"wrong",ExpectedSourceHash:targetHash),(_,_)=>{},CancellationToken.None),"Download sync checks local target hash");
            Check(File.ReadAllText(guardTarget)=="local change","Download conflict preserves local target");
            await session.TransferAdvancedAsync(new(Guid.NewGuid().ToString("N"),guardSource,"/guard.txt",true,"replace",ExpectedHash:targetHash,ExpectedSourceHash:sourceHash),(_,_)=>{},CancellationToken.None);
            Check(File.ReadAllText(Path.Combine(root,"guard.txt"))=="new data","Guarded sync replaces unchanged target");
            Console.WriteLine($"{passed} SFTP checks passed"); return 0;
        }
        finally { Directory.Delete(local, true); }
    }
}
