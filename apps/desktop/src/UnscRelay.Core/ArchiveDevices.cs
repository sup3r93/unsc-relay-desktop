using System.Security.Cryptography;
namespace UnscRelay.Core;
public sealed class ArchiveDeviceKey(Guid id,string account,string publicKey,byte[] privateKey):IDisposable
{
 public Guid Id{get;}=id;public string Account{get;}=account;public string PublicKey{get;}=publicKey;public byte[] PrivateKey{get;}=privateKey;
 public void Dispose()=>CryptographicOperations.ZeroMemory(PrivateKey);
}
public sealed partial class KeyVault
{
 public ArchiveDeviceKey SharingDevice(string account,bool create=true)
 {
  if(account is not {Length:64}||!account.All(char.IsAsciiHexDigit))throw new ArgumentException("Account non valido.");
  lock(_gate)
  {
   RequireUnlocked();var record=_records.SingleOrDefault(r=>r.Info.Kind=="archive-device"&&r.Info.Comment==account);
   if(record is null)
   {
    if(!create)throw new InvalidOperationException("Prepara il dispositivo per questo account.");var generated=ArchiveKeyCrypto.Generate();
    record=new KeyRecord(new KeyInfo(Guid.NewGuid().ToString("D"),"Identità condivisione dispositivo",account,DateTime.UtcNow,generated.PublicKey,Kind:"archive-device"),generated.PrivateKey);
    _records.Add(record);try{Save();}catch{_records.Remove(record);CryptographicOperations.ZeroMemory(record.Content);throw;}
   }
   return new(Guid.Parse(record.Info.Id),account,record.Info.PublicKey,record.Content.ToArray());
  }
 }
}
