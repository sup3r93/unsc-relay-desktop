using System.Security.Cryptography;
using UnscRelay.Core;

static class SecurityReviewChecks
{
    public static int Run()
    {
        var root = Directory.CreateTempSubdirectory("relay-security-review-");
        int count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        void Reject(Action action, string name) { try { action(); } catch (IOException) { Check(true, name); return; } throw new Exception("Accepted " + name); }
        try
        {
            foreach (var name in new[] { "CON", "NUL.txt", "--user-data-dir=evil", "-script", "..", "a:stream", "x.", "x ", "a\0b", new string('a', 200) })
            {
                var copy = ExternalEditorFile.CopyName("/remote/" + name);
                Check(copy.StartsWith("relay-", StringComparison.Ordinal) && copy.Length <= 186 && !copy.Contains(':') && !copy.Any(char.IsControl) && !copy.EndsWith('.') && !copy.EndsWith(' '), "Remote filename becomes an ordinary bounded Windows filename: " + name.Replace('\0', '?'));
                Check(Path.GetFullPath(Path.Combine(root.FullName, copy)).StartsWith(root.FullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Remote copy stays inside editor directory");
            }
            Check(ExternalEditorFile.CopyName("/remote/settings.json") == "relay-settings.json", "Editor copy retains the file extension");
            var exe = Path.Combine(root.FullName, "fixture editor.exe"); File.WriteAllBytes(exe, [77, 90]);
            Reject(() => ExternalEditorFile.Launch("fixture editor.exe", "file.txt"), "Relative executable cannot select a different editor through PATH");
            Reject(() => ExternalEditorFile.Launch(exe + " -argument", "file.txt"), "Executable plus arguments is rejected");
            var file = Path.Combine(root.FullName, "--option & literal.txt");
            var start = ExternalEditorFile.Launch(exe, file);
            Check(!start.UseShellExecute && start.FileName == exe && start.ArgumentList.Count == 1 && start.ArgumentList[0] == file && start.Arguments.Length == 0, "Editor receives one absolute literal file argument without shell parsing");
            var data = Path.Combine(root.FullName, "data"); Directory.CreateDirectory(data);
            var bytes = RandomNumberGenerator.GetBytes(128); File.WriteAllBytes(Path.Combine(data, "keys.vault"), bytes);
            File.WriteAllText(Path.Combine(data, "private-export.key"), "excluded synthetic secret");
            var backup = UpdateBackup.Create(data, "security-fixture");
            Check(File.ReadAllBytes(Path.Combine(backup, "keys.vault")).SequenceEqual(bytes) && !File.Exists(Path.Combine(backup, "private-export.key")), "Backup preserves allowlisted ciphertext and excludes plaintext exports");
            var fixture = Path.Combine(root.FullName, "links"); Directory.CreateDirectory(fixture);
            var target = Path.Combine(fixture, "target"); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "outside.txt"), "outside synthetic fixture");
            Directory.CreateSymbolicLink(Path.Combine(fixture, "data-linked-root"), target);
            var linkedBackups = Path.Combine(fixture, "data-linked-backups"); Directory.CreateDirectory(linkedBackups);
            Directory.CreateSymbolicLink(Path.Combine(linkedBackups, "UpdateBackups"), target);
            var linkedFile = Path.Combine(fixture, "data-linked-file"); Directory.CreateDirectory(linkedFile);
            File.CreateSymbolicLink(Path.Combine(linkedFile, "keys.vault"), Path.Combine(target, "outside.txt"));
            Reject(() => UpdateBackup.Create(Path.Combine(fixture, "data-linked-root"), "fixture"), "Backup refuses an application root redirected by a directory link");
            Reject(() => UpdateBackup.Create(linkedBackups, "fixture"), "Backup refuses an UpdateBackups directory link");
            Reject(() => UpdateBackup.Create(linkedFile, "fixture"), "Backup refuses an allowlisted file redirected by a link");
            Check(!Directory.Exists(Path.Combine(target, "UpdateBackups")) && Directory.GetFileSystemEntries(target).Length == 1, "Rejected links create no backup outside the selected root");
            Check(File.ReadAllText(Path.Combine(target, "outside.txt")) == "outside synthetic fixture", "Rejected links preserve outside content");
            // NIST SP 800-38A, Appendix F.5.1 and F.2.1: test the actual vendored implementation.
            var aesKey = Convert.FromHexString("2b7e151628aed2a6abf7158809cf4f3c");
            var plain = Convert.FromHexString("6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e51");
            var ctrIv = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff");
            using (var ctr = new Renci.SshNet.Security.Cryptography.Ciphers.AesCipher(aesKey, ctrIv, Renci.SshNet.Security.Cryptography.Ciphers.AesCipherMode.CTR))
                Check(Convert.ToHexString(ctr.Encrypt(plain, 0, plain.Length)).Equals("874d6191b620e3261bef6864990db6ce9806f66b7970fdff8617187bb9fffdff", StringComparison.OrdinalIgnoreCase), "SSH.NET counter mode matches two NIST blocks including counter carry");
            using (var cbc = new Renci.SshNet.Security.Cryptography.Ciphers.AesCipher(aesKey, Convert.FromHexString("000102030405060708090a0b0c0d0e0f"), Renci.SshNet.Security.Cryptography.Ciphers.AesCipherMode.CBC))
                Check(Convert.ToHexString(cbc.Encrypt(plain, 0, plain.Length)).Equals("7649abac8119b246cee98e9b12e9197d5086cb9b507219ee95db113a917678b2", StringComparison.OrdinalIgnoreCase), "SSH.NET chaining mode matches two NIST blocks");
            using (var ctr = new Renci.SshNet.Security.Cryptography.Ciphers.AesCipher(aesKey, ctrIv, Renci.SshNet.Security.Cryptography.Ciphers.AesCipherMode.CTR))
            {
                var repeated = new byte[32]; var encrypted = ctr.Encrypt(repeated, 0, repeated.Length);
                Check(!encrypted.AsSpan(0, 16).SequenceEqual(encrypted.AsSpan(16, 16)), "Identical plaintext blocks do not produce repeated ECB ciphertext in CTR");
            }
            var connection = new Renci.SshNet.ConnectionInfo("fixture.invalid", "fixture", new Renci.SshNet.PasswordAuthenticationMethod("fixture", "fixture"));
            Check(!connection.Encryptions.Keys.Any(name => name.Contains("ecb", StringComparison.OrdinalIgnoreCase)), "SSH transport does not offer an ECB cipher");
            Console.WriteLine($"{count} security review checks passed"); return 0;
        }
        finally { root.Delete(true); }
    }
}
