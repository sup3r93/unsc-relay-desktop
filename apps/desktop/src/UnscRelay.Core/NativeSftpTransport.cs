using System.Diagnostics;
namespace UnscRelay.Core;

// sftp disables LocalCommand in its child SSH invocation. Keep original SSH and
// binary streams, prepend our explicit authentication-notification permission.
public static class NativeSftpTransport
{
    public static bool IsInvocation(string[] args) =>
        args.Length > 0 && args[0].StartsWith("-",StringComparison.Ordinal) &&
        args[0] != "--native-bridge" && args.Contains("-s",StringComparer.Ordinal) &&
        Environment.GetEnvironmentVariable("RELAY_NATIVE_PIPE")?.StartsWith("RelayNative-",StringComparison.Ordinal) == true &&
        Environment.GetEnvironmentVariable("RELAY_SFTP_TRANSPORT") is { Length: > 0 };
    public static async Task<int> Run(string[] args)
    {
        var executable = Environment.GetEnvironmentVariable("RELAY_SFTP_TRANSPORT");
        if(!IsInvocation(args) || executable == null || !Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            !Path.GetFileName(executable).Equals("ssh.exe",StringComparison.OrdinalIgnoreCase)) return 1;
        var info = new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
        info.Environment.Remove("RELAY_SFTP_TRANSPORT");
        info.ArgumentList.Add("-o");info.ArgumentList.Add("PermitLocalCommand=yes");
        foreach(var arg in args)info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("Avvio trasporto SSH non riuscito.");
        using var stop = new CancellationTokenSource();
        async Task Input(){try{await Console.OpenStandardInput().CopyToAsync(process.StandardInput.BaseStream,stop.Token);process.StandardInput.Close();}catch(Exception ex)when(ex is IOException or OperationCanceledException or ObjectDisposedException){}}
        var input=Input();
        var output=process.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput());
        var error=process.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError());
        try{await process.WaitForExitAsync();await Task.WhenAll(output,error);return process.ExitCode;}
        finally{stop.Cancel();try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){} }
    }
}
