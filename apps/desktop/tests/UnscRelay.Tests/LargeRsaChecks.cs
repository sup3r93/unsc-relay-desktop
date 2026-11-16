using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using UnscRelay.Core;
static class LargeRsaChecks
{
    public static async Task<int> Run(int port)
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "rsa8192-fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); int count = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
        using var vault = new KeyVault(Path.Combine(dir,"keys.vault")); vault.Unlock("RSA8192 fixture master only!");
        var key = KeyGeneration.Generate(vault, "Large RSA fixture", "rsa8192");
        Check(key.Bits == 8192 && key.Algorithm == "ssh-rsa" && key.PublicKey.StartsWith("ssh-rsa "), "RSA8192 generated and parsed as a real 8192-bit SSH key");
        var bytes = vault.Read(key.Id);
        try
        {
            using var parsed = new PrivateKeyFile(new MemoryStream(bytes)); var data = "RSA8192 signing fixture"u8.ToArray();
            foreach (var name in new[]{"rsa-sha2-256","rsa-sha2-512"})
            {
                var algorithm = parsed.HostKeyAlgorithms.Single(a => a.Name == name); var signature = algorithm.Sign(data);
                Check(algorithm.VerifySignature(data, signature) && !algorithm.VerifySignature("altered"u8.ToArray(), signature), name + " signs and verifies, rejecting modified data");
            }
            var publicPath = Path.Combine(dir,"public.pub"); File.WriteAllText(publicPath,key.PublicKey);
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"OpenSSH","ssh-keygen.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-l","-f",publicPath})start.ArgumentList.Add(arg);using var process=System.Diagnostics.Process.Start(start)!;var output=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();Check(process.ExitCode==0&&output.StartsWith("8192 "),"Installed OpenSSH independently reads the 8192-bit public key");
            var profile = new ConnectionProfile { Name="Large RSA SSH fixture", Host="127.0.0.1", Port=port, Username="test", Authentication="key", KeyId=key.Id };
            using var session = new SshSession(profile,"",bytes,"",new HostKeyStore(Path.Combine(dir,"hosts.json")),_=>true,_=>throw new Exception("Unexpected password fallback"));
            await session.ConnectAsync(80,24);Check(session.Connected&&session.FilesAvailable,"RSA8192 authenticates real SSH and SFTP without password fallback");
            Check((await session.ListAsync(".")).Length>=0,"RSA8192 SFTP channel lists fixture files");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        vault.Lock();vault.Unlock("RSA8192 fixture master only!");Check(vault.List().Single().Bits==8192,"RSA8192 metadata survives encrypted archive reopen");
        Console.WriteLine(count+" RSA8192 checks passed");return 0;
    }
}
