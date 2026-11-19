using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly CancellationTokenSource assistantStop=new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> assistantOutput=new();
    void BufferAssistantOutput(string id,byte[] bytes){if(!data.Settings.AssistantApiEnabled)return;assistantOutput.AddOrUpdate(id,_=>System.Text.Encoding.UTF8.GetString(bytes),(_,previous)=>{var text=previous+System.Text.Encoding.UTF8.GetString(bytes);return text.Length>65536?text[^65536..]:text;});}

    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(IntPtr pipe,out uint pid);
    void InitializeAssistantApi(){if(!smokeTest)_=Task.Run(AssistantLoop);Closed+=(_,_)=>assistantStop.Cancel();}
    async Task AssistantLoop()
    {
        while(!assistantStop.IsCancellationRequested){
            try{
                using var pipe=new NamedPipeServerStream(AssistantTransport.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly|PipeOptions.FirstPipeInstance);
                await pipe.WaitForConnectionAsync(assistantStop.Token);
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(assistantStop.Token);timeout.CancelAfter(TimeSpan.FromMinutes(3));
                if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(),out var pid))continue;
                using var process=Process.GetProcessById((int)pid);
                var expected=Path.Combine(AppContext.BaseDirectory,"ChatGPT","UNSC Relay MCP.exe");
                if(!string.Equals(process.MainModule?.FileName,expected,StringComparison.OrdinalIgnoreCase))continue;
                var requestBytes=await AssistantTransport.Read(pipe,timeout.Token);if(requestBytes.Length>AssistantPolicy.MaximumRequestBytes)throw new InvalidDataException("Richiesta troppo grande.");
                using var request=JsonDocument.Parse(requestBytes);var tool=S(request.RootElement,"tool");var args=request.RootElement.GetProperty("arguments").Clone();
                object response;
                try{AssistantPolicy.Validate(tool,args);response=new{result=await Dispatcher.InvokeAsync(()=>ApproveAssistant(tool,args,timeout.Token)).Task.Unwrap()};}
                catch(Exception){response=new{error="Richiesta non autorizzata o operazione non riuscita. Controlla UNSC Relay; nessun accesso permanente è stato concesso."};}
                await AssistantTransport.Write(pipe,response,timeout.Token);
            }catch(OperationCanceledException)when(assistantStop.IsCancellationRequested){break;}
            catch{try{await Task.Delay(500,assistantStop.Token);}catch(OperationCanceledException){break;}}
        }
    }
    void RequireAssistantAccess(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(nativePresentationMode||CriticalUpdateRequired||closing||updating||AppLocked||AccountLocked||startupSequencePlaying||!consoleReady||!data.Settings.AssistantApiEnabled)throw new InvalidOperationException("API non attiva o console bloccata.");
    }
    async Task<object?> ApproveAssistant(string tool,JsonElement args,CancellationToken token)
    {
        var generation=authenticationGeneration;using var operation=CancellationTokenSource.CreateLinkedTokenSource(token,accountOperationStop.Token);token=operation.Token;RequireOperationSession(generation);RequireAssistantAccess(token);if(accessBusy||helloBusy)throw new InvalidOperationException("Verifica già aperta.");
        AssistantPolicy.Validate(tool,args);
        SshSession? target=null;string? binding=null;
        if(args.TryGetProperty("sessionId",out var sid)){target=Session(args);binding=RouteSnapshot(target.Profile);}
        var detail="Un client MCP locale richiede accesso. Il componente verificato è UNSC Relay MCP.exe; il servizio AI che lo usa non è identificabile automaticamente.\n\nAutorizzazione valida SOLO per questa richiesta. Le risposte potranno essere condivise con il servizio AI dal client.\n\nOperazione: "+tool+"\n"+(target==null?"":$"Macchina: {target.Profile.Name} / {target.Profile.Username}@{target.Profile.Host}:{target.Profile.Port}\n")+"\nParametri esatti:\n"+JsonSerializer.Serialize(args,ProfileStore.Json)+"\n\nL’input al terminale viene inviato esattamente come indicato: eventuali invii eseguono comandi. I file remoti possono contenere dati riservati. Nessun accesso a chiavi private, password o token dell’app.\nRifiutando non verranno restituiti dati né eseguite operazioni.";
        if(accessSmokeTest){if(accessTestConsent==null||!accessTestConsent(tool,args))throw new OperationCanceledException();}
        else {Activate();var approval=new AccessApprovalDialog(this,"Richiesta API / ChatGPT o client MCP",detail,AccountHelloAllowed());
        if(approval.ShowDialog()!=true)throw new OperationCanceledException();RequireAssistantAccess(token);
        await VerifyLocalIdentity("Autorizza richiesta API",approval.Method);}
        if(accessSmokeTest){if(accessTestIdentity==null)throw new OperationCanceledException();await accessTestIdentity();}RequireAssistantAccess(token);
        if(target!=null&&(!sessions.TryGetValue(target.Id,out var current)||!ReferenceEquals(current,target)||binding!=RouteSnapshot(current.Profile)))throw new IOException("La sessione è cambiata. Serve una nuova richiesta.");
        RequireOperationSession(generation);
        switch(tool){
            case "relay_status":return new{version=CurrentVersion.ToString(),sessions=sessions.Count,queue=fileJobs.Values.GroupBy(j=>j.Status).ToDictionary(g=>g.Key,g=>g.Count())};
            case "relay_profiles":return data.Profiles.Select(p=>new{p.Id,p.Name,p.Folder,p.Protocol,p.Host,p.Port,p.Username}).ToArray();
            case "relay_sessions":return sessions.Values.Select(s=>new{s.Id,profileId=s.Profile.Id,s.Profile.Name,s.Profile.Host,s.Profile.Port,s.Connected,s.FilesAvailable}).ToArray();
            case "relay_terminal_read":return new{text=assistantOutput.GetValueOrDefault(target!.Id,""),untrustedContent=true};
            case "relay_send_input":await target!.WriteInputAsync(S(args,"text"));return new{sent=true};
            case "relay_disconnect":await Execute("disconnect",args);Emit("assistantDisconnected",new{sessionId=target!.Id});return new{disconnected=true};
            case "relay_sftp_list":return await target!.ListAsync(S(args,"path"));
            case "relay_sftp_read":return await target!.ReadTextAsync(S(args,"path"));
            case "relay_sftp_write":
                if(QueueBusy||EditorsBusy||editBuffers.Values.Any(e=>e.SessionId==target!.Id&&e.Original.Path==S(args,"path")))throw new IOException("File aperto nell’editor o trasferimento attivo.");
                ConfirmProductionOperation(target!.Profile,"Salvataggio del file remoto",1,S(args,"path"));RequireOperationSession(generation);
                var directory=Path.Combine(dataPath,"AssistantScratch");Directory.CreateDirectory(directory);var scratch=Path.Combine(directory,Guid.NewGuid().ToString("N")+".upload");
                try{await File.WriteAllTextAsync(scratch,S(args,"text"),new UTF8Encoding(false,true),token);RequireOperationSession(generation);RequireAssistantAccess(token);var transfer=new TransferRequest(Guid.NewGuid().ToString("N"),scratch,S(args,"path"),true,"replace",ExpectedHash:S(args,"expectedHash"));await target!.TransferAdvancedAsync(transfer,(_,_)=>{},token);return new{saved=true,hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(scratch,token)))};}
                finally{if(File.Exists(scratch))File.Delete(scratch);}
            case "relay_transfer":
                var local=Path.GetFullPath(S(args,"local"));var protectedRoot=Path.GetFullPath(dataPath)+Path.DirectorySeparatorChar;
                if(local.StartsWith(protectedRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("Dati privati dell’app esclusi dall’API.");
                for(var parent=new DirectoryInfo(Path.GetDirectoryName(local)!);parent!=null;parent=parent.Parent)if(parent.Exists&&parent.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("Percorso locale collegato non ammesso.");
                if(File.Exists(local)&&File.GetAttributes(local).HasFlag(FileAttributes.ReparsePoint))throw new IOException("File collegato non ammesso.");
                var payload=JsonSerializer.SerializeToElement(new{sessionId=target!.Id,direction=S(args,"direction"),conflict="ask",items=new[]{new{local,remote=S(args,"remote"),directory=false,conflict="ask"}}},ProfileStore.Json);
                var before=fileJobs.Keys.ToHashSet();EnqueueFiles(payload);return fileJobs.Values.Where(j=>!before.Contains(j.Id)).Select(j=>new{j.Id,j.Status}).ToArray();
            default:throw new ArgumentException("Operazione non consentita.");
        }
    }
}
