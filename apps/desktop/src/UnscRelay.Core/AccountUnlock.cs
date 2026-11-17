using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace UnscRelay.Core;

// The server half alone cannot decrypt the local vault. The device half is DPAPI protected.
// Neither the account password nor the online half is stored in this packet.
public sealed record AccountUnlockBinding(int Version,string Owner,Guid Device,byte[] Salt,byte[] DevicePart,byte[] Nonce,byte[] Tag,byte[] Ciphertext,string HelloKey="") : IDisposable
{
 public void Dispose(){if(DevicePart!=null)CryptographicOperations.ZeroMemory(DevicePart);}
 public void Validate(string owner){if(Version is not (1 or 2)||Owner!=owner||owner is not {Length:64}||owner.Any(c=>!char.IsAsciiHexDigit(c))||Device==Guid.Empty||Salt is not {Length:16}||DevicePart is not {Length:32}||Nonce is not {Length:12}||Tag is not {Length:16}||Ciphertext is not {Length:32}||HelloKey==null||HelloKey.Length>200)throw new CryptographicException("Collegamento locale non valido. L’archivio è conservato.");}
}
public sealed class AccountUnlockStore(string directory)
{
 internal string DirectoryPath=>Path.GetFullPath(directory);
 string FileName=>Path.Combine(directory,"account-unlock.dpapi");
 public bool Exists=>File.Exists(FileName);
 public AccountUnlockBinding? Read(string owner){
  if(!Exists)return null;if(new FileInfo(FileName).Length>16384)throw new InvalidDataException("Collegamento locale non valido.");
  var bytes=UpdateTokenStore.Transform(File.ReadAllBytes(FileName),true);
  try{var binding=JsonSerializer.Deserialize<AccountUnlockBinding>(bytes)??throw new InvalidDataException("Collegamento vuoto.");try{binding.Validate(owner);return binding;}catch{binding.Dispose();throw;}}finally{CryptographicOperations.ZeroMemory(bytes);}
 }
 public void Save(AccountUnlockBinding binding){binding.Validate(binding.Owner);var bytes=JsonSerializer.SerializeToUtf8Bytes(binding);try{AtomicFile.Write(FileName,UpdateTokenStore.Transform(bytes,false));}finally{CryptographicOperations.ZeroMemory(bytes);}}
 public bool HelloAllowed(string owner,string? key){using var binding=Read(owner);return binding!=null&&key!=null&&binding.HelloKey==key;}
 public void ConfirmHello(string owner,string key){using var binding=Read(owner)??throw new InvalidOperationException("Completa prima l’accesso online al dispositivo.");Save(binding with{HelloKey=key});}
}
public sealed partial class KeyVault
{
 static byte[] AccountAad(string owner,Guid device)=>Encoding.UTF8.GetBytes("UNSC-Relay/account-unlock/v1/"+owner+"/"+device.ToString("N"));
 static byte[] AccountKek(byte[] devicePart,byte[] remotePart,byte[] salt,byte[] aad){if(devicePart.Length!=32||remotePart.Length!=32||salt.Length!=16)throw new CryptographicException("Materiale di sblocco non valido.");var combined=new byte[64];devicePart.CopyTo(combined,0);remotePart.CopyTo(combined,32);try{return HKDF.DeriveKey(HashAlgorithmName.SHA256,combined,32,salt,aad);}finally{CryptographicOperations.ZeroMemory(combined);}}
 public AccountUnlockBinding BindAccount(string owner,Guid device,byte[] remotePart){lock(_gate){RequireUnlocked();var part=RandomNumberGenerator.GetBytes(32);var aad=AccountAad(owner,device);var kek=AccountKek(part,remotePart,_salt!,aad);try{var nonce=RandomNumberGenerator.GetBytes(12);var tag=new byte[16];var cipher=new byte[32];using(var aes=new AesGcm(kek,16))aes.Encrypt(nonce,_key!,cipher,tag,aad);var binding=new AccountUnlockBinding(1,owner,device,_salt!.ToArray(),part,nonce,tag,cipher);binding.Validate(owner);return binding;}catch{CryptographicOperations.ZeroMemory(part);throw;}finally{CryptographicOperations.ZeroMemory(kek);}}}
 // Rotate the data key before retiring password-based access. The journal restores
 // both the vault and its device capsule if saving either file is interrupted.
 public void MigrateAccountUnlock(AccountUnlockStore storage,string owner,Guid device,byte[] remotePart)
 {
  lock(_gate){
   RequireUnlocked();
   if(owner is not {Length:64}||owner.Any(c=>!char.IsAsciiHexDigit(c))||device==Guid.Empty||remotePart is not {Length:32})throw new CryptographicException("Materiale di sblocco non valido.");
   var directory=storage.DirectoryPath;
   if(!string.Equals(Path.GetFullPath(path),Path.Combine(directory,"keys.vault"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Percorso dell’archivio non valido.");
   if(File.Exists(Path.Combine(directory,"sync-transaction.json")))throw new InvalidOperationException("Completa prima il recupero dell’archivio.");
   SyncLocalBackup.Create(directory,true);
   var previousKey=_key!;
   try{
    _key=RandomNumberGenerator.GetBytes(32);_salt=RandomNumberGenerator.GetBytes(16);
    _passkeys=[];_windowsHello=null;
    using var binding=BindAccount(owner,device,remotePart);
    Save();storage.Save(binding with{Version=2});
    SyncLocalBackup.Complete(directory);
   }catch{Lock();SyncLocalBackup.Recover(directory);throw;}
   finally{CryptographicOperations.ZeroMemory(previousKey);}
  }
 }
 public void UnlockAccount(AccountUnlockBinding binding,byte[] remotePart,string owner){lock(_gate){Lock();binding.Validate(owner);var envelope=ReadEnvelope();ValidateEnvelope(envelope);var salt=Convert.FromBase64String(envelope.Salt);if(!CryptographicOperations.FixedTimeEquals(salt,binding.Salt))throw new CryptographicException("L’archivio locale è cambiato. Nessun dato è stato sostituito.");var aad=AccountAad(owner,binding.Device);var kek=AccountKek(binding.DevicePart,remotePart,salt,aad);var key=new byte[32];byte[]? plain=null;
  try{using(var aes=new AesGcm(kek,16))aes.Decrypt(binding.Nonce,binding.Ciphertext,binding.Tag,key,aad);var cipher=Convert.FromBase64String(envelope.Ciphertext);if(cipher.Length>32*1024*1024)throw new InvalidDataException("Archivio troppo grande.");plain=new byte[cipher.Length];using(var aes=new AesGcm(key,16))aes.Decrypt(Convert.FromBase64String(envelope.Nonce),cipher,Convert.FromBase64String(envelope.Tag),plain,PayloadAad(envelope.Version,envelope.Passkeys,envelope.WindowsHello));_records=JsonSerializer.Deserialize<List<KeyRecord>>(plain)??[];_key=key;key=[];_salt=salt;_passkeys=envelope.Passkeys?.ToList()??[];_windowsHello=envelope.WindowsHello;LastAccessUtc=DateTime.UtcNow;
  }catch{Lock();throw;}finally{CryptographicOperations.ZeroMemory(kek);CryptographicOperations.ZeroMemory(key);if(plain!=null)CryptographicOperations.ZeroMemory(plain);}
 }}
}
