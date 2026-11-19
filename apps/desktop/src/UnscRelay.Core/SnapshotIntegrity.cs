using System.Text.Json;
namespace UnscRelay.Core;
public sealed record IntegrityFinding(string Code,string Subject);
public sealed record SnapshotIntegrityReport(DateTimeOffset CheckedAt,int Records,int Profiles,int HostKeys,int WarningCount,List<IntegrityFinding> Findings);
public sealed record IntegrityObservation(Guid ArchiveId,long Revision,DateTimeOffset CheckedAt,bool Offline,bool Pending,int Warnings);
public static class SnapshotIntegrity
{
 static JsonElement Property(JsonElement item,string name)=>item.EnumerateObject().LastOrDefault(property=>property.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Value;
 public static SnapshotIntegrityReport Inspect(PortableSnapshot snapshot){
  var keys=new HashSet<string>(StringComparer.Ordinal);var passwords=new HashSet<string>(StringComparer.Ordinal);foreach(var record in snapshot.Records.EnumerateArray()){var info=Property(record,"info");var id=Property(info,"id").GetString()!;var kind=Property(info,"kind").GetString();if(kind is "ssh-key" or "ssh-native-key")keys.Add(id);if(kind=="password")passwords.Add(id);}
  var findings=new List<IntegrityFinding>();var count=0;void Warn(string code,string subject){count++;if(findings.Count<100)findings.Add(new(code,subject));}
  foreach(var profile in snapshot.Data.Profiles){if(!string.IsNullOrEmpty(profile.KeyId)&&!keys.Contains(profile.KeyId))Warn("missing-key",profile.Name);if(!string.IsNullOrEmpty(profile.PasswordId)&&!passwords.Contains(profile.PasswordId))Warn("missing-password",profile.Name);}
  var profiles=snapshot.Data.Profiles.Select(profile=>profile.Id).ToHashSet(StringComparer.Ordinal);foreach(var workspace in snapshot.Data.Workspaces)foreach(var id in workspace.ProfileIds)if(!profiles.Contains(id))Warn("missing-profile",workspace.Name);
  return new(DateTimeOffset.UtcNow,snapshot.Records.GetArrayLength(),snapshot.Data.Profiles.Count,snapshot.HostKeys.Count,count,findings);
 }
}
