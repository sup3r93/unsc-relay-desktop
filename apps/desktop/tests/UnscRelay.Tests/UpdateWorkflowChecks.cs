using System.Text;
using System.Text.Json;
using UnscRelay.Core;

internal static class UpdateWorkflowChecks
{
    public static async Task<int> Run()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "update-workflow-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
        void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception(name); }
        var tokenStore = new UpdateTokenStore(directory); const string token = "github_pat_fixture_not_a_real_credential_12345";
        Check(!tokenStore.Exists && tokenStore.Read() == "", "No token is required for public repositories");
        tokenStore.Save(token); var tokenPath = Path.Combine(directory, "github-token.dpapi");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(tokenPath)).Contains(token), "GitHub token encrypted at rest");
        Check(new UpdateTokenStore(directory).Read() == token, "Token survives reopening with the same Windows account");
        var savedToken = File.ReadAllBytes(tokenPath); Reject(() => tokenStore.Save("bad token"), "Whitespace token rejected without replacing saved token");
        Check(savedToken.SequenceEqual(File.ReadAllBytes(tokenPath)), "Invalid token leaves encrypted credential unchanged");
        var store = new ProfileStore(directory); var data = new AppData { Profiles = [new() { Name = "Fixture", Host = "localhost", Username = "fixture" }] }; store.Save(data);
        File.WriteAllText(Path.Combine(directory, "keys.vault"), "Encrypted fixture, not a real private key"); File.WriteAllText(Path.Combine(directory, "hostkeys.json"), "[]");
        Directory.CreateDirectory(Path.Combine(directory, "WebView")); File.WriteAllText(Path.Combine(directory, "WebView", "cache"), "cache fixture");
        File.WriteAllText(Path.Combine(directory, "transfers.json"), "[]");
        File.WriteAllText(Path.Combine(directory,"account-sync.dpapi"),"synthetic protected account state");
        File.WriteAllText(Path.Combine(directory,"account-unlock.dpapi"),"synthetic protected device state");
        var backup = UpdateBackup.Create(directory, "0.1.3");
        Check(UpdateBackup.DataFiles.All(name => File.ReadAllBytes(Path.Combine(directory, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(backup, name)))), "Pre-update backup preserves all data and encrypted credential byte for byte");
        Check(!Directory.Exists(Path.Combine(backup, "WebView")) && File.Exists(Path.Combine(backup, "manifest.json")), "Update backup uses explicit data allowlist and manifest");
        Check(!JsonSerializer.Serialize(data, ProfileStore.Json).Contains(token), "Profile export never includes the update credential");
        tokenStore.Delete(); Check(!tokenStore.Exists && store.Load().Profiles.Count == 1 && File.Exists(Path.Combine(directory, "keys.vault")), "Removing token preserves profiles and vault");
        File.WriteAllBytes(tokenPath, [1, 2, 3]);
        try { tokenStore.Read(); throw new Exception("Corrupt credential accepted"); } catch (InvalidOperationException ex) { Check(!ex.Message.Contains(token), "Corrupt credential fails with a non-secret user message"); }
        await UpdateChecks.Run(directory, Check);
        foreach (var rule in new[] { new TunnelRule("a", "local", 2222, "localhost", 22), new("b", "remote", 8080, "localhost", 80, true), new("c", "socks", 1080, "", 0) }) rule.Validate();
        Check(true, "Local remote and SOCKS tunnel rules validate");
        Reject(() => new TunnelRule("a", "local", 1, "localhost", 0).Validate(), "Zero destination port rejected for fixed tunnels");
        Reject(() => new TunnelRule("a", "bad", 1, "localhost", 22).Validate(), "Unknown tunnel kind rejected");
        Reject(() => new TunnelRule("a", "local", 65536, "localhost", 22).Validate(), "Invalid listening port rejected");
        var snippet = new Snippet("existing", "Fixture", "echo {{value}}", "Test");
        var imported = SnippetCatalog.Import(JsonSerializer.SerializeToUtf8Bytes(new[] { snippet }, ProfileStore.Json));
        Check(imported.Single().Id != snippet.Id && imported.Single().Text == snippet.Text, "Snippet import preserves content and assigns independent identities");
        Reject(() => SnippetCatalog.Import(JsonSerializer.SerializeToUtf8Bytes(new[] { snippet with { Text = "" } }, ProfileStore.Json)), "Invalid snippet library rejected before merging");
        Console.WriteLine($"{count} update/workflow checks passed"); return 0;
    }
}
