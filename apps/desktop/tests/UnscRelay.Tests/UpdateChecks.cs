using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnscRelay.Core;

internal sealed class UpdateFixture : HttpMessageHandler
{
    public byte[] Bytes { get; set; } = Encoding.UTF8.GetBytes("Installer fixture, never executed");
    public bool Mismatch { get; set; }
    public bool MissingDigest { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        if (request.RequestUri!.Host != "api.github.com") throw new Exception("Unexpected update host");
        if (request.Headers.Authorization?.Parameter != "test-token") throw new Exception("Missing private repository authentication");
        if (request.RequestUri.AbsolutePath.EndsWith("/latest"))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant();
            var json = System.Text.Json.JsonSerializer.Serialize(new { tag_name = "v0.2.15", body = "Test release", assets = new[] { new { id = 123, name = "UNSC-Relay-Setup-0.2.15.exe", digest = MissingDigest ? null : "sha256:" + hash } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Mismatch ? [1, 2, 3] : Bytes) });
    }
}
internal static class UpdateChecks
{
    public static async Task Run(string directory, Action<bool, string> assert)
    {
        using var fixture = new UpdateFixture(); var updates = new GitHubUpdates(fixture);
        var release = await updates.CheckAsync("test-token", new Version(0, 1, 0), CancellationToken.None);
        assert(release?.Version == "v0.2.15", "Private GitHub release version detected");
        assert(await updates.CheckAsync("test-token", new Version(0, 2, 15), CancellationToken.None) == null, "Same version does not trigger update");
        var reports=new List<UpdateDownloadProgress>();
        var path = await updates.DownloadAsync(release!, "test-token", directory, CancellationToken.None,reports.Add);
        assert(reports.First().Stage=="download"&&reports.Last().Stage=="verify"&&reports.Last().ReceivedBytes==fixture.Bytes.Length&&reports.Last().TotalBytes==fixture.Bytes.Length,"Download progress reports exact byte count and separate verification stage");
        assert(new UserSettings().AppMusicEnabled,"App music enabled by default");
        var explicitOff=System.Text.Json.JsonSerializer.Deserialize<UserSettings>("{\"appMusicEnabled\":false}",ProfileStore.Json)!;
        assert(!explicitOff.AppMusicEnabled,"Saved disabled music preference is preserved");
        assert(File.ReadAllBytes(path).SequenceEqual(fixture.Bytes), "Installer digest verified before launch"); File.Delete(path);
        var editorDirectory=Path.Combine(directory,"Editor Fixture");Directory.CreateDirectory(editorDirectory);var npp=Path.Combine(editorDirectory,"notepad++.exe");File.WriteAllBytes(npp,[77,90]);
        assert(EditorDiscovery.SelectNotepadPlusPlus(["notepad++.exe",Path.Combine(editorDirectory,"absent.exe"),npp])==npp,"Editor discovery accepts an existing absolute Notepad++ path");
        assert(EditorDiscovery.SelectNotepadPlusPlus([npp+" -malicious-argument"])=="","Editor discovery rejects command strings");
        assert(EditorDiscovery.ApplyAutomatic(new UserSettings(),npp).ExternalEditor==npp,"Default editor automatically selects detected Notepad++");
        var manual=new UserSettings(ExternalEditor:"C:\\Fixture\\manual.exe");assert(EditorDiscovery.ApplyAutomatic(manual,npp).ExternalEditor==manual.ExternalEditor,"Existing manual editor is preserved");
        assert(EditorDiscovery.ApplyAutomatic(new UserSettings(EditorPreference:"notepad"),npp).ExternalEditor=="","Explicit Windows Notepad selection is preserved");
        assert(EditorDiscovery.ApplyAutomatic(new UserSettings(ExternalEditor:npp,EditorPreference:"auto"),"").ExternalEditor=="","Missing automatic editor falls back without stale executable");
        foreach(var prior in new[]{new Version(0,1,10),new Version(0,1,21),new Version(0,2,3)})
            assert((await updates.CheckAsync("test-token",prior,CancellationToken.None))?.Version=="v0.2.15","Older installations choose a full latest release without intermediate patches");
        assert(DesktopAccountNavigation.IsIdentityUrl(CloudAccountClient.Authority+"/protocol/openid-connect/auth?client_id=unsc-desktop"),"Only dedicated HTTPS identity navigation accepted");
        foreach(var denied in new[]{"http://unsc.responda.eu/identity/realms/unsc/","https://unsc.responda.eu.evil.invalid/identity/realms/unsc/","https://unsc.responda.eu:444/identity/realms/unsc/","https://unsc.responda.eu/identity/admin/","https://unsc.responda.eu/identity/realms/unsc/../master/"})
            assert(!DesktopAccountNavigation.IsIdentityUrl(denied),"Unsafe identity navigation rejected");
        assert(DesktopAccountNavigation.IsCallback(CloudAccountClient.Callback+"?code=fixture&state=fixture")&&!DesktopAccountNavigation.IsCallback("http://localhost:48187/relay-callback/?code=fixture")&&!DesktopAccountNavigation.IsCallback(CloudAccountClient.Callback+"#fragment"),"Exact callback required before OIDC state validation");
        var backupData=Path.Combine(directory,"backup-fixture");Directory.CreateDirectory(backupData);File.WriteAllText(Path.Combine(backupData,"account-sync.dpapi"),"encrypted-fixture");File.WriteAllText(Path.Combine(backupData,"private-export.key"),"excluded-fixture");
        var backup=UpdateBackup.Create(backupData,"0.1.10");assert(File.ReadAllText(Path.Combine(backup,"account-sync.dpapi"))=="encrypted-fixture"&&!File.Exists(Path.Combine(backup,"private-export.key")),"Update recovery includes encrypted account state and excludes key exports");
        fixture.Mismatch = true;
        try { await updates.DownloadAsync(release!, "test-token", directory, CancellationToken.None); throw new Exception("Digest mismatch accepted"); }
        catch (CryptographicException) { assert(!Directory.EnumerateFiles(directory, "*.exe").Any(), "Corrupt installer rejected and removed"); }
        fixture.MissingDigest = true;
        try { await updates.CheckAsync("test-token", new Version(0, 1, 0), CancellationToken.None); throw new Exception("Missing digest accepted"); }
        catch (InvalidDataException) { assert(true, "Release without SHA-256 rejected"); }
    }
}
