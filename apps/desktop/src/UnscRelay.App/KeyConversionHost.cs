using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using UnscRelay.Core;
using Renci.SshNet.Common;
namespace UnscRelay.App;
public partial class MainWindow
{
    KeyConversion? keyConversion;
    bool conversionOperation;
    string conversionId="";
    readonly System.Windows.Threading.DispatcherTimer conversionTimer=new(){Interval=TimeSpan.FromSeconds(15)};
    void InitializeKeyConversion(){conversionTimer.Tick+=(_,_)=>{if(!conversionOperation&&keyConversion!=null&&DateTime.UtcNow>keyConversion.Expires){CancelKeyConversion();Emit("notice",new{text="Conversione scaduta: seleziona nuovamente l’originale."});}};conversionTimer.Start();Closed+=(_,_)=>{conversionTimer.Stop();CancelKeyConversion();};}
    void CancelKeyConversion(){keyConversion?.Dispose();keyConversion=null;conversionId="";}
    bool KeyConversionBusy=>keyConversion!=null;
    string PuttygenPath=>KeyConversion.VerifyTool(Path.Combine(AppContext.BaseDirectory,"Tools","PuTTYgen","puttygen.exe"));
    object ConversionStatus()=>new{active=keyConversion!=null,id=conversionId,source=keyConversion==null?null:new{fileName=Path.GetFileName(keyConversion.SourcePath),keyConversion.Original.Format,keyConversion.Original.Algorithm,keyConversion.Original.Bits,keyConversion.Original.Fingerprint},target=keyConversion?.Target,expires=keyConversion?.Expires,toolVersion=KeyConversion.ToolVersion};
    async Task<object?> KeyConversionAction(string action,JsonElement p)
    {
        if(action=="conversionStatus")return ConversionStatus();
        if(conversionOperation)throw new IOException("Attendi il completamento dell’operazione sulla chiave.");
        conversionOperation=true;try{return await ConversionActionCore(action,p);}finally{conversionOperation=false;}
    }
    async Task<object?> ConversionActionCore(string action,JsonElement p)
    {

        if(action=="conversionCancel"){CancelKeyConversion();return ConversionStatus();}
        if(action=="conversionStart")
        {
            if(keyConversion!=null)throw new IOException("Termina o annulla la conversione corrente.");
            var target=S(p,"target");if(target is not("ppk" or "openssh"))throw new ArgumentException("Scegli PPK oppure OpenSSH.");
            var tool=PuttygenPath;var path=OpenFile("Seleziona l’originale PPK/OpenSSH (non verrà modificato)");if(path==null)return null;
            KeyConversion parsed;
            try{parsed=await Task.Run(()=>new KeyConversion(path,target,""));}
            catch(SshPassPhraseNullOrEmptyException){var pass=Secret("Passphrase originale","Passphrase della chiave da convertire. Non viene salvata né inviata a PuTTYgen.");parsed=await Task.Run(()=>new KeyConversion(path,target,pass));}
            keyConversion=parsed;conversionId=Guid.NewGuid().ToString("N");
            try{LaunchConversionTool(tool,parsed.SourcePath);}catch{CancelKeyConversion();throw;}
            return ConversionStatus();
        }
        if(keyConversion==null||S(p,"id")!=conversionId)throw new IOException("Conversione non disponibile. Seleziona l’originale.");
        if(action=="conversionOpen"){LaunchConversionTool(PuttygenPath,keyConversion.SourcePath);return ConversionStatus();}
        if(action=="conversionFinish")
        {
            var import=p.TryGetProperty("import",out var selected)&&selected.GetBoolean();
            if(import&&!vault.IsUnlocked)throw new IOException("Sblocca l’archivio prima di importare il risultato.");
            var name=S(p,"name").Trim();var comment=S(p,"comment");if(import&&(name.Length is <1 or >100||comment.Length>1000))throw new ArgumentException("Nome: 1-100 caratteri; commento: massimo 1000.");
            var path=OpenFile("Seleziona la chiave convertita salvata da PuTTYgen");if(path==null)return null;
            var pass="";byte[] content=[];ConversionKey info;var conversion=keyConversion;
            try
            {
                try{byte[] candidate=[];info=await Task.Run(()=>conversion.VerifyResult(path,"",out candidate));content=candidate;}
                catch(SshPassPhraseNullOrEmptyException){pass=Secret("Passphrase risultato","Passphrase del file convertito. Può essere diversa da quella originale.");byte[] candidate=[];info=await Task.Run(()=>conversion.VerifyResult(path,pass,out candidate));content=candidate;}
                string? importedId=null;if(import){var bytes=content;var entry=await Task.Run(()=>vault.Import(name,bytes,pass,comment));importedId=entry.Id;}
                var result=new{verified=true,fileName=Path.GetFileName(path),info.Format,info.Fingerprint,info.Algorithm,importedId,snapshot=import?Snapshot():null};CancelKeyConversion();return result;
            }
            finally{CryptographicOperations.ZeroMemory(content);}
        }
        throw new ArgumentException("Azione conversione non disponibile.");
    }
    static void LaunchConversionTool(string tool,string source)
    {
        var start=new ProcessStartInfo(tool){UseShellExecute=false};start.ArgumentList.Add(source);start.ArgumentList.Add("--ppk-param");start.ArgumentList.Add("version=3");start.ArgumentList.Add("-E");start.ArgumentList.Add("sha256");Process.Start(start);
    }
}
