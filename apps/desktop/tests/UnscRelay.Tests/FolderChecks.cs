using UnscRelay.Core;
internal static class FolderChecks
{
    public static void Run(string directory, Action<bool, string> assert)
    {
        var data = new AppData(); FolderCatalog.Add(data, "Università/Laboratorio");
        var store = new ProfileStore(Path.Combine(directory, "folders")); store.Save(data); data = store.Load();
        assert(data.Folders.Contains("Università/Laboratorio") && data.Profiles.Count == 0, "Empty nested folder survives save/load");
        data.Profiles.Add(new ConnectionProfile { Host = "localhost", Username = "test", Folder = "Università/Laboratorio" });
        FolderCatalog.Rename(data, "Università", "Studio");
        assert(data.Folders.Contains("Studio/Laboratorio") && data.Profiles.Single().Folder == "Studio/Laboratorio", "Folder rename moves descendant connections");
        try { FolderCatalog.RemoveEmpty(data, "Studio"); throw new Exception("Nonempty folder deleted"); }
        catch (InvalidOperationException) { assert(true, "Nonempty folder cannot be deleted"); }
        try { FolderCatalog.Rename(data, "Studio", "Studio/Ciclo"); throw new Exception("Cyclic folder accepted"); }
        catch (ArgumentException) { assert(true, "Folder cannot become child of itself"); }
    }
}
