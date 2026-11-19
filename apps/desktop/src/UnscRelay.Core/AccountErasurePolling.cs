using System.Text.Json;
namespace UnscRelay.Core;
/// <summary>Durable round-robin batches prevent unavailable accounts from starving later ones.</summary>
public sealed class AccountErasurePolling(string directory)
{
 sealed record Cursor(int Version,Guid After);
 public IReadOnlyList<LocalAccountEntry> Next(IEnumerable<LocalAccountEntry> accounts){var ordered=accounts.OrderBy(value=>value.Workspace.ToString("N"),StringComparer.Ordinal).ToArray();if(ordered.Length==0)return [];const string name="erasure-scan.json";Guid after=Guid.Empty;if(File.Exists(LocalDataFiles.Child(directory,name))){try{var value=JsonSerializer.Deserialize<Cursor>(LocalDataFiles.Read(directory,name,256),VaultSyncCrypto.Json)??throw new InvalidDataException("Cursore di verifica account non valido.");if(value.Version!=1)throw new InvalidDataException("Versione della verifica account non valida.");after=value.After;}catch(Exception error)when(error is JsonException or InvalidDataException){after=Guid.Empty;/* Only the replaceable scheduling cursor resets; signed receipts never do. */}}var start=Array.FindIndex(ordered,value=>StringComparer.Ordinal.Compare(value.Workspace.ToString("N"),after.ToString("N"))>0);if(start<0)start=0;var result=Enumerable.Range(0,Math.Min(4,ordered.Length)).Select(offset=>ordered[(start+offset)%ordered.Length]).ToArray();LocalDataFiles.Write(directory,name,JsonSerializer.SerializeToUtf8Bytes(new Cursor(1,result[^1].Workspace),VaultSyncCrypto.Json));return result;}
}
