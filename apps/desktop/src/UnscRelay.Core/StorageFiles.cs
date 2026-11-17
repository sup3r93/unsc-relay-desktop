using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace UnscRelay.Core;

public sealed record StorageOptions
{
    public bool Browser {get;set;}
    public bool Tls {get;set;}=true;
    public bool ImplicitTls {get;set;}
    public string Endpoint {get;set;}="";
    public string Region {get;set;}="us-east-1";
    public string Bucket {get;set;}="";
    public string Provider {get;set;}="Other";
    public void Validate(string kind)
    {
        if(!Browser)return;
        if(kind is not("ftp" or "s3"))throw new ArgumentException("Browser disponibile per FTP e S3.");
        foreach(var text in new[]{Endpoint,Region,Bucket,Provider})if(text==null||text.Length>1024||text.Any(char.IsControl))throw new ArgumentException("Opzioni storage non valide.");
        if(kind=="ftp"&&ImplicitTls&&!Tls)throw new ArgumentException("TLS implicito richiede TLS attivo.");
        if(kind=="s3"){
            if(Bucket.Length is <1 or >255||Bucket.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('-' or '.' or '_')))throw new ArgumentException("Indica un bucket S3 valido.");
            if(Provider=="Other"&&string.IsNullOrWhiteSpace(Endpoint))throw new ArgumentException("Indica l’endpoint HTTPS del provider S3 compatibile.");
            if(Provider is not("AWS" or "Other"))throw new ArgumentException("Provider S3 non valido.");
            if(Region.Length>100||Region.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Regione S3 non valida.");
            if(Endpoint.Length>0&&(!Uri.TryCreate(Endpoint,UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/"||uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback)))throw new ArgumentException("Endpoint S3: HTTPS senza credenziali, query o percorsi. HTTP ammesso soltanto su loopback per test locali.");
        }
    }
}
public sealed record StorageEntry(string Name,string Path,bool Directory,long Size,DateTimeOffset? Modified,string Type);
public sealed class StorageFiles : IAsyncDisposable
{
    readonly string executable;readonly Dictionary<string,string> environment;readonly SemaphoreSlim gate=new(1,1);readonly CancellationTokenSource lifetime=new();
    readonly string prefix;CancellationTokenSource? operation;
    public StorageFiles(string executable,ConnectionProfile profile,string secret)
    {
        profile.Validate();if(!profile.Storage.Browser||profile.Native.Kind is not("ftp" or "s3"))throw new ArgumentException("Seleziona un browser storage.");
        if(secret.Length is <1 or >8192||secret.Any(char.IsControl))throw new ArgumentException("Credenziale storage mancante o non valida.");
        this.executable=Path.GetFullPath(executable);if(!File.Exists(this.executable))throw new IOException("Motore storage incluso mancante.");
        environment=new(StringComparer.OrdinalIgnoreCase){{"RCLONE_CONFIG","NUL"},{"RCLONE_CONFIG_RELAY_TYPE",profile.Native.Kind}};
        if(profile.Native.Kind=="s3"){
            environment["RCLONE_CONFIG_RELAY_PROVIDER"]=profile.Storage.Provider;environment["RCLONE_CONFIG_RELAY_ENV_AUTH"]="false";environment["RCLONE_CONFIG_RELAY_ACCESS_KEY_ID"]=profile.Username;environment["RCLONE_CONFIG_RELAY_SECRET_ACCESS_KEY"]=secret;
            environment["RCLONE_CONFIG_RELAY_DIRECTORY_MARKERS"]="true";environment["RCLONE_CONFIG_RELAY_REGION"]=profile.Storage.Region;environment["RCLONE_CONFIG_RELAY_ENDPOINT"]=profile.Storage.Endpoint;environment["RCLONE_CONFIG_RELAY_FORCE_PATH_STYLE"]=profile.Storage.Provider=="Other"?"true":"false";
        }else{
            environment["RCLONE_CONFIG_RELAY_HOST"]=profile.Host;environment["RCLONE_CONFIG_RELAY_PORT"]=profile.Port.ToString();environment["RCLONE_CONFIG_RELAY_USER"]=profile.Username;environment["RELAY_FTP_SECRET"]=secret;
            environment["RCLONE_CONFIG_RELAY_TLS"]=profile.Storage.ImplicitTls?"true":"false";environment["RCLONE_CONFIG_RELAY_EXPLICIT_TLS"]=profile.Storage.Tls&&!profile.Storage.ImplicitTls?"true":"false";
        }
        prefix="relay:"+string.Join('/',new[]{profile.Native.Kind=="s3"?profile.Storage.Bucket:"",Normalize(profile.RemoteDirectory)}.Where(x=>x.Length>0))+"/";
    }
    // File channels use only the authenticated, app-owned OpenSSH master.
    public StorageFiles(string executable,NativeControlSession control,string initialDirectory,string serverCommand="")
    {
        this.executable=Path.GetFullPath(executable);if(!File.Exists(this.executable))throw new IOException("Motore file incluso mancante.");
        environment=control.SftpEnvironment(serverCommand);
        var root=Normalize(initialDirectory);
        prefix="relay:"+(initialDirectory.StartsWith('/')?"/":"")+(root.Length>0?root+"/":"");
    }
    public static string Normalize(string path)
    {
        if(path==".")return "";
        if(path==null||path.Length>4096||path.Any(char.IsControl)||path.Contains('\\')||path.Contains(':'))throw new ArgumentException("Percorso storage non valido.");
        var parts=path.Split('/',StringSplitOptions.RemoveEmptyEntries);if(parts.Any(x=>x is "." or ".."))throw new ArgumentException("Il percorso non puo uscire dalla cartella iniziale.");return string.Join('/',parts);
    }
    string Remote(string path)=>prefix+Normalize(path);
    public void Cancel(){try{operation?.Cancel();}catch(ObjectDisposedException){}}
    async Task<string> Run(string[] arguments,int seconds=120,string? stdin=null,CancellationToken token=default,MemoryStream? binary=null)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var key in info.Environment.Keys.Where(x=>x.StartsWith("RCLONE_",StringComparison.OrdinalIgnoreCase)||x.StartsWith("AWS_",StringComparison.OrdinalIgnoreCase)).ToArray())info.Environment.Remove(key);
        foreach(var pair in environment)if(pair.Key!="RELAY_FTP_SECRET")info.Environment[pair.Key]=pair.Value;
        foreach(var arg in arguments)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new IOException("Motore storage non avviato.");
        async Task<string> Read(StreamReader stream,int limit){var text=new StringBuilder();var buffer=new char[4096];int n;while((n=await stream.ReadAsync(buffer))>0){if(text.Length+n>limit)throw new IOException("Elenco storage troppo grande. Apri una sottocartella.");text.Append(buffer,0,n);}return text.ToString();}
        async Task<string> ReadBinary(){var buffer=new byte[65536];int n;while((n=await process.StandardOutput.BaseStream.ReadAsync(buffer))>0){if(binary!.Length+n>2097152)throw new IOException("Confronto disponibile per file UTF-8 fino a 2 MiB.");binary.Write(buffer,0,n);}return "";}
        var output=binary==null?Read(process.StandardOutput,2097152):ReadBinary();var error=Drain(process.StandardError);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try{if(stdin!=null)await process.StandardInput.WriteAsync(stdin.AsMemory(),timeout.Token);process.StandardInput.Close();var wait=process.WaitForExitAsync(timeout.Token);await Task.WhenAny(wait,output);if(output.IsFaulted)await output;await wait;var text=await output;await error;if(process.ExitCode!=0)throw new IOException("Operazione storage non riuscita (codice "+process.ExitCode+"). Verifica credenziali, autorizzazioni e connessione.");return text;}
        catch{try{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}}catch(InvalidOperationException){}try{await Task.WhenAll(output,error);}catch{}throw;}
    }
    static async Task Drain(StreamReader stream){var chars=new char[4096];while(await stream.ReadAsync(chars)>0){}}
    async Task<T> Execute<T>(Func<CancellationToken,Task<T>> action)
    {
        await gate.WaitAsync(lifetime.Token);try{using var current=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);operation=current;
            if(environment.TryGetValue("RELAY_FTP_SECRET",out var secret)){environment["RCLONE_CONFIG_RELAY_PASS"]=(await Run(["obscure","-"],stdin:secret+"\n",token:current.Token)).Trim();environment.Remove("RELAY_FTP_SECRET");}
            return await action(current.Token);
        }finally{operation=null;gate.Release();}
    }
    string[] Command(string verb,params string[] args)=>[verb,..args,"--retries","1","--low-level-retries","1","--contimeout","15s","--timeout","45s","--ask-password=false","--log-level","ERROR"];
    public Task<StorageEntry[]> List(string path)=>Execute(token=>ListCore(path,token));
    async Task<StorageEntry[]> ListCore(string path,CancellationToken token){
        var canonical=Normalize(path);var text=await Run(Command("lsjson",Remote(path)),token:token);using var json=JsonDocument.Parse(text);if(json.RootElement.ValueKind!=JsonValueKind.Array||json.RootElement.GetArrayLength()>10000)throw new IOException("Troppi elementi: apri una sottocartella.");
        return json.RootElement.EnumerateArray().Select(e=>{var name=e.GetProperty("Name").GetString()??"";if(name.Length==0||name.Contains('/')||Normalize(name)!=name)throw new IOException("Il server ha restituito un nome non valido.");var directory=e.GetProperty("IsDir").GetBoolean();DateTimeOffset? date=DateTimeOffset.TryParse(e.GetProperty("ModTime").GetString(),out var modified)?modified:null;return new StorageEntry(name,(canonical.Length>0?canonical+"/":"")+name,directory,e.GetProperty("Size").GetInt64(),date,directory?"Cartella":System.IO.Path.GetExtension(name).TrimStart('.'));}).OrderByDescending(x=>x.Directory).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public Task<RemoteText> ReadText(string path)=>Execute(async token=>{
        path=Normalize(path);if(path.Length==0)throw new IOException("Seleziona un file da confrontare.");
        var slash=path.LastIndexOf('/');var parent=slash<0?"":path[..slash];
        var entry=(await ListCore(parent,token)).SingleOrDefault(x=>x.Path==path);
        if(entry==null||entry.Directory||entry.Size<0||entry.Size>2097152)throw new IOException("Confronto disponibile per file UTF-8 fino a 2 MiB.");
        using var bytes=new MemoryStream();
        // Count bounds the provider request; the byte reader also rejects a file that grows.
        await Run(Command("cat",Remote(path),"--count","2097153"),token:token,binary:bytes);
        var content=bytes.ToArray();
        if(content.LongLength!=entry.Size)throw new IOException("Il file remoto è cambiato durante la lettura. Riprova.");
        if(content.Contains((byte)0))throw new IOException("Il confronto testuale non supporta file binari.");
        var bom=content.AsSpan().StartsWith(new byte[]{0xef,0xbb,0xbf});
        string text;try{text=new UTF8Encoding(false,true).GetString(content,bom?3:0,content.Length-(bom?3:0));}
        catch(DecoderFallbackException){throw new IOException("Il confronto richiede un file di testo UTF-8 valido.");}
        return new RemoteText(path,text,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)),bom);
    });
    public Task<bool> Transfer(string remote,string local,bool upload,bool replace)=>Execute(async token=>{
        local=Path.GetFullPath(local);if(upload&&!File.Exists(local))throw new IOException("Il file locale non esiste.");
        if(upload){await Run(Command("copyto",local,Remote(remote),replace?"--ignore-times":"--immutable"),900,token:token);}
        else{if(File.Exists(local)&&!replace)throw new IOException("Il file locale esiste gia.");var part=local+".relay-"+Guid.NewGuid().ToString("N")+".part";try{await Run(Command("copyto",Remote(remote),part),900,token:token);File.Move(part,local,replace);}finally{if(File.Exists(part))File.Delete(part);}}
        return true;
    });
    public Task<bool> Change(string verb,string path,string? destination=null)=>Execute(async token=>{
        if(Normalize(path).Length==0)throw new ArgumentException("Non puoi modificare la radice del browser.");
        if(verb is not("mkdir" or "deletefile" or "moveto"))throw new ArgumentException("Operazione storage non valida.");
        if(verb=="moveto"){if(destination==null||Normalize(destination).Length==0)throw new ArgumentException("Nuovo nome mancante.");
            destination=Normalize(destination);
            if(destination==Normalize(path))return true;
            var slash=destination.LastIndexOf('/');var parent=slash<0?"":destination[..slash];
            if((await ListCore(parent,token)).Any(entry=>entry.Path==destination))throw new IOException("La destinazione esiste gia: scegli un altro nome.");
            // Preflight prevents rclone moveto from deleting a source when an identical target exists.
            // FTP/S3 do not provide a universal atomic no-replace rename; server ACLs still apply.
            await Run(Command(verb,Remote(path),Remote(destination),"--immutable"),token:token);}
        else await Run(Command(verb,Remote(path)),token:token);return true;
    });
    public async ValueTask DisposeAsync(){lifetime.Cancel();await gate.WaitAsync();try{environment.Clear();}finally{gate.Release();}}
}
