namespace UnscRelay.Core;
public sealed record PrivacyDataGroup(string Category,int Files,long Bytes,int SkippedLinks,bool Partial);
public sealed record PrivacyCleanupSummary(string Category,int Files,long Bytes);
public sealed record PrivacyCleanupResult(bool Removed,int Files,long Bytes,bool Partial=false);
public static class PrivacyData
{
 static readonly Dictionary<string,string[]> Groups=new(){["archive"]=["profiles.json","keys.vault","hostkeys.json","ArchiveCopies","SyncConflicts"],["recordings"]=["Recordings"],["editor"]=["Editor"],["temporary"]=["AssistantScratch","DragExports"]};
 static List<string> Files(string root,string category,bool strict,out int skipped,out bool partial){
  if(!Groups.TryGetValue(category,out var roots))throw new ArgumentException("Categoria dati non valida.");var files=new List<string>();var links=0;var incomplete=false;var examined=0;
  void Walk(string relative,int depth){if(++examined>10000||depth>16){if(strict)throw new IOException("Troppi dati per una pulizia unica. Nessun file è stato rimosso.");incomplete=true;return;}var path=FolderSync.LocalChild(root,relative);FolderSync.CheckLocalParents(Path.GetDirectoryName(path)!);FileAttributes attributes;try{attributes=File.GetAttributes(path);}catch(FileNotFoundException){return;}catch(DirectoryNotFoundException){return;}if(attributes.HasFlag(FileAttributes.ReparsePoint)){links++;if(strict)throw new IOException("La pulizia rifiuta i collegamenti. Nessun file è stato rimosso.");return;}if(attributes.HasFlag(FileAttributes.Directory)){foreach(var child in new DirectoryInfo(path).EnumerateFileSystemInfos()){if(examined>10000)break;Walk(relative+"/"+child.Name,depth+1);}}else{if(files.Count>=2000){if(strict)throw new IOException("Troppi dati per una pulizia unica. Nessun file è stato rimosso.");incomplete=true;return;}files.Add(relative);}}
  foreach(var relative in roots)Walk(relative,0);skipped=links;partial=incomplete;return files;
 }
 public static PrivacyDataGroup[] Inventory(string directory){var root=LocalDataFiles.Root(directory);return Groups.Keys.Select(category=>{var files=Files(root,category,false,out var skipped,out var partial);long bytes=0;foreach(var file in files){try{using var read=LocalDataFiles.OpenRead(root,file,long.MaxValue);bytes=checked(bytes+read.Length);}catch(Exception error)when(error is IOException or UnauthorizedAccessException or OverflowException){partial=true;}}return new PrivacyDataGroup(category,files.Count,bytes,skipped,partial);}).ToArray();}
 public static PrivacyCleanupResult Cleanup(string directory,string category,Func<PrivacyCleanupSummary,bool> confirm){
  if(category is not("editor" or "temporary"))throw new ArgumentException("Categoria non eliminabile dal centro privacy.");var root=LocalDataFiles.Root(directory);var paths=Files(root,category,true,out _,out _);var opened=new List<ManagedFileDeletion>();long bytes=0;
  try{foreach(var relative in paths){var held=new ManagedFileDeletion(LocalDataFiles.Child(root,relative));opened.Add(held);bytes=checked(bytes+held.Content.Length);}if(opened.Count==0)return new(false,0,0);if(!confirm(new(category,opened.Count,bytes)))return new(false,0,0);var removed=0;long removedBytes=0;foreach(var held in opened){var length=held.Content.Length;try{held.Delete();}catch(IOException){return new(removed>0,removed,removedBytes,true);}removed++;removedBytes+=length;}return new(true,removed,removedBytes);}finally{foreach(var held in opened)held.Dispose();}
 }
}
