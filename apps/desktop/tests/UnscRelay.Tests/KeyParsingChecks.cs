using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;
using UnscRelay.Core;

static class KeyParsingChecks
{
 public static int Run()
 {
  var count=0;
  void Reject(string text,string label){var watch=Stopwatch.StartNew();try{using var stream=new MemoryStream(Encoding.UTF8.GetBytes(text));using var key=new PrivateKeyFile(stream);throw new Exception("Invalid key accepted");}catch(Exception ex)when(ex is SshException or RegexMatchTimeoutException or FormatException){if(watch.Elapsed>TimeSpan.FromSeconds(5))throw new Exception("Key parsing exceeded bound");Console.WriteLine("PASS "+label);count++;}}
  Reject(new string('A',1024*1024+1),"oversized private key rejected before parsing");
  Reject("-----BEGIN "+string.Join(" ",Enumerable.Repeat("A",10000))+"!", "malformed PEM header has bounded parsing time");
  Reject("PuTTY-User-Key-File-3: ssh-ed25519\nEncryption: none\nComment: fixture\nPublic-Lines: 1\nAAAA\nPrivate-Lines: 80\n"+string.Concat(Enumerable.Repeat("AAAA\n",80))+"!", "malformed PPK data does not trigger nested backtracking");
  var directory=Path.Combine(Path.GetTempPath(),"relay-key-parsing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  try{using var vault=new KeyVault(Path.Combine(directory,"keys.vault"));vault.Unlock("Parser regression fixture only!");foreach(var algorithm in new[]{"ed25519","rsa3072","ecdsa256","ecdsa384","ecdsa521"}){var info=KeyGeneration.Generate(vault,algorithm,algorithm);if(info.Fingerprint.Length==0)throw new Exception("Valid key rejected");Console.WriteLine("PASS valid "+algorithm+" import");count++;}}
  finally{if(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(directory,true);}
  Console.WriteLine($"PASS {count} key parsing checks");return 0;
 }
}
