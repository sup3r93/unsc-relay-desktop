using System.Buffers.Binary;
using System.Text.Json;
using UnscRelay.Core;
static class AssistantChecks
{
 public static async Task<int> Run(){int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
 var empty=JsonSerializer.SerializeToElement(new{});foreach(var name in new[]{"bootstrap","vaultUnlock","vaultGenerate","exportProfiles","settings","nativeTask","transfer","clipboardRead"}){bool denied=false;try{AssistantPolicy.Validate(name,empty);}catch(ArgumentException){denied=true;}Check(denied,"Private RPC rejected: "+name);}
 foreach(var tool in new[]{"relay_status","relay_profiles","relay_sessions"}){AssistantPolicy.Validate(tool,empty);Check(true,"Narrow read tool accepted: "+tool);}
 bool Reject(string tool,object p){try{AssistantPolicy.Validate(tool,JsonSerializer.SerializeToElement(p));return false;}catch(ArgumentException){return true;}}
 Check(Reject("relay_sftp_write",new{sessionId=Guid.NewGuid(),path="/tmp/a",text="abc",expectedHash="fake"}),"Write requires exact content hash");
 Check(Reject("relay_transfer",new{sessionId=Guid.NewGuid(),local="C:\\a",remote="/a",direction="delete"}),"Transfer direction constrained");
 Check(Reject("relay_status",new{secret="x"}),"Unknown request fields rejected");
 Check(Reject("relay_send_input",new{sessionId=Guid.NewGuid(),text=new string('x',9000)}),"Terminal input bounded");
 var settings=SequencePolicy.Apply(new UserSettings(RequireAppAuthentication:true,AssistantApiEnabled:true));Check(settings.RequireAppAuthentication&&settings.AssistantApiEnabled,"Sequence normalization preserves access preferences");
 Check(!new UserSettings().RequireAppAuthentication&&!new UserSettings().AssistantApiEnabled,"New access options default off");
 using(var stream=new MemoryStream()){await AssistantTransport.Write(stream,new{text="roundtrip",count=3},CancellationToken.None);stream.Position=0;using var doc=JsonDocument.Parse(await AssistantTransport.Read(stream,CancellationToken.None));Check(doc.RootElement.GetProperty("count").GetInt32()==3,"Framed transport round trip");}
 foreach(var length in new[]{-1,0,4*1024*1024+1}){using var stream=new MemoryStream();var prefix=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(prefix,length);stream.Write(prefix);stream.Position=0;bool denied=false;try{await AssistantTransport.Read(stream,CancellationToken.None);}catch(InvalidDataException){denied=true;}Check(denied,"Malformed frame length rejected: "+length);}

 var directory=Path.Combine(Path.GetTempPath(),"relay-identity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
 try{
  const string master="Hello identity fixture 024!";using var vault=new KeyVault(Path.Combine(directory,"identity.vault"));vault.Unlock(master);
  using var rsa=System.Security.Cryptography.RSA.Create(2048);var binding=vault.PrepareWindowsHello() with{PublicKey=Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())};var wrapping=rsa.SignData(System.Text.Encoding.UTF8.GetBytes(binding.Message),System.Security.Cryptography.HashAlgorithmName.SHA256,System.Security.Cryptography.RSASignaturePadding.Pkcs1);vault.EnableWindowsHello(binding,wrapping,master);vault.Lock();
  var proof=vault.PrepareWindowsHelloIdentity();var next=vault.PrepareWindowsHelloIdentity();Check(proof.Message!=next.Message,"Each identity request has a fresh random challenge");
  var signed=rsa.SignData(System.Text.Encoding.UTF8.GetBytes(proof.Message),System.Security.Cryptography.HashAlgorithmName.SHA256,System.Security.Cryptography.RSASignaturePadding.Pkcs1);vault.VerifyWindowsHelloIdentity(proof,signed);Check(!vault.IsUnlocked,"Identity approval leaves a locked archive locked");
  bool denied=false;try{vault.VerifyWindowsHelloIdentity(proof,wrapping);}catch(System.Security.Cryptography.CryptographicException){denied=true;}Check(denied&&!vault.IsUnlocked,"Archive wrapping proof cannot authorize an identity challenge");
  denied=false;try{vault.VerifyWindowsHelloIdentity(next,signed);}catch(System.Security.Cryptography.CryptographicException){denied=true;}Check(denied,"Proof from another request is rejected");
  vault.Unlock(master);vault.VerifyWindowsHelloIdentity(proof,signed);Check(vault.IsUnlocked,"Identity verification preserves an already unlocked archive");
 }finally{Directory.Delete(directory,true);}
 Console.WriteLine("Assistant policy checks: "+count);return 0;
 }
}
