using System.Diagnostics;
using System.Text;
namespace UnscRelay.Core;
public sealed record LocalShellChoice(string Id,string Name,string? Executable,bool Available);
public static class LocalShell
{
    public static string? Executable(string id)
    {
        if(!OperatingSystem.IsWindows())return null;
        var system=System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
        var path=id switch {"cmd"=>Path.Combine(system,"cmd.exe"),"powershell"=>Path.Combine(system,"WindowsPowerShell","v1.0","powershell.exe"),"wsl"=>Path.Combine(system,"wsl.exe"),"pwsh"=>Path.Combine(AppContext.BaseDirectory,"Tools","PowerShell","pwsh.exe"),_=>""};
        if(File.Exists(path))return path;

        return null;
    }
    public static LocalShellChoice[] Choices()=>new[]{("cmd","Prompt dei comandi / CMD"),("powershell","Windows PowerShell"),("pwsh","PowerShell 7"),("wsl","WSL / Linux")}.Select(x=>{var exe=Executable(x.Item1);return new LocalShellChoice(x.Item1,x.Item2,exe,exe!=null);}).ToArray();
    public static async Task<string[]> Distributions()
    {
        var exe=Executable("wsl");if(exe==null)return [];
        var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.Unicode};info.ArgumentList.Add("--list");info.ArgumentList.Add("--quiet");
        using var process=Process.Start(info)??throw new IOException("Impossibile elencare WSL.");var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill(true);}catch(InvalidOperationException){}throw new IOException("WSL non risponde. Verifica che sia disponibile.");}
        var text=await output;await error;if(process.ExitCode!=0)return [];return text.Replace("\0","").Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Length<=256&&!x.Any(char.IsControl)).Distinct().Take(100).ToArray();
    }
    // Windows argv quoting, not shell escaping. Startup text is intentionally interpreted only by the selected shell.
    public static string Quote(string value){if(value.Length>0&&!value.Any(c=>char.IsWhiteSpace(c)||c=='"'))return value;var result=new StringBuilder("\"");int slashes=0;foreach(var ch in value){if(ch=='\\'){slashes++;continue;}if(ch=='\"'){result.Append('\\',slashes*2+1).Append(ch);slashes=0;continue;}result.Append('\\',slashes).Append(ch);slashes=0;}return result.Append('\\',slashes*2).Append('"').ToString();}
}
public sealed class LocalLaunch:IDisposable
{
    public string Executable {get;private init;}="";public string WorkingDirectory {get;private init;}="";public List<string> Arguments {get;}=[];
    public Dictionary<string,string> EnvironmentVariables {get;}=new(StringComparer.OrdinalIgnoreCase);string? temporaryDirectory;
    public static LocalLaunch Direct(string executable, IEnumerable<string> arguments, string? directory=null) { var launch=new LocalLaunch {Executable=Path.GetFullPath(executable), WorkingDirectory=directory??System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)}; if(!File.Exists(launch.Executable)) throw new IOException("Programma non disponibile: "+launch.Executable); launch.Arguments.AddRange(arguments);
        return launch; }
    public void SetHome(string home) {
        Directory.CreateDirectory(home);
        EnvironmentVariables["RCLONE_CONFIG"]=Path.Combine(home,"rclone.conf");
        EnvironmentVariables["STEPPATH"]=Path.Combine(home,"step");
        if(Executable.Contains("Cygwin",StringComparison.OrdinalIgnoreCase)){EnvironmentVariables["HOME"]=NativeClient.CygwinPath(home);EnvironmentVariables["PATH"]=Path.GetDirectoryName(Executable)+Path.PathSeparator+(System.Environment.GetEnvironmentVariable("PATH")??"");EnvironmentVariables["SSH_AUTH_SOCK"]=NativeClient.CygwinPath(NativeAgent.SocketForHome(home));EnvironmentVariables["TERM"]="xterm-256color";EnvironmentVariables["LANG"]="C.UTF-8";}
    }
    public string CommandLine=>string.Join(" ",new[]{Executable}.Concat(Arguments).Select(LocalShell.Quote));
    public static LocalLaunch Create(ConnectionProfile profile,string scratch)
    {
        profile.Validate();if(profile.Protocol!="local")throw new ArgumentException("Seleziona una sessione locale.");var exe=LocalShell.Executable(profile.LocalShell)??throw new IOException("La shell scelta non e installata.");var folder=profile.StartupDirectory;
        if(profile.LocalShell!="wsl"&&folder.Length>0&&(!Path.IsPathFullyQualified(folder)||!Directory.Exists(folder)))throw new IOException("La cartella iniziale non esiste oppure non e un percorso Windows assoluto.");
        var result=new LocalLaunch{Executable=exe,WorkingDirectory=profile.LocalShell=="wsl"||folder.Length==0?System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile):Path.GetFullPath(folder)};
        try{
            if(profile.LocalShell=="cmd"){
                result.Arguments.AddRange(["/D","/Q"]);
                if(profile.StartupCommands.Length>0){result.temporaryDirectory=Path.Combine(Path.GetFullPath(scratch),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(result.temporaryDirectory);var file=Path.Combine(result.temporaryDirectory,"startup.cmd");File.WriteAllText(file,"@echo off\r\nchcp 65001 >nul\r\n"+profile.StartupCommands.Replace("\r\n","\n").Replace("\n","\r\n")+"\r\n",new UTF8Encoding(false));result.Arguments.Add("/K");result.Arguments.Add(file);}
            }else if(profile.LocalShell=="wsl"){
                if(profile.WslDistribution.Length>0)result.Arguments.AddRange(["--distribution",profile.WslDistribution]);result.Arguments.AddRange(["--cd",folder.Length==0?"~":folder]);
                if(profile.StartupCommands.Length>0)result.Arguments.AddRange(["--exec","/bin/sh","-c",profile.StartupCommands.Replace("\r\n","\n")+"\nexec \"${SHELL:-/bin/sh}\" -i"]);
            }else{result.Arguments.AddRange(["-NoLogo","-NoProfile","-NoExit"]);if(profile.StartupCommands.Length>0)result.Arguments.AddRange(["-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(profile.StartupCommands))]);}
            return result;
        }catch{result.Dispose();throw;}
    }
    public void Dispose(){if(temporaryDirectory!=null){try{File.Delete(Path.Combine(temporaryDirectory,"startup.cmd"));Directory.Delete(temporaryDirectory);}catch(IOException){}catch(UnauthorizedAccessException){}temporaryDirectory=null;}}
}
