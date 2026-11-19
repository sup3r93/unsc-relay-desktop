using System.Text;
using UnscRelay.Core;

internal static class LocalDataFileChecks
{
    public static int Run()
    {
        var root = Directory.CreateTempSubdirectory("relay-local-data-"); int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS local data: " + name); }
        void Reject(Action action, string name) { try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException) { Check(true, name); return; } throw new Exception("Accepted " + name); }
        try
        {
            foreach (var invalid in new[] { "", " ", "relative", Path.Combine(root.FullName, "..", "outside") })
                Reject(() => LocalDataFiles.ApplicationRoot(invalid), "unresolved, relative or traversing application root rejected");
            Check(LocalDataFiles.ApplicationRoot(root.FullName) == Path.Combine(root.FullName, "UNSC Relay"), "valid Windows folder keeps the existing data location");
            Reject(() => LocalDataFiles.Child(root.FullName, "../outside"), "metadata cannot escape its root");
            var data = Path.Combine(root.FullName, "data"); Directory.CreateDirectory(data);
            var outside = Path.Combine(root.FullName, "outside"); Directory.CreateDirectory(outside);
            var sentinel = Path.Combine(outside, "sentinel"); File.WriteAllText(sentinel, "outside synthetic data");
            foreach (var name in new[] { "github-token.dpapi", "update-policy.json", "accounts.dpapi" })
            {
                var link = Path.Combine(data, name); File.CreateSymbolicLink(link, sentinel);
                Reject(() => LocalDataFiles.Read(data, name, 32768), "linked metadata read rejected: " + name);
                Reject(() => LocalDataFiles.Write(data, name, [1]), "linked metadata write rejected: " + name);
                if (name == "github-token.dpapi") { Reject(() => new UpdateTokenStore(data).Read(), "token link rejected before DPAPI"); Reject(() => new UpdateTokenStore(data).Delete(), "token delete refuses links"); }
                if (name == "accounts.dpapi") Reject(() => new AccountWorkspaces(data).Load(), "account index link rejected before DPAPI");
                File.Delete(link);
            }
            var directoryLink = Path.Combine(root.FullName, "alias"); Directory.CreateSymbolicLink(directoryLink, outside);
            Reject(() => LocalDataFiles.Read(directoryLink, "sentinel", 32768), "linked ancestor rejected");
            LocalDataFiles.Write(data, "update-policy.json", Encoding.UTF8.GetBytes("synthetic policy"));
            Check(Encoding.UTF8.GetString(LocalDataFiles.Read(data, "update-policy.json", 32)) == "synthetic policy", "ordinary metadata writes and reads round trip");
            Reject(() => LocalDataFiles.Read(data, "update-policy.json", 4), "size bound enforced on opened handle");
            using (var held = LocalDataFiles.OpenRead(data, "update-policy.json", 32))
            {
                if (OperatingSystem.IsWindows()) Reject(() => File.WriteAllText(Path.Combine(data, "update-policy.json"), "tampered"), "opened metadata cannot change during verification");
            }
            Check(File.ReadAllText(sentinel) == "outside synthetic data" && Directory.GetFileSystemEntries(outside).Length == 1, "rejected links never modify external data");
            var profiles=new ProfileStore(data);profiles.Save(new AppData());Check(profiles.Load().Profiles.Count==0,"profile store uses the bounded local metadata path");var profilePath=Path.Combine(data,"profiles.json");File.Delete(profilePath);File.CreateSymbolicLink(profilePath,sentinel);Reject(()=>profiles.Load(),"profile load refuses redirected metadata");Reject(()=>profiles.Save(new AppData()),"profile save cannot replace a redirected metadata target");Reject(()=>profiles.Import(profilePath,new AppData()),"configuration import refuses redirected external input");File.Delete(profilePath);using(var oversized=new FileStream(profilePath,FileMode.CreateNew)){oversized.SetLength(32L*1024*1024+1);}Reject(()=>profiles.Load(),"oversized saved configuration rejected before reading or parsing");Reject(()=>profiles.Import(profilePath,new AppData()),"oversized imported configuration rejected before allocation");Check(File.ReadAllText(sentinel)=="outside synthetic data","profile operations preserve redirected outside file");
            Console.WriteLine("Local data checks: " + count); return 0;
        }
        finally { root.Delete(true); }
    }
}
