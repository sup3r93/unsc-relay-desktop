using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly Dictionary<string,ConnectionProfile> nativeTasks=[];
    readonly Dictionary<string,LocalLaunch> nativeTaskLaunches=[];
    readonly Dictionary<string,string> generatedNativePaths=[];
    System.Diagnostics.Process? bundledAgent;
    readonly SemaphoreSlim bundledAgentGate=new(1,1);
    async Task EnsureBundledAgent()
    {
        await bundledAgentGate.WaitAsync();try{await StartBundledAgent();}finally{bundledAgentGate.Release();}
    }
    async Task StartBundledAgent()
    {
        if(bundledAgent is {HasExited:false})return;
        var exe=NativeClient.Find("ssh-agent")??throw new IOException("Agent del pacchetto mancante.");var home=Path.Combine(dataPath,"NativeHome");Directory.CreateDirectory(home);var socket=NativeAgent.SocketForHome(home);if(File.Exists(socket))File.Delete(socket);
        var info=new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("-D");info.ArgumentList.Add("-a");info.ArgumentList.Add(NativeClient.CygwinPath(socket));info.Environment["HOME"]=NativeClient.CygwinPath(home);
        bundledAgent=System.Diagnostics.Process.Start(info)??throw new IOException("Agent non avviato.");_=bundledAgent.StandardOutput.ReadToEndAsync();_=bundledAgent.StandardError.ReadToEndAsync();
        Closed+=(_,_)=>StopBundledAgent();
        for(var i=0;i<40&&!File.Exists(socket)&&!bundledAgent.HasExited;i++)await Task.Delay(50);
        if(bundledAgent.HasExited||!File.Exists(socket))throw new IOException("Agent incluso non disponibile. Nessun servizio Windows e stato modificato.");
    }
    bool certificateRenewBusy;
    DateTime nextCertificateRenew=DateTime.UtcNow.AddMinutes(1);
    async Task<object?> AdvancedAction(string action,JsonElement p)
    {
        if(action=="nativeSelectFile")return OpenFile(S(p,"title","Seleziona file del client"));
        if(action=="nativeTools") {
            var kinds=new[]{"ssh","sftp","ssh-keygen","mosh","telnet","serial","ftp","s3","rsh","xdmcp","bash","zsh","step"};var keys=Array.Empty<string>();var ssh=S(p,"ssh");if(ssh.Length==0)ssh=NativeClient.Find("ssh")??"";try{if(ssh.Length>0)keys=(await NativeClient.Capture(ssh,["-Q","key"])).Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries);}catch(IOException){}
            return new{tools=kinds.Select(k=>new{kind=k,executable=NativeClient.Find(k),available=NativeClient.Find(k)!=null}).ToArray(),keys};
        }
        if(action=="nativeTask"){
            var kind=S(p,"kind");var exe=S(p,"executable");var args=new List<string>();string title;
            if(kind=="generate"){
                if(exe.Length==0)exe=NativeClient.Find("ssh-keygen")??throw new IOException("Componente ssh-keygen del pacchetto non disponibile.");
                var algorithm=S(p,"algorithm");if(algorithm is not("ed25519-sk" or "ecdsa-sk" or "mldsa44-ed25519"))throw new ArgumentException("Algoritmo esterno non supportato.");
                var output=SaveFile("Salva identita OpenSSH (riferimento hardware o chiave ibrida)","id_"+algorithm.Replace('-','_'));if(output==null)return null;
                if(File.Exists(output)||File.Exists(output+".pub"))throw new IOException("Scegli un nuovo nome: i file esistenti non vengono sostituiti.");
                if(!Confirm("Generare "+algorithm+" tramite ssh-keygen?\n\n"+output+"\n\nPer -sk serve un token compatibile; PIN, tocco e passphrase saranno richiesti nella scheda del terminale. La parte segreta hardware resta sul token. L'identita ibrida invece e un file privato: proteggilo con una passphrase."))return null;
                args.AddRange(["-t",algorithm,"-f",output,"-C",S(p,"name","UNSC Relay")]);if(algorithm.EndsWith("-sk")&&p.TryGetProperty("resident",out var resident)&&resident.GetBoolean())args.AddRange(["-O","resident"]);title="Generazione / "+algorithm;
            }else if(kind=="load"){
                await EnsureBundledAgent();exe=NativeClient.Find("ssh-add")??throw new IOException("Agent del pacchetto non disponibile.");var identity=OpenFile("Carica identita nel tuo agent OpenSSH");if(identity==null)return null;args.AddRange(["-t","3600",identity]);title="Agent / aggiungi identita";
            }else throw new ArgumentException("Operazione nativa non valida.");
            if(exe.Contains("Cygwin",StringComparison.OrdinalIgnoreCase)) {for(var index=0;index<args.Count;index++)if(Path.IsPathFullyQualified(args[index]))args[index]=NativeClient.CygwinPath(args[index]);}
            var launch=LocalLaunch.Direct(exe,args);var profile=new ConnectionProfile{Name=title,Protocol="native",Host="",Authentication="auto",Native=new NativeOptions{Target="tool"}};nativeTasks[profile.Id]=profile;nativeTaskLaunches[profile.Id]=launch;if(kind=="generate"){var flagIndex=args.IndexOf("-f");if(flagIndex>=0)generatedNativePaths[profile.Id]=args[flagIndex+1];}return profile;
        }
        if(action=="externalCertificateStatus")return data.CertificateProviders;
        if(action=="externalCertificateSave"){
            var id=S(p,"keyId");if(!vault.IsUnlocked||!vault.List().Any(k=>k.Id==id))throw new InvalidOperationException("Sblocca e seleziona una chiave del deposito.");
            var previous=data.CertificateProviders;
            if(p.TryGetProperty("remove",out var remove)&&remove.GetBoolean())data.CertificateProviders=previous.Where(c=>c.KeyId!=id).ToList();
            else{var provider=p.GetProperty("provider").Deserialize<CertificateProvider>(ProfileStore.Json)!;provider.Validate();if(provider.KeyId!=id)throw new ArgumentException("Chiave CA non corrispondente.");if(!Confirm("Autorizzare questo provider CA per la chiave selezionata?\n\n"+provider.Executable+"\nCA: "+provider.AuthorityFingerprint+"\nPrincipal: "+provider.Principal+"\n\nViene inviata solo la chiave pubblica; i comandi del provider possono avviare autenticazioni locali. Rinnovo automatico: "+(provider.AutoRenew?"attivo, solo con archivio sbloccato e app aperta":"disattivato")))throw new OperationCanceledException();data.CertificateProviders=[..previous.Where(c=>c.KeyId!=id),provider];}
            try{store.Save(data);}catch{data.CertificateProviders=previous;throw;}return Snapshot();
        }
        if(action=="externalCertificateIssue"){
            if(certificateRenewBusy)throw new IOException("Una richiesta di certificato e gia in corso.");var provider=data.CertificateProviders.Single(c=>c.KeyId==S(p,"keyId"));var passphrase=Secret("Passphrase chiave utente","Vuoto se assente. La chiave privata rimane nel deposito.");certificateRenewBusy=true;
            try{await ExternalCertificates.Issue(vault,provider,Path.Combine(dataPath,"CertificateRequests"),passphrase);return Snapshot();}finally{certificateRenewBusy=false;}
        }
        throw new ArgumentException("Operazione avanzata non valida.");
    }
    async Task FinishNativeTask(string profileId,int code)
    {
        if(code==0&&generatedNativePaths.Remove(profileId,out var output)){
            var chmod=Path.Combine(Path.GetDirectoryName(NativeClient.Find("ssh")!)!,"chmod.exe");
            try{await NativeClient.Capture(chmod,["600",output]);Emit("notice",new{text="Identita creata. Permessi del riferimento/chiave limitati al proprietario; file pubblico accanto all'identita."});}
            catch(IOException){Emit("notice",new{text="Identita creata: controlla i permessi del file prima di usarla. La verifica automatica non e riuscita."});}
        }
        nativeTaskLaunches.Remove(profileId);generatedNativePaths.Remove(profileId);
    }
    async void RenewExternalCertificates()
    {
        if(smokeTest||certificateRenewBusy||updating||!vault.IsUnlocked||DateTime.UtcNow<nextCertificateRenew)return;
        nextCertificateRenew=DateTime.UtcNow.AddMinutes(10);certificateRenewBusy=true;
        try{
            foreach(var provider in data.CertificateProviders.Where(c=>c.AutoRenew).ToArray()){
                if(!vault.IsUnlocked)break;var key=vault.List().FirstOrDefault(k=>k.Id==provider.KeyId);if(key?.Certificate?.ValidUntil is not{} until||until>DateTimeOffset.UtcNow.AddMinutes(10)||CertificateRevocations.IsRevoked(key.Certificate.Text,vault.Revocations()))continue;
                try{await ExternalCertificates.Issue(vault,provider,Path.Combine(dataPath,"CertificateRequests"));Emit("notice",new{text="Certificato rinnovato: "+key.Name});}catch(Exception ex){Emit("notice",new{text="Rinnovo non completato per "+key.Name+": "+(ex is System.Security.Cryptography.CryptographicException?"verifica crittografica non superata":ex.Message)});}
            }
        }finally{certificateRenewBusy=false;}
    }
}
