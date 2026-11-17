using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
internal static class ArchiveInteropChecks
{
 public static async Task<int> Run()
 {
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));var input=await Console.In.ReadLineAsync(deadline.Token)??throw new Exception("Missing synthetic interop input");using var request=JsonDocument.Parse(input);
  var root=request.RootElement;var archive=root.GetProperty("archiveId").GetGuid();var device=root.GetProperty("deviceId").GetGuid();var owner=root.GetProperty("archiveOwner").GetString()!;var account=root.GetProperty("accountOwner").GetString()!;var bytes=root.GetProperty("key").GetBytesFromBase64();var local=ArchiveKeyCrypto.Generate();
  try
  {
   var grant=ArchiveKeyCrypto.Wrap(archive,owner,account,device,root.GetProperty("publicKey").GetString()!,bytes);await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new{publicKey=local.PublicKey,grant},VaultSyncCrypto.Json));
   var answer=await Console.In.ReadLineAsync(deadline.Token)??throw new Exception("Missing return grant");var returned=JsonSerializer.Deserialize<ArchiveKeyGrant>(answer,VaultSyncCrypto.Json)??throw new Exception("Invalid return grant");var restored=ArchiveKeyCrypto.Unwrap(returned,archive,owner,account,device,local.PrivateKey);try{if(!CryptographicOperations.FixedTimeEquals(restored,bytes))throw new Exception("Interop key mismatch");}finally{CryptographicOperations.ZeroMemory(restored);}await Console.Out.WriteLineAsync("PASS cross-runtime archive key interoperability");return 0;
  }
  finally{CryptographicOperations.ZeroMemory(local.PrivateKey);CryptographicOperations.ZeroMemory(bytes);}
 }
}
