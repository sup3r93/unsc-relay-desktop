using System.Diagnostics;
namespace UnscRelay.Core;

// Proxy mux opens channels on the already authenticated master without SCM_RIGHTS.
// Unlike ordinary multiplexing it works with the bundled Cygwin OpenSSH on Windows.
public sealed class NativeControlSession : IAsyncDisposable
{
    readonly string ssh, directory;
    readonly CancellationTokenSource lifetime = new();
    readonly Dictionary<string,string> environment = new(StringComparer.OrdinalIgnoreCase);
    public string SocketPath { get; }
    NativeControlSession(string ssh, string directory) { this.ssh=ssh; this.directory=directory; SocketPath=directory+"/s"; }
    public static async Task<NativeControlSession> Create(string ssh)
    {
        ssh=Path.GetFullPath(ssh);
        if(!File.Exists(ssh))throw new IOException("OpenSSH incluso non disponibile.");
        var directory="/tmp/relay-"+Guid.NewGuid().ToString("N");
        // No reuse of an existing directory; other users must not reach the master socket.
        await NativeClient.Capture(Path.Combine(Path.GetDirectoryName(ssh)!,"mkdir.exe"),["-m","700","--",directory]);
        return new NativeControlSession(ssh,directory);
    }
    public void Configure(LocalLaunch launch)
    {
        launch.Arguments.InsertRange(0,["-o","ControlMaster=yes","-o","ControlPath="+SocketPath,"-o","ControlPersist=no"]);
        foreach(var pair in launch.EnvironmentVariables)environment[pair.Key]=pair.Value;
    }
    public string[] ProxyArguments => ["-F","/dev/null","-S",SocketPath,"-O","proxy","-o","BatchMode=yes","-o","RemoteCommand=none","-o","ClearAllForwardings=yes","-o","ForwardAgent=no","-o","ForwardX11=no","relay-channel"];
    public Dictionary<string,string> SftpEnvironment(string serverCommand="")
    {
        if(serverCommand.Length>4096||serverCommand.Any(char.IsControl))throw new ArgumentException("Subsystem SFTP non valido.");
        string Quote(string value)=>"\""+value.Replace('\\','/').Replace("\"","\\\"")+"\"";
        var result=new Dictionary<string,string>(environment,StringComparer.OrdinalIgnoreCase);
        result["RCLONE_CONFIG"]="NUL";result["RCLONE_CONFIG_RELAY_TYPE"]="sftp";
        result["RCLONE_CONFIG_RELAY_SSH"]=string.Join(" ",new[]{ssh}.Concat(ProxyArguments).Select(Quote));
        result["RCLONE_CONFIG_RELAY_DISABLE_HASHCHECK"]="true";
        result["RCLONE_CONFIG_RELAY_SHELL_TYPE"]="none";
        if(serverCommand.Length>0)result["RCLONE_CONFIG_RELAY_SERVER_COMMAND"]=serverCommand;
        return result;
    }
    ProcessStartInfo StartInfo(IEnumerable<string> arguments)
    {
        var start=new ProcessStartInfo(ssh){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in arguments)start.ArgumentList.Add(arg);
        foreach(var pair in environment)start.Environment[pair.Key]=pair.Value;
        return start;
    }
    public async Task<Renci.SshNet.SftpClient> OpenSftp(string serverCommand="",CancellationToken token=default)
    {
        if(serverCommand.Length>4096||serverCommand.Any(char.IsControl))throw new ArgumentException("Subsystem SFTP non valido.");
        ObjectDisposedException.ThrowIf(lifetime.IsCancellationRequested,this);
        using var opening=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
        var args=ProxyArguments.ToList();
        if(serverCommand.Length==0)args.Insert(args.Count-1,"-s");
        args.Add(serverCommand.Length==0?"sftp":serverCommand);
        var process=Process.Start(StartInfo(args))??throw new IOException("Canale SFTP non disponibile.");
        var closed=0;
        void Close(){if(Interlocked.Exchange(ref closed,1)!=0)return;try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}finally{process.Dispose();}}
        var ownership=lifetime.Token.Register(Close);
        using var cancelled=opening.Token.Register(Close);
        async Task Drain(){try{var bytes=new byte[4096];while(await process.StandardError.BaseStream.ReadAsync(bytes)>0){}}catch(Exception ex)when(ex is IOException or ObjectDisposedException or InvalidOperationException){}}
        _=Drain();
        try
        {
            return await Task.Run(()=>Renci.SshNet.SftpClient.FromSubsystemStreams(process.StandardOutput.BaseStream,process.StandardInput.BaseStream,()=>{ownership.Dispose();Close();},TimeSpan.FromSeconds(45)),opening.Token);
        }
        catch{ownership.Dispose();Close();throw;}
    }
    async Task<int> Run(IEnumerable<string> arguments,int seconds,CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(lifetime.IsCancellationRequested,this);
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,token);stop.CancelAfter(TimeSpan.FromSeconds(seconds));
        using var process=Process.Start(StartInfo(arguments))??throw new IOException("Canale OpenSSH non disponibile.");
        process.StandardInput.Close();
        async Task Drain(Stream stream){var buffer=new byte[4096];while(await stream.ReadAsync(buffer,stop.Token)>0){} }
        var output=Drain(process.StandardOutput.BaseStream);var error=Drain(process.StandardError.BaseStream);
        try { await process.WaitForExitAsync(stop.Token);await Task.WhenAll(output,error);return process.ExitCode; }
        catch(OperationCanceledException){try{process.Kill(true);}catch(InvalidOperationException){}try{await Task.WhenAll(output,error);}catch(OperationCanceledException){}throw new TimeoutException("Canale OpenSSH interrotto per timeout o annullamento.");}
    }
    public async Task WaitReady(CancellationToken token=default)
    {
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,token);limit.CancelAfter(TimeSpan.FromSeconds(10));
        try { while(await Run(["-F","/dev/null","-S",SocketPath,"-O","check","relay-channel"],2,limit.Token)!=0)await Task.Delay(100,limit.Token); }
        catch(OperationCanceledException){throw new TimeoutException("Connessione OpenSSH non disponibile dopo l'autenticazione.");}
    }
    public async Task<string> ResolveDirectory(string initialDirectory)
    {
        if(initialDirectory.Length>4096||initialDirectory.Any(char.IsControl))throw new ArgumentException("Cartella remota non valida.");
        var quoted="'"+(initialDirectory.Length==0?".":initialDirectory).Replace("'","'\\''")+"'";
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);stop.CancelAfter(TimeSpan.FromSeconds(10));
        using var process=Process.Start(StartInfo(ProxyArguments.Concat(["cd -- "+quoted+" && pwd -P"])))??throw new IOException("Canale cartella non disponibile.");
        process.StandardInput.Close();
        async Task<byte[]> Read(Stream stream){using var data=new MemoryStream();var bytes=new byte[1024];int n;while((n=await stream.ReadAsync(bytes,stop.Token))>0){if(data.Length+n>8192)throw new IOException("Risposta cartella troppo grande.");data.Write(bytes,0,n);}return data.ToArray();}
        var output=Read(process.StandardOutput.BaseStream);var error=Read(process.StandardError.BaseStream);
        try
        {
            await Task.WhenAll(output,error).WaitAsync(stop.Token);await process.WaitForExitAsync(stop.Token);
            var path=System.Text.Encoding.UTF8.GetString(await output).TrimEnd('\r','\n');
            if(process.ExitCode!=0||path.Length is <1 or >4096||!path.StartsWith('/')||path.Any(char.IsControl))throw new IOException("La shell non comunica una cartella assoluta valida.");
            return path;
        }
        catch{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}throw;}
    }
    public async Task RunRemoteHook(ConnectionHook hook,CancellationToken cancellation=default)
    {
        hook.Validate();if(!hook.Enabled||hook.Kind!="remote")return;
        // -O proxy fails if the owned socket is gone: no new transport or authentication.
        var code=await Run(ProxyArguments.Concat([hook.Command]),hook.TimeoutSeconds,cancellation);
        if(code!=0)throw new IOException("Hook remoto OpenSSH terminato con codice "+code+".");
    }
    public async ValueTask DisposeAsync()
    {
        if(lifetime.IsCancellationRequested)return;
        lifetime.Cancel();environment.Clear();
        // The visible master owns its socket. Never terminate an unrelated SSH process.
        try{await NativeClient.Capture(Path.Combine(Path.GetDirectoryName(ssh)!,"rmdir.exe"),["--",directory]);}catch(IOException){}
    }
}
