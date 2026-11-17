using System.Security.Cryptography;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    bool helloBusy;
    async Task<object> EnableHello()
    {
        if (helloBusy) throw new InvalidOperationException("Completa prima la richiesta Windows Hello aperta.");
        if(!vault.IsUnlocked)throw new InvalidOperationException("Accedi prima con il tuo account.");
        if(!Confirm("Abilitare Windows Hello per questo account Relay su questo PC? Chiunque possa usare il PIN o i metodi Hello dello stesso account Windows potrebbe aprire l’archivio. Su un account Windows condiviso lascia questa opzione disattivata."))return Snapshot();
        await VerifyLocalIdentity("Conferma account prima di abilitare Windows Hello","account");
        var generation=authenticationGeneration;helloBusy = true;
        try
        {
            using var accountBinding=AccountUnlocks.Read(cloud.State.Owner)??throw new InvalidOperationException("Completa prima l’accesso online.");
            if(vault.WindowsHello()!=null){
                var challenge=vault.PrepareWindowsHelloIdentity();var signature=await WindowsHelloService.SignAsync(challenge);
                try{vault.VerifyWindowsHelloIdentity(challenge,signature);}finally{CryptographicOperations.ZeroMemory(signature);}
                if(generation!=authenticationGeneration)throw new OperationCanceledException("Sessione bloccata. Ripeti l’abilitazione.");
                AccountUnlocks.ConfirmHello(cloud.State.Owner,challenge.KeyName);return Snapshot();
            }
            var prepared = vault.PrepareWindowsHello();
            Emit("helloPending", new { });
            var proof = await WindowsHelloService.CreateAsync(prepared); var committed = false;
            try {
                if(generation!=authenticationGeneration)throw new OperationCanceledException("Sessione bloccata. Ripeti l’abilitazione.");
                vault.EnableWindowsHelloForUnlockedVault(proof.Challenge, proof.Signature);committed=true;
                AccountUnlocks.ConfirmHello(cloud.State.Owner,proof.Challenge.KeyName);
            }
            finally { CryptographicOperations.ZeroMemory(proof.Signature); if (!committed) await WindowsHelloService.DeleteAsync(proof.Challenge.KeyName); }
            return Snapshot();
        }
        finally { helloBusy = false; Emit("helloFinished", new { }); }
    }
    async Task<object> UnlockHello()
    {
        if (helloBusy) throw new InvalidOperationException("Completa prima la richiesta Windows Hello aperta.");
        if(!AccountHelloAllowed())throw new InvalidOperationException("Windows Hello non è abilitato manualmente per questo account e questo PC. Accedi online.");
        var generation=authenticationGeneration;helloBusy = true;
        try
        {
            var challenge = vault.WindowsHello() ?? throw new InvalidOperationException("Sblocca prima con la password e abilita Windows Hello.");
            if(!accessBusy)Emit("helloPending", new { });
            var signature = await WindowsHelloService.SignAsync(challenge);
            try { await Task.Run(() => vault.UnlockWindowsHello(challenge, signature)); }
            finally { CryptographicOperations.ZeroMemory(signature); }
            if(generation!=authenticationGeneration){vault.Lock();throw new OperationCanceledException("La sessione Windows è stata bloccata. Ripeti l’accesso.");}
            return Snapshot();
        }
        finally { helloBusy = false; if(!accessBusy)Emit("helloFinished", new { }); }
    }
}
