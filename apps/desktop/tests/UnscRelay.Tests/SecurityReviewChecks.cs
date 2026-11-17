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
            var configTarget = Path.Combine(fixture, "config-target"); Directory.CreateDirectory(configTarget);
            var configBytes = System.Text.Encoding.UTF8.GetBytes("Host personal\n  HostName 192.0.2.80\n");
            File.WriteAllBytes(Path.Combine(configTarget,"config"),configBytes);
            var configAlias = Path.Combine(fixture,"config-alias"); Directory.CreateSymbolicLink(configAlias,configTarget);
            var configProfile = new ConnectionProfile { Name="fixture",Host="192.0.2.81",Username="fixture" };
            Reject(()=>new OpenSshConfigEdit(Path.Combine(configAlias,"config"),[configProfile]),"SSH config preview rejects a linked parent directory");
            var configOwned = Path.Combine(fixture,"config-owned"); Directory.CreateDirectory(configOwned); File.WriteAllBytes(Path.Combine(configOwned,"config"),configBytes);
            var configPreview = new OpenSshConfigEdit(Path.Combine(configOwned,"config"),[configProfile]);
            Directory.Move(configOwned,configOwned+"-original"); Directory.CreateSymbolicLink(configOwned,configTarget);
            Reject(()=>configPreview.Apply(),"SSH config apply rejects a parent replaced by a link after preview");
            Check(File.ReadAllBytes(Path.Combine(configTarget,"config")).SequenceEqual(configBytes)&&Directory.GetFileSystemEntries(configTarget).Length==1,"Rejected config parent links leave external bytes and directory unchanged");
            var accountDirectory=Path.Combine(root.FullName,"account-backup");Directory.CreateDirectory(accountDirectory);
            var accountOwner=new string('D',64);var serverHalf=RandomNumberGenerator.GetBytes(32);
            try
            {
                using var originalVault=new KeyVault(Path.Combine(accountDirectory,"keys.vault"));originalVault.Unlock("Synthetic backup migration password!");var record=originalVault.StorePassword("backup-fixture","recoverable fixture secret");
                var unlockStore=new AccountUnlockStore(accountDirectory);originalVault.MigrateAccountUnlock(unlockStore,accountOwner,Guid.NewGuid(),serverHalf);
                var accountBackup=UpdateBackup.Create(accountDirectory,"backup-fixture");
                Check(File.ReadAllBytes(Path.Combine(accountDirectory,"account-unlock.dpapi")).SequenceEqual(File.ReadAllBytes(Path.Combine(accountBackup,"account-unlock.dpapi"))),"Update backup keeps the protected device capsule byte-for-byte with its vault");
                using var restoredVault=new KeyVault(Path.Combine(accountBackup,"keys.vault"));using var restoredBinding=new AccountUnlockStore(accountBackup).Read(accountOwner)!;restoredVault.UnlockAccount(restoredBinding,serverHalf,accountOwner);
                Check(restoredVault.ReadPassword(record)=="recoverable fixture secret","Migrated vault actually unlocks from the update backup with its matching capsule");
                Check(!File.ReadAllText(Path.Combine(accountBackup,"keys.vault")).Contains("recoverable fixture secret")&&!File.ReadAllText(Path.Combine(accountBackup,"account-unlock.dpapi")).Contains(Convert.ToBase64String(serverHalf)),"Backup does not introduce plaintext secrets or the server unlock half");
            }
            finally{CryptographicOperations.ZeroMemory(serverHalf);}
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
