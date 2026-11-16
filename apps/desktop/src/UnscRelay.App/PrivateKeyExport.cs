using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
namespace UnscRelay.App;
static class PrivateKeyExport
{
    public static void Write(string path,byte[] bytes)
    {
        path=Path.GetFullPath(path);var parent=Path.GetDirectoryName(path)!;
        for(var dir=new DirectoryInfo(parent);dir!=null;dir=dir.Parent)if(dir.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("Scegli una cartella locale senza collegamenti.");
        if(File.Exists(path)&&File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Il file di destinazione è un collegamento.");
        var user=WindowsIdentity.GetCurrent().User??throw new IOException("Utente Windows non disponibile.");
        var acl=new FileSecurity();acl.SetOwner(user);acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new FileSystemAccessRule(user,FileSystemRights.FullControl,AccessControlType.Allow));
        var temporary=Path.Combine(parent,".relay-key-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var stream=FileSystemAclExtensions.Create(new FileInfo(temporary),FileMode.CreateNew,FileSystemRights.FullControl,FileShare.None,4096,FileOptions.WriteThrough,acl)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temporary,path,true);
        }catch(PlatformNotSupportedException ex){throw new IOException("La cartella non supporta i permessi richiesti. Scegli un disco locale NTFS.",ex);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
