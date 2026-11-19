using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.Core;
public sealed record ErasureReceipt(string KeyId,string Payload,string Signature);
public sealed record ErasureReceiptClaims(int Version,string Issuer,string Audience,string Owner,string Subject,Guid RequestId,long RequestedAt,long ExecuteAfter,string State,long IssuedAt);
public sealed class ErasureReceiptVerifier
{
 readonly IReadOnlyDictionary<string,string> trusted;
 public ErasureReceiptVerifier():this(ErasureReceiptTrust.Keys){}
 public ErasureReceiptVerifier(IReadOnlyDictionary<string,string> trustedKeys)=>trusted=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>(trustedKeys,StringComparer.Ordinal));
 static byte[] Decode(string value,int maximum){if(value.Length==0||value.Length>maximum||value.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('-' or '_')))throw new InvalidDataException("Ricevuta di cancellazione non verificabile.");var text=value.Replace('-','+').Replace('_','/');return Convert.FromBase64String(text.PadRight((text.Length+3)/4*4,'='));}
 public ErasureReceiptClaims Verify(ErasureReceipt receipt,string owner,string subject,DateTimeOffset now,Guid? expectedRequestId=null)
 {
  try{
   if(owner is not{Length:64}||!owner.All(char.IsAsciiHexDigit)||subject.Length is <1 or >128||subject.Any(char.IsControl)||receipt.KeyId is not{Length:32}||!trusted.TryGetValue(receipt.KeyId,out var encodedKey)||receipt.Payload is null||receipt.Signature is null)throw new InvalidDataException("Ricevuta di cancellazione non verificabile.");
   var bytes=Decode(receipt.Payload,8192);var signature=Decode(receipt.Signature,128);if(signature.Length!=64)throw new InvalidDataException("Firma di cancellazione non valida.");
   using var key=ECDsa.Create();var publicKey=Convert.FromBase64String(encodedKey);key.ImportSubjectPublicKeyInfo(publicKey,out var read);if(read!=publicKey.Length||key.KeySize!=256||Convert.ToHexString(SHA256.HashData(publicKey))[..32]!=receipt.KeyId||!key.VerifyData(bytes,signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))throw new InvalidDataException("Firma di cancellazione non valida.");
   var claims=JsonSerializer.Deserialize<ErasureReceiptClaims>(bytes,VaultSyncCrypto.Json)??throw new InvalidDataException("Ricevuta di cancellazione non valida.");var limit=now.AddMinutes(1).ToUnixTimeMilliseconds();
   if(claims.Version!=1||claims.Issuer!="https://unsc.responda.eu"||claims.Audience!="unsc-relay-desktop-erasure"||claims.Owner!=owner||claims.Subject!=subject||claims.RequestId==Guid.Empty||expectedRequestId.HasValue&&claims.RequestId!=expectedRequestId.Value||claims.State is not("frozen" or "vault" or "identity" or "sql" or "completed")||claims.RequestedAt<0||claims.ExecuteAfter<0||claims.ExecuteAfter>now.ToUnixTimeMilliseconds()||claims.IssuedAt<claims.ExecuteAfter||claims.IssuedAt>limit||claims.ExecuteAfter-claims.RequestedAt!=2592000000L)throw new InvalidDataException("La ricevuta non autorizza la rimozione di questo account.");
   return claims;
  }catch(Exception error)when(error is CryptographicException or FormatException or JsonException or ArgumentException or OverflowException){throw new InvalidDataException("Ricevuta di cancellazione non verificabile.");}
 }
}
internal static class ErasureReceiptTrust
{
 internal static readonly IReadOnlyDictionary<string,string> Keys=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>(StringComparer.Ordinal){["0ECDF15B88E2982337599F983B8AFDDC"]="MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEtMbxaWqJNugUXN5N0tjSftpCVJ+c8mr1vA7noY91/seuiU2hFQvCeU5SsP59Gb7C/85ML70s6klV6Ajxtz6r5A=="});
}
