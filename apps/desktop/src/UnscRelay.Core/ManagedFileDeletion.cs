using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace UnscRelay.Core;
/// <summary>Deletes only the already verified open object, never a re-resolved path.</summary>
internal sealed class ManagedFileDeletion:IDisposable
{
 readonly SafeFileHandle handle;FileStream? stream;readonly bool link;readonly bool directoryObjectKind;bool marked;
 internal Stream Content=>link||directoryObjectKind?throw new IOException("Il contenuto dei collegamenti non viene aperto."):stream??=new FileStream(handle,FileAccess.Read);
 internal ManagedFileDeletion(string path,bool linkObject=false,bool directoryObject=false)
 {
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("La rimozione verificata dei file locali richiede Windows.");
  var directory=LocalDataFiles.Root(Path.GetDirectoryName(Path.GetFullPath(path))!);var full=linkObject?FolderSync.LocalChild(directory,Path.GetFileName(path)):LocalDataFiles.Child(directory,Path.GetFileName(path));link=linkObject;directoryObjectKind=directoryObject;
  handle=CreateFile(full,0x80000000|0x00010000,0,IntPtr.Zero,3,0x00200000|0x02000000|0x00000080,IntPtr.Zero);
  try{if(handle.IsInvalid)throw new IOException("Non riesco ad aprire in modo esclusivo il file da rimuovere.",new Win32Exception(Marshal.GetLastWin32Error()));LocalDataFiles.RequireOpenedObject(handle,full,linkObject,directoryObject);FolderSync.CheckLocalParents(directory);if(!linkObject)LocalDataFiles.Child(directory,Path.GetFileName(full));}
  catch{handle.Dispose();throw;}
 }
 internal void Delete(){if(marked)return;byte remove=1;if(!SetFileInformationByHandle(handle,4,ref remove,1))throw new IOException("Il file verificato non è stato rimosso.",new Win32Exception(Marshal.GetLastWin32Error()));marked=true;}
 public void Dispose(){if(stream!=null)stream.Dispose();else handle.Dispose();}
 [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool SetFileInformationByHandle(SafeFileHandle handle,int informationClass,ref byte information,uint size);
}
