using System.Text;
using UnscRelay.Core;

internal static class RouteChecks
{
    public static async Task<int> Run(int rootPort, int jumpPort, int targetPort)
    {
        var count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { Check(true, message); return; } throw new Exception(message); }
        var root = new ConnectionProfile { Id = "root", Name = "Root fixture", Host = "127.0.0.1", Port = rootPort, Username = "test" };
        var jump = root with { Id = "jump", Name = "Jump fixture", Port = jumpPort, JumpHost = root.Id };
        var target = root with { Id = "target", Name = "Target fixture", Host = "localhost", Port = targetPort, JumpHost = jump.Id, TmuxSession = "relay_fixture" };
        ConnectionProfile[] profiles = [root, jump, target];
        Check(SshRoutes.Resolve(target, profiles).Select(p => p.Id).SequenceEqual(new[] { "root", "jump", "target" }), "Jump chain resolves in transport order");
        Reject(() => SshRoutes.Resolve(target, [root with { JumpHost = target.Id }, jump, target]), "Jump cycles rejected before opening sockets");
        Reject(() => SshRoutes.Resolve(target, [root, target]), "Deleted jump profile rejected without direct fallback");
        Reject(() => SshRoutes.Resolve(target, [root, jump with { Protocol = "rdp" }, target]), "Non SSH jump host rejected");
        Reject(() => (target with { TmuxSession = "name; touch injected" }).Validate(), "Shell metacharacters rejected in tmux names");
        new Workspace("fixture", "tabs", [root.Id], ["Root tab"]).Validate();
        Reject(() => new Workspace("fixture", "tabs", [root.Id], []).Validate(), "Workspace names must match saved profile sequence");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "route-fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var hosts = new HostKeyStore(Path.Combine(directory, "hostkeys.json")); var opened = new List<SshSession>(); var accepted = new List<HostKey>();
        Task<SshSession> Create(ConnectionProfile profile, string? endpoint, int port)
        {
            var session = new SshSession(profile, "relay-test-password", null, "", hosts, key => { accepted.Add(key); return true; }, _ => "", endpointHost: endpoint, endpointPort: port);
            opened.Add(session); return Task.FromResult(session);
        }
        using (var session = await SshRoutes.CreateAsync(target, profiles, Create))
        {
            var output = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously); var text = new StringBuilder();
            session.Output += bytes => { text.Append(Encoding.UTF8.GetString(bytes)); if (text.ToString().Contains("route-marker")) output.TrySetResult(text.ToString()); };
            await session.ConnectAsync(100, 30); session.Write("route-marker\r");
            Check((await output.Task.WaitAsync(TimeSpan.FromSeconds(10))).Contains("route-marker"), "Interactive SSH traverses two jump hosts");
            Check(session.FilesAvailable && (await session.ListAsync(".")).Length >= 0, "SFTP uses the same chained route");
            Check(accepted.Any(h => h.Host == target.Host && h.Port == target.Port) && accepted.Any(h => h.Port == rootPort) && accepted.Any(h => h.Port == jumpPort), "Each hop verified with original host and port, not temporary loopback endpoint");
            Check(text.ToString().Contains("tmux new-session -A -s relay_fixture"), "Explicit tmux command reaches the remote terminal");
        }
        Check(opened.All(s => !s.Connected), "Closing target disposes all owned jump connections");
        var desktop = target with { Id = "desktop", Protocol = "rdp", Username = "desktop-user", TmuxSession = "" };
        Reject(() => DesktopRoutes.Validate(desktop with { JumpHost = "missing" }, profiles), "Missing desktop jump rejected without direct fallback");
        Reject(() => DesktopRoutes.Validate(desktop with { JumpHost = desktop.Id }, profiles), "Desktop self route rejected");
        Reject(() => DesktopRoutes.Validate(desktop, [root, jump with { Protocol = "vnc" }]), "Desktop route rejects non SSH bastions");
        using (var directRoute = await DesktopRoutes.OpenAsync(desktop with { JumpHost = "" }, profiles, (_, _, _) => throw new Exception("Direct route opened SSH")))
            Check(directRoute.Host == desktop.Host && directRoute.Port == targetPort, "Direct desktop route keeps the original endpoint without SSH");
        foreach (var protocol in new[] { "rdp", "vnc" })
        {
            using (var route = await DesktopRoutes.OpenAsync(desktop with { Protocol = protocol }, profiles, Create))
            {
                using var socket = new System.Net.Sockets.TcpClient(); await socket.ConnectAsync(route.Host, route.Port);
                var bytes = new byte[256]; var size = await socket.GetStream().ReadAsync(bytes).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                Check(route.Host == "127.0.0.1" && route.Port != targetPort && Encoding.ASCII.GetString(bytes, 0, size).StartsWith("SSH-"), protocol + " raw desktop transport reaches target through two SSH bastions");
                Check(desktop.Host == "localhost" && desktop.Port == targetPort, "Desktop target identity remains unchanged in profile");
            }
            Check(opened.All(s => !s.Connected), protocol + " closes all owned SSH transports");
        }
        using var fixtureRsa = System.Security.Cryptography.RSA.Create(2048); var fixturePrivateKey = Encoding.UTF8.GetBytes(fixtureRsa.ExportRSAPrivateKeyPem());
        var direct = target with { JumpHost = "", TmuxSession = "", Authentication = "password" };
        using (var passwordOnly = new SshSession(direct, "wrong-password", fixturePrivateKey, "", hosts, _ => true, _ => ""))
        {
            var rejected = false; try { await passwordOnly.ConnectAsync(100, 30); } catch { rejected = true; }
            Check(rejected, "Explicit password method never falls back to an available SSH key");
        }
        using (var passwordIgnoresKey = new SshSession(direct, "relay-test-password", [1, 2, 3], "", hosts, _ => true, _ => ""))
        {
            await passwordIgnoresKey.ConnectAsync(100, 30); Check(passwordIgnoresKey.Connected, "Password login ignores unrelated private-key input");
        }
        using (var keyOnly = new SshSession(direct with { Authentication = "key", KeyId = "fixture", Username = "password-only" }, "relay-test-password", fixturePrivateKey, "", hosts, _ => true, _ => ""))
        {
            var rejected = false; try { await keyOnly.ConnectAsync(100, 30); } catch { rejected = true; }
            Check(rejected, "Explicit key method never silently falls back to password");
        }
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(fixturePrivateKey);
        var entry = hosts.List().Single(h => h.Host == target.Host && h.Port == target.Port); hosts.Forget(entry.Host, entry.Port); hosts.Accept(entry with { Fingerprint = "changed-fixture-key" });
        using (var session = await SshRoutes.CreateAsync(target, profiles, Create))
        {
            var rejected = false; try { await session.ConnectAsync(100, 30); } catch { rejected = true; }
            Check(rejected && hosts.List().Single(h => h.Host == target.Host && h.Port == target.Port).Fingerprint == "changed-fixture-key", "Changed target host key refused through jump route");
        }
        Check(opened.All(s => !s.Connected), "Failed target handshake also closes owned route");
        Console.WriteLine($"{count} route/session checks passed"); return 0;
    }
}
