using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed record PlainExportInfo(Guid ArchiveId,string ArchiveOwner,string Name,long Revision);
public static class PortablePlainExport
{
 public const int MaxZip=256*1024*1024;
 const string Readme="English: This unencrypted export contains all data in the selected current revision, not historical versions, account security factors or support tickets. Original key file encryption is preserved. SHA-256 detects accidental corruption, not authenticity. Protect this copy yourself.\n\nItaliano: Questa copia in chiaro contiene tutti i dati della revisione corrente selezionata, non le versioni storiche, i fattori di sicurezza o i ticket. Le eventuali passphrase originali delle chiavi sono conservate. Gli SHA-256 rilevano corruzioni accidentali, non provano autenticità. Proteggi questa copia.\n";
 static void Validate(PlainExportInfo info){if(info.ArchiveId==Guid.Empty||info.Revision is <1 or >9007199254740991||info.ArchiveOwner is not{Length:64}||!info.ArchiveOwner.All(char.IsAsciiHexDigit)||info.Name is not{Length:>0 and <=100})throw new InvalidDataException("Archivio o revisione non validi.");}
 public static byte[] Create(byte[] snapshot,PlainExportInfo info,CancellationToken ct=default)
 {
  Validate(info);VaultSyncCrypto.Parse(snapshot);using var doc=JsonDocument.Parse(snapshot);var root=doc.RootElement;var files=new List<(string Path,byte[] Bytes)>();
  try{
   files.Add(("vault.json",snapshot.ToArray()));files.Add(("data.json",JsonSerializer.SerializeToUtf8Bytes(root.GetProperty("data"))));files.Add(("host-keys.json",JsonSerializer.SerializeToUtf8Bytes(root.GetProperty("hostKeys"))));files.Add(("README.txt",Encoding.UTF8.GetBytes(Readme)));var index=new List<object>();var passwords=new List<object>();var n=0;
   foreach(var record in root.GetProperty("records").EnumerateArray()){
    ct.ThrowIfCancellationRequested();var value=record.GetProperty("info");var kind=value.GetProperty("kind").GetString();var path="records/"+(++n).ToString("D5",CultureInfo.InvariantCulture);var name=kind=="password"?"password.txt":kind=="ssh-certificate-revocation"?"revocations.krl":"key.original";var bytes=record.GetProperty("content").GetBytesFromBase64();files.Add((path+"/"+name,bytes));files.Add((path+"/metadata.json",JsonSerializer.SerializeToUtf8Bytes(value)));if(value.TryGetProperty("publicKey",out var pub)&&pub.ValueKind==JsonValueKind.String&&pub.GetString() is{Length:>0} publicKey)files.Add((path+"/public.pub",Encoding.UTF8.GetBytes(publicKey)));index.Add(new{id=value.GetProperty("id").GetString(),name=value.GetProperty("name").GetString(),kind,path=path+"/"+name});if(kind=="password")passwords.Add(new{id=value.GetProperty("id").GetString(),name=value.GetProperty("name").GetString(),password=new UTF8Encoding(false,true).GetString(bytes),comment=value.TryGetProperty("comment",out var comment)?comment.GetString():""});
   }
   files.Add(("records/index.json",JsonSerializer.SerializeToUtf8Bytes(index)));files.Add(("passwords.json",JsonSerializer.SerializeToUtf8Bytes(passwords)));return Finish(files,info,"portable-snapshot-v1",new{records=n,passwords=passwords.Count,profiles=root.GetProperty("data").GetProperty("profiles").GetArrayLength()},ct);
  }finally{foreach(var file in files)CryptographicOperations.ZeroMemory(file.Bytes);}
 }
 public static byte[] Legacy(string name,byte[] bytes,PlainExportInfo info,CancellationToken ct=default)
 {
  Validate(info);if(name.Length>4096||bytes.Length>4*1024*1024)throw new InvalidDataException("File precedente non valido.");var files=new List<(string Path,byte[] Bytes)>{("legacy/content.bin",bytes.ToArray()),("legacy/original-name.json",JsonSerializer.SerializeToUtf8Bytes(new{originalName=name})),("README.txt",Encoding.UTF8.GetBytes(Readme+"\nEnglish: Legacy format contains a single file, not a portable snapshot.\nItaliano: Il formato precedente contiene un solo file, non uno snapshot portabile.\n"))};try{return Finish(files,info,"legacy-single-file-v1",new{files=1},ct);}finally{foreach(var file in files)CryptographicOperations.ZeroMemory(file.Bytes);}
 }
 static byte[] Finish(List<(string Path,byte[] Bytes)> files,PlainExportInfo info,string format,object counts,CancellationToken ct)
 {
  if(files.Count+1>60010||files.Sum(f=>(long)f.Bytes.Length+2*f.Path.Length+100)>MaxZip-1048576)throw new InvalidDataException("Esportazione oltre i limiti: nessun file parziale è stato creato.");var manifest=new{format="unsc-relay-plaintext-export-v1",archiveId=info.ArchiveId,archiveOwner=info.ArchiveOwner,name=info.Name,revision=info.Revision,exportedAt=DateTimeOffset.UtcNow,scope="current-revision",historyIncluded=false,encrypted=false,contentFormat=format,originalKeyEncryptionPreserved=true,integrity="SHA-256 detects accidental corruption; not an authenticity signature",counts,files=files.Select(f=>new{path=f.Path,bytes=f.Bytes.Length,sha256=Convert.ToHexString(SHA256.HashData(f.Bytes)).ToLowerInvariant()}).ToArray()};files.Add(("manifest.json",JsonSerializer.SerializeToUtf8Bytes(manifest)));using var output=new MemoryStream();
  try{using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true,Encoding.UTF8)){foreach(var file in files){ct.ThrowIfCancellationRequested();var entry=zip.CreateEntry(file.Path,CompressionLevel.NoCompression);entry.LastWriteTime=new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero);using var stream=entry.Open();stream.Write(file.Bytes);if(output.Length>MaxZip)throw new InvalidDataException("Pacchetto oltre 256 MiB.");}}ct.ThrowIfCancellationRequested();if(output.Length>MaxZip)throw new InvalidDataException("Pacchetto oltre 256 MiB.");return output.ToArray();}finally{if(output.TryGetBuffer(out var buffer))CryptographicOperations.ZeroMemory(buffer.AsSpan());}
 }
 public static (string Name,byte[] Bytes) OpenLegacy(JsonElement envelope,string password)
 {
  if(envelope.GetProperty("version").GetInt32()!=1||envelope.GetProperty("kdf").GetString()!="PBKDF2-SHA256"||envelope.GetProperty("iterations").GetInt32()!=600000||password.Length>4096)throw new InvalidDataException("Pacchetto precedente non valido.");var salt=envelope.GetProperty("salt").GetBytesFromBase64();var iv=envelope.GetProperty("iv").GetBytesFromBase64();var cipher=envelope.GetProperty("ciphertext").GetBytesFromBase64();if(salt.Length!=16||iv.Length!=12||cipher.Length is <16 or >7600000)throw new InvalidDataException("Pacchetto precedente non valido.");var key=Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);var plain=new byte[cipher.Length-16];try{using var aes=new AesGcm(key,16);aes.Decrypt(iv,cipher.AsSpan(0,plain.Length),cipher.AsSpan(plain.Length),plain,"UNSC-RELAY-WEB:1"u8);using var doc=JsonDocument.Parse(plain);var name=doc.RootElement.GetProperty("name").GetString()??throw new InvalidDataException("Nome originale assente.");var bytes=doc.RootElement.GetProperty("data").GetBytesFromBase64();if(bytes.Length>4*1024*1024||name.Length>4096){CryptographicOperations.ZeroMemory(bytes);throw new InvalidDataException("File precedente oltre il limite.");}return(name,bytes);}finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(plain);}
 }
}
