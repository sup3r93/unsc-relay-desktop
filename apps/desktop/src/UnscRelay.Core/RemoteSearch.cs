using Renci.SshNet.Common;
namespace UnscRelay.Core;
public sealed record RemoteSearchQuery(string Root,string Name="",string Extension="",long? MinimumBytes=null,long? MaximumBytes=null,DateTimeOffset? ModifiedAfter=null,DateTimeOffset? ModifiedBefore=null,int MaximumDepth=8,int MaximumResults=500,int MaximumEntries=10000)
{
 public void Validate(){if(string.IsNullOrWhiteSpace(Root)||Root.Length>4096||Root.Any(char.IsControl)||Name is null||Name.Length>160||Name.Any(char.IsControl)||Extension is null||Extension.Length>30||Extension.Any(value=>char.IsControl(value)||value is '/' or '\\')||MinimumBytes<0||MaximumBytes<0||MinimumBytes>MaximumBytes||ModifiedAfter>ModifiedBefore||MaximumDepth is <0 or >16||MaximumResults is <1 or >1000||MaximumEntries is <1 or >20000)throw new ArgumentException("Filtri della ricerca remota non validi.");}
 public bool Matches(string name,long size,DateTime modified){var extension=Extension.Trim().TrimStart('.');return (Name.Length==0||name.Contains(Name,StringComparison.OrdinalIgnoreCase))&&(extension.Length==0||name.EndsWith("."+extension,StringComparison.OrdinalIgnoreCase))&&(!MinimumBytes.HasValue||size>=MinimumBytes)&&(!MaximumBytes.HasValue||size<=MaximumBytes)&&(!ModifiedAfter.HasValue||modified>=ModifiedAfter.Value.UtcDateTime)&&(!ModifiedBefore.HasValue||modified<=ModifiedBefore.Value.UtcDateTime);}
}
public sealed record RemoteSearchHit(string Name,string Path,long Size,DateTime Modified);
public sealed record RemoteSearchResult(string Root,List<RemoteSearchHit> Files,int Examined,int SkippedLinks,int Unreadable,bool Truncated,DateTimeOffset CheckedAt);
public sealed partial class SshSession
{
 public async Task<RemoteSearchResult> SearchFilesAsync(RemoteSearchQuery query,CancellationToken ct=default){
  query.Validate();using var channel=await FileChannel(ct);using var stop=ct.Register(()=>channel.Dispose());var root=await channel.GetAsync(query.Root,ct);if(root.FullName.Length>4096||!root.IsDirectory)throw new IOException("Seleziona una cartella per la ricerca remota.");var prefix=root.FullName.TrimEnd('/')+"/";var pending=new Queue<(string Path,int Depth)>();pending.Enqueue((root.FullName,0));var seen=new HashSet<string>(StringComparer.Ordinal);var files=new List<RemoteSearchHit>();var examined=0;var skipped=0;var unreadable=0;var truncated=false;
  try{while(pending.TryDequeue(out var next)){
   ct.ThrowIfCancellationRequested();if(!seen.Add(next.Path))continue;
   try{var current=await channel.GetAsync(next.Path,ct);if(!current.IsDirectory||current.IsSymbolicLink||current.FullName!=root.FullName&&!current.FullName.StartsWith(prefix,StringComparison.Ordinal)){skipped++;continue;}
    await foreach(var file in channel.ListDirectoryAsync(current.FullName,ct)){
     ct.ThrowIfCancellationRequested();if(file.Name is "." or "..")continue;if(++examined>query.MaximumEntries){truncated=true;break;}if(file.FullName.Length>4096||file.Name.Length==0||file.Name.Length>4096||file.Name.Any(value=>char.IsControl(value)||value=='/')||!file.FullName.StartsWith(prefix,StringComparison.Ordinal)){skipped++;continue;}if(file.IsSymbolicLink){skipped++;continue;}if(file.IsDirectory){if(next.Depth<query.MaximumDepth)pending.Enqueue((file.FullName,next.Depth+1));else truncated=true;continue;}if(!file.IsRegularFile)continue;if(query.Matches(file.Name,file.Length,file.LastWriteTimeUtc)){files.Add(new(file.Name,file.FullName,file.Length,file.LastWriteTimeUtc));if(files.Count>=query.MaximumResults){truncated=true;break;}}
    }
   }catch(SftpPermissionDeniedException){unreadable++;}catch(SftpPathNotFoundException){unreadable++;}
   if(examined>query.MaximumEntries||files.Count>=query.MaximumResults)break;
  }ct.ThrowIfCancellationRequested();return new(root.FullName,files,Math.Min(examined,query.MaximumEntries),skipped,unreadable,truncated,DateTimeOffset.UtcNow);
  }catch(Exception)when(ct.IsCancellationRequested){throw new OperationCanceledException(ct);}
 }
}
