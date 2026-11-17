using System.Security.Cryptography;
using UnscRelay.Core;
static class StorageFilesChecks
{
 public static async Task<int> RejectUntrustedTls(string executable,int port)
 {
  var profile=new ConnectionProfile{Name="TLS fixture",Protocol="native",Native=new(){Kind="s3"},Storage=new(){Browser=true,Endpoint="https://127.0.0.1:"+port,Bucket="fixture"},Username="fixture-access",Authentication="password"};
  await using var files=new StorageFiles(executable,profile,"fixture-secret");
  try{await files.List("");throw new Exception("Untrusted certificate accepted");}
  catch(IOException ex)when(!ex.Message.Contains("fixture-secret",StringComparison.Ordinal)) {Console.WriteLine("PASS s3 / Untrusted TLS certificate rejected without leaking credentials");return 0;}
 }
 public static async Task<int> Run(string executable,string kind,int port,string root)
 {
  int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+kind+" / "+name);}
  var p=new ConnectionProfile{Name="Storage fixture",Protocol="native",Native=new(){Kind=kind},Storage=new(){Browser=true,Tls=false,Endpoint="http://127.0.0.1:"+port,Region="us-east-1",Bucket="fixture"},Username=kind=="ftp"?"fixture":"fixture-access",Host="127.0.0.1",Port=port,Authentication="password"};
  foreach(var path in new[]{"../x","a/../b","C:/x","x\\y","a\n"})try{StorageFiles.Normalize(path);throw new Exception("Traversal accepted");}catch(ArgumentException){Check(true,"Reject path "+count);}
  Check(StorageFiles.Normalize("/folder//file.txt/")=="folder/file.txt","Canonical relative paths");
  foreach(var invalid in new[]{p with{Storage=p.Storage with{Endpoint="http://example.com"},Native=new(){Kind="s3"}},p with{Native=p.Native with{ArgumentsJson="[\"--config\",\"unsafe\"]"}},p with{Storage=null!},p with{Hooks=null!},p with{Authentication="auto"},p with{Native=new(){Kind="s3"},Storage=p.Storage with{Endpoint=""}}})try{invalid.Validate();throw new Exception("Invalid storage profile accepted");}catch(ArgumentException){Check(true,"Reject unsafe profile "+count);}
  await using var files=new StorageFiles(executable,p,kind=="ftp"?"fixture-secret":"fixture-secret");
  var rows=await files.List("");Check(rows.Any(x=>x.Name=="existing.txt")&&rows.Any(x=>x.Directory&&x.Name=="folder"),"List root with names and directory metadata");
  var textPath=Path.Combine(root,"comparison.txt");var textBytes=new System.Text.UTF8Encoding(true).GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes("caffè · 比較\r\n")).ToArray();
  await File.WriteAllBytesAsync(textPath,textBytes);await files.Transfer("comparison.txt",textPath,true,false);
  var comparison=await files.ReadText("comparison.txt");Check(comparison.Bom&&comparison.Text=="caffè · 比較\r\n"&&comparison.Hash==Convert.ToHexString(SHA256.HashData(textBytes)),"Text comparison preserves UTF-8 bytes, BOM, CRLF and hash");
  await File.WriteAllBytesAsync(textPath,[0xc3,0x28]);await files.Transfer("invalid.txt",textPath,true,false);
  try{await files.ReadText("invalid.txt");throw new Exception("Invalid text accepted");}catch(IOException){Check(true,"Text comparison rejects invalid UTF-8");}
  await files.Change("deletefile","comparison.txt");await files.Change("deletefile","invalid.txt");
  var local=Path.Combine(root,"local upload.txt");var bytes=RandomNumberGenerator.GetBytes(65537);await File.WriteAllBytesAsync(local,bytes);
  await files.Transfer("new file.txt",local,true,false);Check((await files.List("")).Any(x=>x.Name=="new file.txt"),"Upload file with spaces");
  var downloaded=Path.Combine(root,"download.txt");await files.Transfer("new file.txt",downloaded,false,false);Check((await File.ReadAllBytesAsync(downloaded)).SequenceEqual(bytes),"Binary download preserves bytes");
  await File.WriteAllTextAsync(local,"different");try{await files.Transfer("new file.txt",local,true,false);throw new Exception("Conflict silently overwritten");}catch(IOException){Check(true,"Upload conflict preserves remote content");}
  await files.Transfer("new file.txt",local,true,true);await files.Transfer("new file.txt",downloaded,false,true);Check(await File.ReadAllTextAsync(downloaded)=="different","Explicit replacement works both directions");
  await files.Change("mkdir","new folder");Check((await files.List("")).Any(x=>x.Name=="new folder"&&x.Directory),"Create directory or S3 folder marker");
  await files.Change("moveto","new file.txt","renamed.txt");Check((await files.List("")).Any(x=>x.Name=="renamed.txt")&&!(await files.List("")).Any(x=>x.Name=="new file.txt"),"Rename retains contents");
  await files.Transfer("same content.txt",local,true,false);
  try{await files.Change("moveto","renamed.txt","same content.txt");throw new Exception("Rename consumed source with identical target");}catch(IOException){Check((await files.List("")).Count(x=>x.Name is "renamed.txt" or "same content.txt")==2,"Rename refuses an existing identical target without deleting source");}
  await files.Change("deletefile","same content.txt");
  await files.Change("deletefile","renamed.txt");Check(!(await files.List("")).Any(x=>x.Name=="renamed.txt"),"Delete selected file");
  var boundary=p with{RemoteDirectory="folder"};await using(var scoped=new StorageFiles(executable,boundary,"fixture-secret")){Check((await scoped.List("")).Any(x=>x.Name=="nested.txt")&&!(await scoped.List("")).Any(x=>x.Name=="existing.txt"),"Initial directory defines browser root");}
  if(kind=="ftp"){
   await File.WriteAllTextAsync(downloaded,"keep original");var transfer=files.Transfer("large.bin",downloaded,false,true);await Task.Delay(350);files.Cancel();try{await transfer;throw new Exception("Cancelled download completed");}catch(OperationCanceledException){Check(true,"Cancel stops active process");}
   Check(await File.ReadAllTextAsync(downloaded)=="keep original"&&!Directory.GetFiles(root,"*.part").Any(),"Cancelled download preserves target and cleans partial file");Check((await files.List("")).Length>0,"Connection can be reused after cancellation");
  }
  Console.WriteLine("Storage checks: "+count);return 0;
 }
}
