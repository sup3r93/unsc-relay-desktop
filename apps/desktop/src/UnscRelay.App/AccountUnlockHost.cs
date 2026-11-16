using System.Security.Cryptography;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    AccountUnlockStore AccountUnlocks=>new(dataPath);
    bool AccountHelloAllowed()
    {
        if(cloud==null||!cloud.State.Activated)return false;
        try{return AccountUnlocks.HelloAllowed(cloud.State.Owner,vault.WindowsHello()?.KeyName);}
        catch(Exception ex) when(ex is CryptographicException or System.IO.IOException or System.Text.Json.JsonException or ArgumentException){return false;}
    }
    async Task UnlockOnlineAfterLogin(long generation)
    {
        byte[]? remote=null;
        try{
            using var existing=AccountUnlocks.Read(cloud.State.Owner);
            var device=existing?.Device??Guid.NewGuid();
            remote=await cloud.DeviceUnlock(device,existing==null,cloudLifetime.Token);
            if(existing!=null)await Task.Run(()=>vault.UnlockAccount(existing,remote,cloud.State.Owner));
            else{
                if(vault.Exists&&!vault.IsUnlocked){
                    var legacy=SecretDialog.Ask(this,"Migrazione sicura dell’archivio","Solo questa volta: inserisci la vecchia password dell’archivio locale per conservarne chiavi e dati. Nei prossimi accessi userai il tuo account con 2FA.",false);
                    await Task.Run(()=>vault.Unlock(legacy));
                }
                if(!vault.Exists){var seed=RandomNumberGenerator.GetBytes(32);try{await Task.Run(()=>vault.Unlock(Convert.ToBase64String(seed)));}finally{CryptographicOperations.ZeroMemory(seed);}}
                if(generation!=authenticationGeneration)throw new OperationCanceledException("La sessione è stata bloccata. Ripeti l’accesso.");
                SyncLocalBackup.Create(dataPath,false);
                using var binding=vault.BindAccount(cloud.State.Owner,device,remote);
                AccountUnlocks.Save(binding);
                SyncLocalBackup.Complete(dataPath);
            }
            CompleteAppAuthentication(generation);
        }catch{vault.Lock();throw;}
        finally{if(remote!=null)CryptographicOperations.ZeroMemory(remote);}
    }
}
