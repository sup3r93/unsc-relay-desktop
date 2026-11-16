using System.Security.Cryptography;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    bool helloBusy;
    async Task<object> EnableHello()
    {
        if (helloBusy) throw new InvalidOperationException("Completa prima la richiesta Windows Hello aperta.");
        helloBusy = true;
        try
        {
            if (vault.WindowsHello() != null) throw new InvalidOperationException("Windows Hello è già abilitato per questo archivio.");
            var master = Secret("Abilita Windows Hello", "Conferma la password principale. In seguito potrai sbloccare con il PIN o la biometria di Windows Hello.");
            await Task.Run(() => vault.VerifyMasterPassword(master));
            var prepared = vault.PrepareWindowsHello();
            if(!accessBusy)Emit("helloPending", new { });
            var proof = await WindowsHelloService.CreateAsync(prepared); var committed = false;
            try { await Task.Run(() => vault.EnableWindowsHello(proof.Challenge, proof.Signature, master)); committed = true; }
            finally { CryptographicOperations.ZeroMemory(proof.Signature); if (!committed) await WindowsHelloService.DeleteAsync(proof.Challenge.KeyName); }
            return Snapshot();
        }
        finally { helloBusy = false; if(!accessBusy)Emit("helloFinished", new { }); }
    }
    async Task<object> UnlockHello()
    {
        if (helloBusy) throw new InvalidOperationException("Completa prima la richiesta Windows Hello aperta.");
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
