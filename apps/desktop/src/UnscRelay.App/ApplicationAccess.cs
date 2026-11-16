using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.App;
public partial class MainWindow
{
    bool appAuthenticated;
    bool windowsSessionLocked;
    long authenticationGeneration;
    bool consoleReady;
    System.Diagnostics.Stopwatch? accessFinalWatch;
    bool accessBusy;
    bool AppLocked => (windowsSessionLocked || data.Settings.RequireAppAuthentication && !appAuthenticated) && (!smokeTest||accessSmokeTest);
    async Task VerifyLocalIdentity(string purpose,string? method=null,bool unlockArchive=false)
    {
        if(accessBusy||helloBusy)throw new InvalidOperationException("Una verifica di identità è già aperta.");
        if(!vault.Exists)throw new InvalidOperationException("Crea prima l’archivio con una password principale.");
        accessBusy=true;
        try {
            if(method==null){var choice=new AccessApprovalDialog(this,purpose,"Conferma la tua identità. Non vengono condivise password o chiavi private.",vault.WindowsHello()!=null);if(choice.ShowDialog()!=true)throw new OperationCanceledException();method=choice.Method;}
            if(method=="hello"){
                if(unlockArchive)await UnlockHello();
                else {helloBusy=true;try{var challenge=vault.PrepareWindowsHelloIdentity();var signature=await WindowsHelloService.SignAsync(challenge);try{await Task.Run(()=>vault.VerifyWindowsHelloIdentity(challenge,signature));}finally{CryptographicOperations.ZeroMemory(signature);}}finally{helloBusy=false;}}
            }
            else if(method=="password"){var secret=Secret(purpose,"Conferma la password principale dell’archivio:");await Task.Run(()=>{vault.VerifyMasterPassword(secret);if(unlockArchive)vault.Unlock(secret);});}
            else throw new InvalidOperationException("Metodo non disponibile.");
        } finally {accessBusy=false;}
    }
    object AccessBootstrap()
    {
        if(!AppLocked&&!AccountLocked)return Snapshot();
        return new {account=CloudStatus(),data=new {profiles=Array.Empty<object>(),folders=Array.Empty<string>(),snippets=Array.Empty<object>(),workspaces=Array.Empty<object>(),settings=new {requireAppAuthentication=true,startupVolume=data.Settings.StartupVolume}},vault=new{exists=vault.Exists,unlocked=false,keys=Array.Empty<object>(),windowsHello=vault.WindowsHello()!=null},hostKeys=Array.Empty<object>(),version=CurrentVersion.ToString(),diagnostic=false,appLocked=AppLocked};
    }
    async Task<object?> AccessAction(string action,JsonElement p)
    {
        switch(action){
            case "applicationAuthenticate":
                if(AccountLocked)throw new InvalidOperationException("Collega prima l’account al sito UNSC.");
                if(!AppLocked)return Snapshot();
                if(startupSequencePlaying)throw new InvalidOperationException("Attendi la sequenza iniziale.");
                if(accessBusy||helloBusy)throw new InvalidOperationException("Una verifica è già in corso.");
                var generation=authenticationGeneration;var method=S(p,"method");
                if(method=="hello")await VerifyLocalIdentity("Accesso a UNSC Relay","hello",true);
                else if(method=="password"){
                    var password=S(p,"password");if(password.Length is <1 or >4096)throw new ArgumentException("Inserisci la password principale.");
                    accessBusy=true;try{await Task.Run(()=>vault.Unlock(password));}finally{accessBusy=false;}
                }else throw new ArgumentException("Scegli un metodo di accesso disponibile.");
                if(generation!=authenticationGeneration){vault.Lock();throw new OperationCanceledException("La sessione Windows è stata bloccata. Ripeti l’accesso.");}
                if(!vault.IsUnlocked)throw new InvalidOperationException("L’archivio non è stato sbloccato.");
                windowsSessionLocked=false;DesktopLayer.Visibility=System.Windows.Visibility.Visible;foreach(var floating in detachedWindows.Values)floating.Show();appAuthenticated=true;accessFinalWatch=System.Diagnostics.Stopwatch.StartNew();return Snapshot();
            case "applicationExit":
                if(!AppLocked&&!AccountLocked)throw new InvalidOperationException("Accesso già completato.");Close();return null;
            case "accessSettings":
                bool? requireApp=p.TryGetProperty("requireAppAuthentication",out var appFlag)?appFlag.GetBoolean():null;
                bool? enableApi=p.TryGetProperty("assistantApiEnabled",out var apiFlag)?apiFlag.GetBoolean():null;
                if(requireApp==null&&enableApi==null)throw new ArgumentException("Nessuna impostazione specificata.");
                await VerifyLocalIdentity("Modifica protezione e API");
                var previousAccessSettings=data.Settings;
                data.Settings=data.Settings with {RequireAppAuthentication=requireApp??data.Settings.RequireAppAuthentication,AssistantApiEnabled=enableApi??data.Settings.AssistantApiEnabled};
                try{store.Save(data);}catch{data.Settings=previousAccessSettings;throw;}if(!data.Settings.AssistantApiEnabled)assistantOutput.Clear();appAuthenticated=true;return Snapshot();
            default:throw new ArgumentException("Azione non valida.");
        }
    }
}
