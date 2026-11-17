using System.Diagnostics;
using System.Text;
namespace UnscRelay.Core;
public sealed record NativeOptions
{
    public string Kind {get;set;}="ssh";
    public bool SharedHostKeys {get;set;}=true;
    public string Executable {get;set;}="";
    public string ConfigFile {get;set;}="";
    public string Target {get;set;}="";
    public string IdentityFile {get;set;}="";
    public string Signature {get;set;}="default";
    public string SerialPort {get;set;}="COM1";
    public int Baud {get;set;}=115200;
    public int Port {get;set;}
    public string ArgumentsJson {get;set;}="[]";
    public void Validate()
    {
        if(Kind is not("ssh" or "sftp" or "mosh" or "telnet" or "serial" or "ftp" or "s3" or "rsh" or "xdmcp" or "bash" or "zsh" or "browser" or "file"))throw new ArgumentException("Client nativo non supportato.");
        foreach(var value in new[]{Executable,ConfigFile,Target,IdentityFile,Signature,SerialPort,ArgumentsJson})if(value==null||value.Length>8192||value.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t')))throw new ArgumentException("Opzioni native non valide.");
        foreach(var value in new[]{Executable,ConfigFile,IdentityFile})if(value.Length>0&&(!Path.IsPathFullyQualified(value)||value.Any(char.IsControl)))throw new ArgumentException("Usa percorsi assoluti per programmi, config e chiavi.");
        if(Kind is "ssh" or "sftp" or "mosh" or "telnet" or "rsh") {if(string.IsNullOrWhiteSpace(Target)||Target.StartsWith('-')||Target.Any(char.IsWhiteSpace)||Target.Any(char.IsControl))throw new ArgumentException("Indica un host o alias valido.");}
        if(Kind=="browser"&&(!Uri.TryCreate(Target,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https")||uri.UserInfo.Length>0))throw new ArgumentException("Usa un URL http/https senza credenziali incorporate.");
        if(Kind=="file"&&Target.Length>0&&!Path.IsPathFullyQualified(Target))throw new ArgumentException("Indica una cartella Windows assoluta.");
        if(Signature is not("default" or "ssh-mldsa44-ed25519" or "ssh-ed448"))throw new ArgumentException("Firma nativa non riconosciuta.");
        if(Kind=="serial"&&(!System.Text.RegularExpressions.Regex.IsMatch(SerialPort,@"^COM[1-9][0-9]{0,3}$")||Baud is <300 or >4000000))throw new ArgumentException("Porta seriale o baud non validi.");
        var args=System.Text.Json.JsonSerializer.Deserialize<string[]>(ArgumentsJson)??throw new ArgumentException("Inserisci un elenco JSON di argomenti.");
        if(args.Length>64||args.Any(a=>a==null||a.Length>4096||a.Contains('\0')))throw new ArgumentException("Troppi argomenti o valori non validi.");
        if(Port is <0 or >65535)throw new ArgumentException("Porta OpenSSH non valida.");
        if(Kind is "ssh" or "sftp" && args.Length!=0)throw new ArgumentException("Per SSH/SFTP usa il file config, non argomenti aggiuntivi.");
    }
}
public static class NativeClient
{
    public static string? Find(string name)
    {
        if(!OperatingSystem.IsWindows())return null;
        var tools=Path.Combine(AppContext.BaseDirectory,"Tools");
        var relative=name switch {"ssh"=>"Cygwin/bin/ssh.exe","sftp"=>"Cygwin/bin/sftp.exe","ssh-keygen"=>"Cygwin/bin/ssh-keygen.exe","ssh-add"=>"Cygwin/bin/ssh-add.exe","ssh-agent"=>"Cygwin/bin/ssh-agent.exe","windows-keygen"=>"OpenSSH/ssh-keygen.exe","serial"=>"PuTTY/plink.exe","s3"=>"Rclone/rclone.exe","step"=>"Step/step.exe","mosh"=>"Cygwin/bin/mosh","telnet"=>"Cygwin/bin/telnet.exe","ftp"=>"Cygwin/bin/ftp.exe","rsh"=>"Cygwin/bin/rsh.exe","bash"=>"Cygwin/bin/bash.exe","zsh"=>"Cygwin/bin/zsh-5.8.exe","xdmcp"=>"Cygwin/bin/XWin.exe",_=>""};
        var bundled=Path.Combine(tools,relative.Replace('/',Path.DirectorySeparatorChar));if(relative.Length>0&&File.Exists(bundled))return bundled;
        return null;
    }
    public static string Resolve(NativeOptions options)=>options.Executable.Length>0&&File.Exists(options.Executable)?Path.GetFullPath(options.Executable):options.Executable.Length==0?Find(options.Kind)??throw new IOException("Componente "+options.Kind+" mancante dal pacchetto. Ripara l’installazione di Relay."):throw new IOException("L'eseguibile selezionato non esiste.");
    public static LocalLaunch Launch(ConnectionProfile profile)
    {
        profile.Validate();var o=profile.Native;var args=new List<string>();var executable=Resolve(o);string P(string path)=>executable.Contains("Cygwin",StringComparison.OrdinalIgnoreCase)?CygwinPath(path):path;
        if(o.Kind is "ssh" or "sftp"){
            if(o.Kind=="sftp")args.AddRange(["-S",P(Path.Combine(Path.GetDirectoryName(executable)!,"ssh.exe"))]);
            if(o.ConfigFile.Length>0){if(!File.Exists(o.ConfigFile))throw new IOException("File config non trovato.");args.AddRange(["-F",P(o.ConfigFile)]);}
            if(o.Port>0)args.AddRange(["-o","Port="+o.Port]);
            if(o.IdentityFile.Length>0){if(!File.Exists(o.IdentityFile))throw new IOException("File identita non trovato.");args.AddRange(["-i",P(o.IdentityFile),"-o","IdentitiesOnly=yes"]);}
            if(o.Signature!="default")args.AddRange(["-o","PubkeyAcceptedAlgorithms="+o.Signature]);
            if(profile.RequirePostQuantumKex)args.AddRange(["-o","KexAlgorithms=mlkem768x25519-sha256,sntrup761x25519-sha512,sntrup761x25519-sha512@openssh.com"]);
            args.AddRange(["-o","StrictHostKeyChecking="+(o.SharedHostKeys?"yes":"ask"),"-o","UpdateHostKeys=no"]);args.Add(o.Target);
        }else if(o.Kind=="serial")args.AddRange(["-serial",o.SerialPort,"-sercfg",o.Baud+",8,n,1,N"]);
        else {args.AddRange(System.Text.Json.JsonSerializer.Deserialize<string[]>(o.ArgumentsJson)!);if(o.Target.Length>0){if(o.Kind=="xdmcp")args.Add("-query");args.Add(o.Target);}}
        if(o.Kind=="mosh"){args.Insert(0,CygwinPath(executable));executable=Path.Combine(Path.GetDirectoryName(executable)!,"perl.exe");}
        if(o.Kind is "bash" or "zsh" && args.Count==0)args.Add("-i");
        return LocalLaunch.Direct(executable,args,profile.StartupDirectory.Length>0?profile.StartupDirectory:null);
    }
    public static void AddStartupMacro(LocalLaunch launch,ConnectionProfile profile,string? command)
    {
        if(command==null)return;
        if(profile.Protocol!="native"||profile.Native.Kind!="ssh"||profile.Native.Executable.Length!=0)throw new ArgumentException("Le macro native richiedono OpenSSH incluso.");
        if(command.Length is <1 or >8192||command.Contains('\0'))throw new ArgumentException("Comando macro non valido.");
        // OpenSSH sends this as a remote exec request only after successful authentication.
        // Keep stdin available for the interactive shell; never send the macro to a login prompt.
        launch.Arguments.InsertRange(0,["-tt","-o","RemoteCommand=none"]);
        launch.Arguments.Add(command.Replace("\r\n","\n").Replace('\r','\n')+"\nexec \"${SHELL:-/bin/sh}\" -l");
    }
    public static string CygwinPath(string path) { var full=Path.GetFullPath(path).Replace('\\','/');return full.Length>2&&full[1]==':'?"/cygdrive/"+char.ToLowerInvariant(full[0])+full[2..]:full; }
    public static async Task<string> Capture(string executable,IEnumerable<string> args,int seconds=15,string? inputText=null,IReadOnlyDictionary<string,string>? environment=null)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true};foreach(var arg in args)info.ArgumentList.Add(arg);if(environment!=null)foreach(var pair in environment)info.Environment[pair.Key]=pair.Value;
        using var process=Process.Start(info)??throw new IOException("Avvio del client non riuscito.");var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        if(inputText!=null)await process.StandardInput.WriteAsync(inputText);process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill(true);}catch(InvalidOperationException){}throw new IOException("Il client non risponde entro il timeout.");}
        var text=await output;var err=await error;if(process.ExitCode!=0)throw new IOException("Client terminato con codice "+process.ExitCode+": "+err[..Math.Min(err.Length,1000)]);if(text.Length>1048576)throw new IOException("Risposta troppo grande.");return text;
    }
    public static async Task VerifyCapabilities(ConnectionProfile profile)
    {
        var options=profile.Native;if(options.Kind is not("ssh" or "sftp"))return;
        var ssh=options.Executable.Length==0?Find("ssh"):options.Kind=="sftp"?Path.Combine(Path.GetDirectoryName(options.Executable)!,"ssh.exe"):options.Executable;
        if(options.Signature!="default") {if(ssh==null||!(await Capture(ssh,["-Q","key"])).Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Contains(options.Signature,StringComparer.Ordinal))throw new IOException("Il client selezionato non supporta la firma "+options.Signature+". Nessun fallback eseguito.");}
        if(profile.RequirePostQuantumKex){if(ssh==null)throw new IOException("Client SSH non trovato.");var supported=(await Capture(ssh,["-Q","kex"])).Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries);if(!supported.Any(x=>x.StartsWith("mlkem768x25519-")||x.StartsWith("sntrup761x25519-")))throw new IOException("Client privo di scambio chiavi post-quantistico. Nessun fallback eseguito.");}
    }
}
public sealed record RdpOptions
{
    public string Gateway {get;set;}="";
    public string GatewayUsername {get;set;}="";
    public string GatewayDomain {get;set;}="";
    public bool MultiMonitor {get;set;}
    public bool RedirectDrives {get;set;}
    public bool RedirectPrinters {get;set;}
    public bool RedirectSmartCards {get;set;}
    public bool RedirectPorts {get;set;}
    public int AudioMode {get;set;}=2;
    public void Validate(string jump)
    {
        foreach(var value in new[]{Gateway,GatewayUsername,GatewayDomain})if(value==null||value.Length>256||value.Any(char.IsControl))throw new ArgumentException("Configurazione Gateway non valida.");
        if(Gateway.Length>0&&(Gateway.Any(char.IsWhiteSpace)||Gateway.Contains('/')||Gateway.Contains('\\')||Gateway.StartsWith('-')))throw new ArgumentException("Indica il nome DNS del Gateway, senza URL.");
        if(Gateway.Length>0&&jump.Length>0)throw new ArgumentException("Scegli RD Gateway oppure jump host SSH, non entrambi.");
        if(AudioMode is <0 or >2)throw new ArgumentException("Modalita audio RDP non valida.");
    }
}
