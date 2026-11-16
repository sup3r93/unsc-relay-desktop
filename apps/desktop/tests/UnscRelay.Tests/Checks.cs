using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using UnscRelay.Core;

internal static class Checks
{
    public static async Task<int> Run(string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Relay-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var passed = 0;
        void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
        void Reject<T>(Action action, string name) where T : Exception { try { action(); } catch (T) { Assert(true, name); return; } throw new Exception("Not rejected: " + name); }
        try
        {
            FolderChecks.Run(directory, Assert);
            PasskeyChecks.Run(directory, Assert);
            WindowsHelloChecks.Run(directory, Assert);
            var p = new ConnectionProfile { Name = "Test", Host = "127.0.0.1", Username = "test" };
            p.Validate(); Reject<ArgumentException>(() => (p with { Port = 0 }).Validate(), "Invalid SSH port rejected");
            Reject<ArgumentException>(() => (p with { Host = "-bad host" }).Validate(), "Invalid hostname rejected");
            var store = new ProfileStore(directory); var data = new AppData { Profiles = [p] }; store.Save(data);
            Assert(store.Load().Profiles.Single().Host == p.Host, "Profiles round trip");
            var bad = Path.Combine(directory, "bad.json");
            File.WriteAllText(bad, System.Text.Json.JsonSerializer.Serialize(new AppData { Profiles = [p, p with { Port = -1 }] }, ProfileStore.Json));
            Reject<ArgumentException>(() => store.Import(bad, data), "Invalid import rejected");
            Assert(data.Profiles.Count == 1 && store.Load().Profiles.Count == 1, "Import failure preserves memory and disk");
            var hosts = new HostKeyStore(Path.Combine(directory, "hosts.json"));
            Assert(hosts.Check("host", 22, "ed25519", "one") == HostKeyState.Unknown, "Unknown host requires trust");
            hosts.Accept(new("host", 22, "ed25519", "one", DateTime.UtcNow));
            Assert(hosts.Check("HOST", 22, "ed25519", "one") == HostKeyState.Trusted, "Accepted host recognized");
            Assert(hosts.Check("host", 22, "ed25519", "two") == HostKeyState.Changed, "Changed host key rejected");
            Assert(hosts.Check("host", 23, "ed25519", "one") == HostKeyState.Unknown, "Trust separated by port");
            var vaultPath = Path.Combine(directory, "keys.vault");
            using var vault = new KeyVault(vaultPath);
            Reject<ArgumentException>(() => vault.Unlock("short"), "Short master password rejected");
            const string password = "Test-only master passphrase 123!";
            vault.Unlock(password); Assert(vault.IsUnlocked, "Vault created");
            using var rsa = RSA.Create(2048); var key = Encoding.UTF8.GetBytes(rsa.ExportRSAPrivateKeyPem());
            var info = vault.Import("fixture-key", key); var firstNonce = JsonNode.Parse(File.ReadAllText(vaultPath))!["Nonce"]!.ToString();
            Assert(vault.Read(info.Id).SequenceEqual(key), "Imported SSH key preserved");
            Assert(!File.ReadAllText(vaultPath).Contains("fixture-key") && !File.ReadAllText(vaultPath).Contains("PRIVATE KEY"), "Vault metadata and key are encrypted");
            vault.Lock(); Reject<InvalidOperationException>(() => vault.Read(info.Id), "Locked vault blocks key read");
            Reject<CryptographicException>(() => vault.Unlock("Incorrect test password 123!"), "Wrong master password rejected");
            vault.Unlock(password); Assert(vault.List().Single().Id == info.Id, "Vault opens after restart");
            var generated = KeyGeneration.Generate(vault, "generated-ed25519", "ed25519");
            Assert(generated.PublicKey.StartsWith("ssh-ed25519 ") && generated.Fingerprint.StartsWith("SHA256:"), "Ed25519 generation imports valid OpenSSH key into vault");
            var rsaGenerated = KeyGeneration.Generate(vault, "generated-rsa", "rsa3072");
            Assert(rsaGenerated.PublicKey.StartsWith("ssh-rsa "), "RSA 3072 generation imports valid key into vault");
            CertificateChecks.Run(vault, generated.Id, info.Id, directory, Assert);
            vault.Import("second", key); var secondNonce = JsonNode.Parse(File.ReadAllText(vaultPath))!["Nonce"]!.ToString();
            Assert(firstNonce != secondNonce, "Unique AES-GCM nonce on rewrite");
            vault.Lock(); var envelope = JsonNode.Parse(File.ReadAllText(vaultPath))!; var ciphertext = Convert.FromBase64String(envelope["Ciphertext"]!.ToString()); ciphertext[0] ^= 1; envelope["Ciphertext"] = Convert.ToBase64String(ciphertext); File.WriteAllText(vaultPath, envelope.ToJsonString());
            Reject<CryptographicException>(() => vault.Unlock(password), "Tampered vault rejected");
            Assert(!vault.IsUnlocked, "Vault remains locked after failed authentication");
            CryptographicOperations.ZeroMemory(key);
            await UpdateChecks.Run(directory, Assert);
            if (args.Length == 2 && args[0] == "--signature")
            {
                var certificate = Path.Combine(Directory.GetCurrentDirectory(), "src/UnscRelay.App/Assets/Publisher.cer");
                InstallerSignature.Verify(args[1], certificate);
                Assert(true, "Authenticode accepts pinned personal publisher");
                var altered = Path.Combine(directory, "altered.exe"); var image = File.ReadAllBytes(args[1]); image[0x500] ^= 1; File.WriteAllBytes(altered, image);
                Reject<CryptographicException>(() => InstallerSignature.Verify(altered, certificate), "Authenticode rejects altered executable");
                var unsigned = Path.Combine(directory, "unsigned.exe"); File.WriteAllText(unsigned, "Not an executable");
                Reject<CryptographicException>(() => InstallerSignature.Verify(unsigned, certificate), "Authenticode rejects unsigned executable");
            }
            if (args.Length == 1 && int.TryParse(args[0], out var port))
            {
                var profile = p with { Port = port }; var output = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var ssh = new SshSession(profile, "relay-test-password", null, "", hosts, _ => true, _ => "relay-test-password");
                ssh.Output += bytes => { if (Encoding.UTF8.GetString(bytes).Contains("relay-integration")) output.TrySetResult("ok"); };
                await ssh.ConnectAsync(100, 30); Assert(ssh.Connected && ssh.FilesAvailable, "Loopback SSH and SFTP connected");
                ssh.Resize(120, 40); ssh.Write("relay-integration\r");
                Assert(await Task.WhenAny(output.Task, Task.Delay(5000)) == output.Task, "Interactive terminal output and resize");
                var source = Path.Combine(directory, "upload.txt"); File.WriteAllText(source, "Transfer fixture " + Guid.NewGuid());
                await ssh.TransferAsync(source, "/upload.txt", true, false, (_, _) => { }, CancellationToken.None);
                var original = File.ReadAllText(source); File.WriteAllText(source, "Replacement content");
                try { await ssh.TransferAsync(source, "/upload.txt", true, false, (_, _) => { }, CancellationToken.None); throw new Exception("Fixture unexpectedly accepted POSIX rename"); }
                catch (NotSupportedException) { Assert(true, "SFTP refuses unsafe overwrite without server extension"); }
                var target = Path.Combine(directory, "download.txt"); await ssh.TransferAsync(target, "/upload.txt", false, false, (_, _) => { }, CancellationToken.None);
                Assert(original == File.ReadAllText(target), "SFTP upload/download and preservation of original");
                Assert((await ssh.ListAsync("/")).Length > 0, "SFTP directory listing");
                File.WriteAllBytes(source, RandomNumberGenerator.GetBytes(256 * 1024));
                using var cancellation = new CancellationTokenSource();
                try { await ssh.TransferAsync(source, "/cancelled.txt", true, false, (_, _) => cancellation.Cancel(), cancellation.Token); throw new Exception("Transfer was not cancelled"); }
                catch (OperationCanceledException) { Assert(true, "SFTP transfer cancellation observed"); }
                var listing = System.Text.Json.JsonSerializer.Serialize(await ssh.ListAsync("/"));
                Assert(!listing.Contains("cancelled.txt") && !listing.Contains(".partial"), "Cancelled transfer removes only its temporary file");
                using var quantum = new SshSession(profile with { RequirePostQuantumKex = true }, "relay-test-password", null, "", hosts, _ => true, _ => "");
                try { await quantum.ConnectAsync(80, 24); throw new Exception("Post-quantum policy silently downgraded"); }
                catch (Renci.SshNet.Common.SshConnectionException ex) when (ex.DisconnectReason == Renci.SshNet.Messages.Transport.DisconnectReason.KeyExchangeFailed)
                { Assert(true, "Post-quantum-only profile refuses server without compatible KEX"); }
                var rsaLogin = Encoding.UTF8.GetBytes(rsa.ExportRSAPrivateKeyPem());
                try
                {
                    using var rsaSession = new SshSession(profile, "", rsaLogin, "", hosts, _ => true, _ => "");
                    await rsaSession.ConnectAsync(80, 24);
                    Assert(rsaSession.Connected && rsaSession.FilesAvailable, "RSA SHA-2 authenticates SSH and SFTP without password fallback");
                }
                finally { CryptographicOperations.ZeroMemory(rsaLogin); }
            }
            Console.WriteLine($"{passed} checks passed."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.Delete(directory, true); }
    }
}
