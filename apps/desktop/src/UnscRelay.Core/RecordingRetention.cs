namespace UnscRelay.Core;
public sealed record RetentionResult(int Deleted, int Failed, DateTime CheckedUtc);
public static class RecordingRetention
{
    public static RetentionResult Apply(string directory, int days, IEnumerable<string> active, DateTime utcNow)
    {
        if(days is <0 or >3650)throw new ArgumentException("Conservazione: da 0 a 3650 giorni.");
        if(days==0||!Directory.Exists(directory))return new(0,0,utcNow);
        FolderSync.CheckLocalParents(directory);var excluded=active.ToHashSet(StringComparer.OrdinalIgnoreCase);int deleted=0,failed=0;Span<byte> magic=stackalloc byte[8];
        foreach(var file in new DirectoryInfo(directory).EnumerateFiles("*.urec").Take(10000))
        {
            if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name),"N",out _)||file.Attributes.HasFlag(FileAttributes.ReparsePoint)||excluded.Contains(file.FullName)||file.LastWriteTimeUtc>=utcNow.AddDays(-days))continue;
            try
            {
                using(var stream=new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.Read)){if(stream.Read(magic)!=8||!magic.SequenceEqual("UREC0001"u8))continue;}
                File.Delete(file.FullName);deleted++;
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){failed++;}
        }
        return new(deleted,failed,utcNow);
    }
}
