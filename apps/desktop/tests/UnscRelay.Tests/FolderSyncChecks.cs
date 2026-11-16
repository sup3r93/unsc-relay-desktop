using UnscRelay.Core;
internal static class FolderSyncChecks
{
    public static int Run()
    {
        int count = 0; void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var time = DateTime.UtcNow;
        SyncEntry E(string name, string hash = "A", bool folder = false) => new(name, folder, 4, time, hash);
        var rows = FolderSync.Compare([E("same"), E("changed", "A"), E("local"), E("empty", folder:true)], [E("same"), E("changed", "B"), E("remote")]);
        Check(rows.Single(r => r.Relative == "same").Status == "Identico", "Equal content ignores timestamps");
        Check(rows.Single(r => r.Relative == "changed").Transferable, "Same-size different hashes are detected");
        Check(rows.Single(r => r.Relative == "local").Status == "Solo locale", "Local-only file has upload direction");
        Check(rows.Single(r => r.Relative == "remote").Status == "Solo remoto", "Remote-only file has download direction");
        Check(rows.Single(r => r.Relative == "empty").Transferable, "Empty missing directory is transferable");
        Check(FolderSync.Compare([E("x", folder:true), E("x/child")], [E("x")]).All(r => !r.Transferable), "File-directory collision blocks entire subtree");
        Check(FolderSync.Compare([E("Path/child"), E("Path", folder:true)], [E("path", folder:true)]).All(r => !r.Transferable), "Windows case collisions block descendants");
        Check(FolderSync.Compare([E("link/child")], [E("link") with {Link=true}]).All(r => !r.Transferable), "Symbolic links block descendants in either direction");
        var root = Path.Combine(Path.GetTempPath(), "relay-sync-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            foreach(var name in new[]{"../escape", "/absolute", "a//b", "a/../b", "CON.txt", "x.", "x ", "x:stream", "x\\y"})
            { bool rejected=false; try { FolderSync.LocalChild(root,name); } catch(IOException) { rejected=true; } Check(rejected,"Unsafe relative name refused: "+name); }
            Directory.CreateDirectory(Path.Combine(root,"empty"));File.WriteAllText(Path.Combine(root,"text"),"fixture");
            var entries=FolderSync.ScanLocal(root,out var skipped);Check(entries.Count==2&&skipped==0,"Recursive scan retains empty folders");
            Check(FolderSync.HashLocal(Path.Combine(root,"text"))==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("fixture"))),"Local SHA256 reads actual bytes");
        }
        finally {Directory.Delete(root,true);}
        Console.WriteLine($"{count} folder checks passed");return 0;
    }
}
