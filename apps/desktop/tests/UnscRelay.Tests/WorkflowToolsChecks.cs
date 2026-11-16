using UnscRelay.Core;

static class WorkflowToolsChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        void Reject(Action action, string name) { try { action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { Check(true, name); return; } throw new Exception("Accepted: " + name); }
        var shortcut=new Snippet("one","Fixture","echo {{name}}","Test","Ctrl+Alt+Shift+A");
        SnippetCatalog.ValidateShortcut(shortcut,[]);Check(true,"Valid explicit snippet shortcut accepted");
        Reject(()=>SnippetCatalog.ValidateShortcut(shortcut with{Id="two"},[shortcut]),"Duplicate shortcut rejected without replacing original");
        Reject(()=>SnippetCatalog.Validate(shortcut with{Shortcut="Ctrl+K"}),"Reserved arbitrary shortcut rejected");
        SnippetCatalog.ValidateShortcut(shortcut,[shortcut]);Check(true,"Editing own shortcut is allowed");
        var importedSnippet=SnippetCatalog.Import(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[]{shortcut},ProfileStore.Json)).Single();
        Check(importedSnippet.Shortcut==""&&importedSnippet.Id!=shortcut.Id,"Imported snippets never activate foreign shortcuts");
        var oldSnippet=System.Text.Json.JsonSerializer.Deserialize<Snippet>("{\"id\":\"legacy\",\"name\":\"Old\",\"text\":\"pwd\"}",ProfileStore.Json)!;
        Check(oldSnippet.Shortcut=="","Old snippets load without shortcut");
        var preview = OpenSshConfig.Parse("# fixture\nHost lab backup\n HostName=192.0.2.10 # comment\n User \"student\"\n Port 48221\n IdentityFile \"C:/fixtures/key file\"\nHost *\n User default\n Port 22\n ConnectTimeout 30\n");
        Check(preview.Candidates.Length == 2 && preview.Candidates.All(c => c.Profile != null), "Explicit aliases resolved without inventing wildcard machines");
        var lab = preview.Candidates[0].Profile!;
        Check(lab.Host == "192.0.2.10" && lab.Port == 48221 && lab.Username == "student" && lab.ConnectTimeoutSeconds == 30, "First obtained values, equals, quotes, comments and defaults");
        Check(lab.KeyId == "" && lab.PasswordId == "" && preview.Candidates[0].Warnings.Any(w => w.Contains("key file")), "IdentityFile is metadata only, no key or password import");
        var precedence = OpenSshConfig.Parse("Host *\n User first\nHost lab\n User second\n");
        Check(precedence.Candidates.Single().Profile!.Username == "first", "Earlier global value wins over later specific value");
        var negative = OpenSshConfig.Parse("Host * !lab\n User default\nHost lab\n User student\nHost other\n");
        Check(negative.Candidates.Single(c=>c.Alias=="lab").Profile!.Username == "student" && negative.Candidates.Single(c=>c.Alias=="other").Profile!.Username == "default", "Negated Host patterns exclude matching defaults");
        var unsafeConfig = OpenSshConfig.Parse("Include ~/.ssh/*\nHost lab\n User test\nMatch exec \"never-run\"\n User root");
        Check(unsafeConfig.Candidates.All(c=>c.Profile==null) && unsafeConfig.Warnings.Count() >= 2, "Include and Match block import without reading files or executing commands");
        var unsupported = OpenSshConfig.Parse("Host advanced\n User test\n ProxyCommand never-run\nHost basic\n User test");
        Check(unsupported.Candidates[0].Profile == null && unsupported.Candidates[1].Profile != null, "Unsupported routing excludes only affected endpoint");
        Check(OpenSshConfig.Parse("Host missing\n HostName test\n").Candidates.Single().Profile == null, "Missing username cannot create a usable profile silently");
        Check(OpenSshConfig.Parse("Host invalid\n User test\n Port 70000").Candidates.Single().Profile == null, "Invalid port rejected in preview");
        Reject(()=>OpenSshConfig.Parse("Host lab\n User \"unfinished"), "Unclosed quotes rejected");
        Reject(()=>OpenSshConfig.Parse(new string('x',1048577)), "Oversized config rejected");
        var exported = OpenSshConfig.Export([lab, lab with {Id="second"}]);
        var roundtrip = OpenSshConfig.Parse(exported);
        Check(roundtrip.Candidates.Length == 2 && roundtrip.Candidates.All(c=>c.Profile?.Host==lab.Host), "Export roundtrip and unique aliases");
        Check(!exported.Contains("IdentityFile") && !exported.Contains("PasswordId"), "Export contains no vault references or key paths");
        Reject(()=>OpenSshConfig.Export([lab with {Host="test\nProxyCommand evil"}]), "Export rejects directive injection");
        Reject(()=>OpenSshConfig.Export([lab with {JumpHost="bastion"}]), "Export cannot silently drop jump routing");
        Reject(()=>OpenSshConfig.Export([lab with {RequirePostQuantumKex=true}]), "Export cannot silently drop required cryptography");
        var targets = new[] {new TerminalTarget("a","A","192.0.2.1",22,"test",true,"ssh"),new TerminalTarget("b","B","192.0.2.2",22,"test",true,"ssh"),new TerminalTarget("f","Files","192.0.2.3",22,"test",true,"sftp"),new TerminalTarget("d","Down","192.0.2.4",22,"test",false,"ssh")};
        Check(MultiSend.Validate(targets,["b","a"],"echo fixture").Select(t=>t.Id).SequenceEqual(["b","a"]), "Multi-send preserves explicit recipient order");
        Reject(()=>MultiSend.Validate(targets,[],"test"), "No implicit recipients");
        Reject(()=>MultiSend.Validate(targets,["a","a"],"test"), "No duplicate sends");
        Reject(()=>MultiSend.Validate(targets,["a","d"],"test"), "Disconnected recipient rejects entire plan before sending");
        Reject(()=>MultiSend.Validate(targets,["a","f"],"test"), "SFTP is never a terminal recipient");
        Reject(()=>MultiSend.Validate(targets,["unknown"],"test"), "Closed or unknown recipient rejected");
        Reject(()=>MultiSend.Validate(targets,["a"],"\x1b[31m"), "Hidden control sequences rejected");
        Reject(()=>MultiSend.Validate(targets,["a"],new string('x',32769)), "Oversized multi-send rejected");
        Check(MultiSend.Validate(targets,["a"],"one\ntwo\tthree").Length==1, "Explicit multiline text supported");
        Check(NetworkReconnect.IsTransient(new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.TimedOut)), "Network timeout is retryable");
        Check(!NetworkReconnect.IsTransient(new Renci.SshNet.Common.SshAuthenticationException("fixture")), "Authentication failure never retried automatically");
        Check(!NetworkReconnect.IsTransient(new InvalidOperationException("vault locked")), "Vault or configuration errors never retried automatically");
        Check(!NetworkReconnect.IsTransient(new Renci.SshNet.Common.SshConnectionException("host rejected",Renci.SshNet.Messages.Transport.DisconnectReason.HostKeyNotVerifiable)), "Host identity rejection never retried automatically");
        Console.WriteLine("Workflow checks: " + count); return 0;
    }
}
