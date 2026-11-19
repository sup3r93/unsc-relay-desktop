using System.Security.Cryptography;
namespace UnscRelay.Core;
public sealed record RemoteLogChunk(long Offset,string Data,string PrefixHash,bool Reset,long Size,DateTime Modified);
public sealed partial class SshSession
{
 public async Task<RemoteLogChunk> ReadLogAsync(string path,long? offset,string prefixHash,CancellationToken cancellation=default)
 {
  if(string.IsNullOrWhiteSpace(path)||path.Length>4096||path.Any(char.IsControl)||offset<0||prefixHash.Length>0&&(prefixHash.Length!=64||!prefixHash.All(char.IsAsciiHexDigit)))throw new ArgumentException("Percorso o posizione del log non validi.");
  await _files.WaitAsync(cancellation);
  try{return await Task.Run(()=>{
   cancellation.ThrowIfCancellationRequested();var attributes=_sftp.GetAttributes(path);if(!attributes.IsRegularFile)throw new IOException("Seleziona un file regolare per il log.");
   using var stream=_sftp.OpenRead(path);var prefix=new byte[(int)Math.Min(512,attributes.Size)];var prefixRead=0;while(prefixRead<prefix.Length){cancellation.ThrowIfCancellationRequested();var n=stream.Read(prefix,prefixRead,prefix.Length-prefixRead);if(n==0)break;prefixRead+=n;}var fingerprint=Convert.ToHexString(SHA256.HashData(prefix.AsSpan(0,prefixRead)));
   var reset=offset.HasValue&&(offset.Value>attributes.Size||offset.Value>=512&&attributes.Size>=512&&prefixHash.Length>0&&!fingerprint.Equals(prefixHash,StringComparison.OrdinalIgnoreCase));var start=offset==null||reset?Math.Max(0,attributes.Size-65536):offset.Value;stream.Position=start;
   var bytes=new byte[(int)Math.Min(65536,Math.Max(0,attributes.Size-start))];var read=0;while(read<bytes.Length){cancellation.ThrowIfCancellationRequested();var n=stream.Read(bytes,read,bytes.Length-read);if(n==0)break;read+=n;}cancellation.ThrowIfCancellationRequested();return new RemoteLogChunk(start+read,Convert.ToBase64String(bytes,0,read),fingerprint,reset,attributes.Size,attributes.LastWriteTimeUtc);
  },cancellation);}finally{_files.Release();}
 }
}
