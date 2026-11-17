using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using UnscRelay.Core;
internal static class UpdatePolicyChecks
{
 public static int Run()
 {
  var count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS update policy: "+name);}
  using var key=RSA.Create(3072);var request=new CertificateRequest("CN=Synthetic update fixture",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
  string Bundle(UpdatePolicy value){var payload=JsonSerializer.SerializeToUtf8Bytes(value);return JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))});}
  void Deny(Action action,string name){try{action();}catch(Exception e)when(e is CryptographicException or InvalidDataException or FormatException or JsonException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
  Deny(()=>UpdatePolicy.Verify("{}",cert),"missing policy fields fail safely");Deny(()=>UpdatePolicy.Verify("{\"payload\":7,\"signature\":false}",cert),"incorrect JSON field types fail safely");
  var policy=new UpdatePolicy("0.2.19",new string('A',64),["features","security"],"critical","0.2.19");var blob=Bundle(policy);
  var valid=UpdatePolicy.Verify(blob,cert,"v0.2.19",new string('a',64));Check(valid.Blocks(new Version(0,2,18)),"signed critical policy blocks vulnerable versions");Check(!valid.Blocks(new Version(0,2,19)),"updated and newer versions remain usable");
  Check(!(policy with{Security="important"}).Blocks(new Version(0,2,18)),"important security update does not impose a mandatory lock");
  Deny(()=>UpdatePolicy.Verify(blob,cert,"0.2.20",policy.InstallerSha256),"policy is bound to release version");Deny(()=>UpdatePolicy.Verify(blob,cert,policy.Version,new string('B',64)),"policy is bound to exact installer digest");
  using var other=RSA.Create(3072);var otherReq=new CertificateRequest("CN=Untrusted fixture",other,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var otherCert=otherReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));Deny(()=>UpdatePolicy.Verify(blob,otherCert),"different publisher cannot force a lock");
  using(var doc=JsonDocument.Parse(blob)){var bytes=Convert.FromBase64String(doc.RootElement.GetProperty("payload").GetString()!);bytes[0]^=1;var altered=JsonSerializer.Serialize(new{payload=Convert.ToBase64String(bytes),signature=doc.RootElement.GetProperty("signature").GetString()});Deny(()=>UpdatePolicy.Verify(altered,cert),"tampered policy is rejected before deserialisation");}
  Deny(()=>UpdatePolicy.Verify(Bundle(policy with{MinimumSupportedVersion="0.2.20"}),cert),"minimum version cannot exceed offered release");Deny(()=>UpdatePolicy.Verify(Bundle(policy with{Categories=["features"]}),cert),"security severity requires an explicit security category");Deny(()=>UpdatePolicy.Verify(Bundle(policy with{Categories=["security","security"]}),cert),"duplicate categories are rejected");Deny(()=>UpdatePolicy.Verify(Bundle(policy with{Security="urgent"}),cert),"unknown security levels are rejected");
  var now=DateTimeOffset.UtcNow;Check(UpdateSchedule.Due(true,true,false,now,now),"startup option checks on every launch");Check(!UpdateSchedule.Due(true,false,true,null,now),"disabled startup option is respected");Check(!UpdateSchedule.Due(false,true,true,now.AddHours(-23),now),"daily checks do not run repeatedly");Check(UpdateSchedule.Due(false,false,true,now.AddDays(-1),now),"daily check becomes due after 24 hours");Check(!UpdateSchedule.Due(false,true,false,null,now),"disabled daily option is respected");Check(!UpdateSchedule.Due(false,true,true,now.AddHours(1),now),"clock rollback does not cause repeated checks");
  var old=JsonSerializer.Deserialize<UserSettings>("{}",ProfileStore.Json)!;Check(!old.CloseToTray&&old.CheckUpdatesOnStartup&&old.CheckUpdatesDaily,"existing installations retain full close and enable update checks by default");
  Console.WriteLine("Update policy checks: "+count);return 0;
 }
}
