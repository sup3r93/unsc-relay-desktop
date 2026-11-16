using System.Diagnostics;
namespace UnscRelay.Core;
public sealed record ConnectionHook(string Phase, string Kind, string Command, string[] Arguments, bool Enabled=false, int TimeoutSeconds=15)
{
    public void Validate()
    {
        if(Phase is not("before-connect" or "after-connect" or "before-disconnect" or "after-disconnect")||Kind is not("local" or "remote"))throw new ArgumentException("Evento hook non valido.");
        if(Kind=="remote"&&Phase is "before-connect" or "after-disconnect")throw new ArgumentException("Un hook remoto richiede una connessione attiva.");
        if(Command==null||Command.Length>4096||Command.Any(char.IsControl)||Enabled&&string.IsNullOrWhiteSpace(Command)||Arguments==null||Arguments.Length>32||Arguments.Any(a=>a==null||a.Length>8192||a.Contains('\0'))||TimeoutSeconds is <1 or >30)throw new ArgumentException("Hook non valido: comando singola riga, massimo 32 argomenti e timeout 1-30 secondi.");
        if(Enabled&&Kind=="local"&&(!Path.IsPathFullyQualified(Command)||!Command.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Hook locale: indica il percorso completo di un file EXE. Per script usa un interprete esplicito con argomenti separati.");
        if(Kind=="remote"&&Arguments.Length>0)throw new ArgumentException("Hook remoto: inserisci il comando completo, senza argomenti locali.");
    }
}
public static class ConnectionHooks
{
    public static async Task RunLocal(ConnectionHook hook,CancellationToken token=default)
    {
        hook.Validate();if(!hook.Enabled||hook.Kind!="local")return;
        if(!File.Exists(hook.Command))throw new FileNotFoundException("EXE dell'hook non trovato.",hook.Command);
        var start=new ProcessStartInfo(hook.Command){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in hook.Arguments)start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new IOException("Avvio hook non riuscito.");
        // Drain bounded buffers without retaining potentially secret script output.
        async Task Drain(StreamReader reader){var buffer=new char[4096];while(await reader.ReadAsync(buffer)>0){} }
        var stdout=Drain(process.StandardOutput);var stderr=Drain(process.StandardError);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(hook.TimeoutSeconds));
        try{await process.WaitForExitAsync(timeout.Token);await Task.WhenAll(stdout,stderr).WaitAsync(timeout.Token);if(process.ExitCode!=0)throw new IOException("Hook locale terminato con codice "+process.ExitCode+".");}
        catch(OperationCanceledException){try{process.Kill(true);}catch(InvalidOperationException){}throw new TimeoutException("Hook locale interrotto per timeout o annullamento.");}
    }
}
public sealed partial class SshSession
{
    public async Task RunRemoteHook(ConnectionHook hook)
    {
        hook.Validate();if(!hook.Enabled||hook.Kind!="remote")return;
        if(!_ssh.IsConnected){if(Profile.Protocol!="sftp")throw new IOException("Sessione SSH non disponibile per l’hook remoto.");await _ssh.ConnectAsync(CancellationToken.None);}
        using var command=_ssh.CreateCommand(hook.Command);command.CommandTimeout=TimeSpan.FromSeconds(hook.TimeoutSeconds);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(hook.TimeoutSeconds));
        async Task Drain(Stream stream){var buffer=new byte[4096];while(await stream.ReadAsync(buffer,timeout.Token)>0){} }
        var execution=command.ExecuteAsync(timeout.Token);await Task.WhenAll(execution,Drain(command.OutputStream),Drain(command.ExtendedOutputStream));
        if(command.ExitStatus!=0)throw new IOException("Hook remoto terminato con codice "+command.ExitStatus+".");
    }
}
