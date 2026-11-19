using Renci.SshNet.Common;
namespace UnscRelay.Core;
public sealed record RemoteServerInfo(string Directory,int Owner,int Group,string Permissions,long? TotalBytes,long? AvailableBytes,bool? ReadOnly,DateTimeOffset CheckedAt);
public sealed partial class SshSession
{
 public async Task<RemoteServerInfo> ServerInfoAsync(string path,CancellationToken ct=default){
  if(string.IsNullOrWhiteSpace(path)||path.Length>4096||path.Any(char.IsControl))throw new ArgumentException("Percorso remoto non valido.");using var channel=await FileChannel(ct);using var stop=ct.Register(()=>channel.Dispose());try{var root=await channel.GetAsync(path,ct);if(!root.IsDirectory||root.FullName.Length>4096)throw new IOException("Seleziona una cartella remota.");long? total=null,available=null;bool? readOnly=null;
   try{var capacity=await channel.GetStatusAsync(root.FullName,ct);total=CapacityBytes(capacity.TotalBlocks,capacity.BlockSize);available=CapacityBytes(capacity.AvailableBlocks,capacity.BlockSize);readOnly=capacity.IsReadOnly;}catch(Exception error)when(!ct.IsCancellationRequested&&error is SshException or NotSupportedException){/* Optional statvfs may be unavailable or refused; no command fallback. */}
   ct.ThrowIfCancellationRequested();return new(root.FullName,root.Attributes.UserId,root.Attributes.GroupId,FilePresentation.Permissions(root.Attributes),total,available,readOnly,DateTimeOffset.UtcNow);
  }catch(Exception)when(ct.IsCancellationRequested){throw new OperationCanceledException(ct);}
 }
 public static long? CapacityBytes(ulong blocks,ulong blockSize)=>blockSize==0||blocks>(ulong)long.MaxValue/blockSize?null:(long)(blocks*blockSize);
}
