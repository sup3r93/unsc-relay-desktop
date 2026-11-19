using UnscRelay.Core;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace UnscRelay.App;
public partial class MainWindow
{
    bool OperationsLocked=>CriticalUpdateRequired||AppLocked||AccountLocked;
    void RequireOperationSession(long generation){if(OperationsLocked||generation!=authenticationGeneration)throw new OperationCanceledException("Accesso bloccato durante l’operazione.");}
    void CloseNativeApprovals(){foreach(var window in OwnedWindows.Cast<System.Windows.Window>().Where(window=>window is ConfirmationDialog or AccessApprovalDialog or SecretDialog or PermissionReportWindow).ToArray())window.Close();}
    bool appAuthenticated;
    bool windowsSessionLocked;
    long authenticationGeneration;
    bool consoleReady;
    System.Diagnostics.Stopwatch? accessFinalWatch;
    bool accessBusy;
    bool AppLocked => AccountErasureLocked || (windowsSessionLocked || (data.Settings.RequireAppAuthentication || cloud?.State.Activated==true) && !appAuthenticated) && (!smokeTest||accessSmokeTest);
    async Task VerifyLocalIdentity(string purpose,string? method=null,bool unlockArchive=false)
    {
        if(AccountErasureLocked)throw new InvalidOperationException("Attendi la verifica online della cancellazione dell’account.");
        if(accessBusy||helloBusy)throw new InvalidOperationException("Una verifica di identità è già aperta.");
        if(!vault.Exists)throw new InvalidOperationException("Collega il tuo account prima di autorizzare questa operazione.");
        accessBusy=true;
        try {
            if(method==null){var choice=new AccessApprovalDialog(this,purpose,"Conferma la tua identità. Non vengono condivise password o chiavi private.",AccountHelloAllowed());if(choice.ShowDialog()!=true)throw new OperationCanceledException();method=choice.Method;}
            if(method=="account"){await CloudAction("accountLogin",JsonSerializer.SerializeToElement(new{}));if(!vault.IsUnlocked)throw new InvalidOperationException("Accesso account non completato.");}
            else if(method=="hello"){
                if(!AccountHelloAllowed())throw new InvalidOperationException("Abilita manualmente Windows Hello per questo account e questo PC.");
                if(unlockArchive)await UnlockHello();
                else {helloBusy=true;try{var challenge=vault.PrepareWindowsHelloIdentity();var signature=await WindowsHelloService.SignAsync(challenge);try{await Task.Run(()=>vault.VerifyWindowsHelloIdentity(challenge,signature));}finally{CryptographicOperations.ZeroMemory(signature);}}finally{helloBusy=false;}}
            }
            else throw new InvalidOperationException("Metodo non disponibile.");
        } finally {accessBusy=false;}
    }
    void CompleteAppAuthentication(long generation)
    {
        if(AccountErasureLocked||generation!=authenticationGeneration){LockVault();throw new OperationCanceledException("La sessione Windows è stata bloccata. Ripeti l’accesso.");}
        if(!vault.IsUnlocked)throw new InvalidOperationException("L’archivio non è stato sbloccato.");
        var wasLocked=AppLocked;
        windowsSessionLocked=false;DesktopLayer.Visibility=nativePresentationMode?System.Windows.Visibility.Collapsed:System.Windows.Visibility.Visible;
        if(!nativePresentationMode)foreach(var floating in detachedWindows.Values){floating.DesktopLayer.Visibility=System.Windows.Visibility.Visible;floating.Show();}
        appAuthenticated=true;if(wasLocked)accessFinalWatch=System.Diagnostics.Stopwatch.StartNew();
    }
    object AccessBootstrap()
    {
        if(!AppLocked&&!AccountLocked)return Snapshot();
        return new {account=CloudStatus(),data=new {profiles=Array.Empty<object>(),folders=Array.Empty<string>(),snippets=Array.Empty<object>(),workspaces=Array.Empty<object>(),settings=new {language=InterfaceLocale.Normalize(data.Settings.Language),requireAppAuthentication=true,startupVolume=data.Settings.StartupVolume}},vault=new{exists=vault.Exists,unlocked=false,keys=Array.Empty<object>(),windowsHello=AccountHelloAllowed()},hostKeys=Array.Empty<object>(),version=CurrentVersion.ToString(),criticalUpdateRequired=CriticalUpdateRequired,diagnostic=false,appLocked=AppLocked};
    }
    async Task<object?> AccessAction(string action,JsonElement p)
    {
        switch(action){
            case "applicationAuthenticate":
                if(AccountLocked)throw new InvalidOperationException("Collega prima l’account al sito UNSC.");
                if(!AppLocked)return Snapshot();
                if(startupSequencePlaying)throw new InvalidOperationException("Attendi la sequenza iniziale.");
                if(AccountErasureLocked)throw new InvalidOperationException("Attendi la verifica online della cancellazione dell’account.");
        if(accessBusy||helloBusy)throw new InvalidOperationException("Una verifica è già in corso.");
                var generation=authenticationGeneration;var method=S(p,"method");
                if(method=="account")return await CloudAction("accountLogin",p);
                if(method!="hello")throw new ArgumentException("Accedi con il tuo account oppure con Windows Hello abilitato manualmente.");
                await VerifyLocalIdentity("Accesso a UNSC Relay","hello",true);
                CompleteAppAuthentication(generation);return Snapshot();
            case "applicationExit":
                explicitExit=true;
                if(!CriticalUpdateRequired&&!AppLocked&&!AccountLocked)throw new InvalidOperationException("Accesso già completato.");Close();return null;
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
