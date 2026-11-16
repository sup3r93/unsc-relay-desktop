using System.Text;
using UnscRelay.Core;
static class TerminalChecks
{
    public static async Task<int> Run(int port)
    {
        var directory=Path.Combine(Path.GetTempPath(),"relay-terminal-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            var profile=new ConnectionProfile{Name="Terminal fixture",Host="127.0.0.1",Port=port,Username="test",Authentication="password"};
            using var session=new SshSession(profile,"relay-test-password",null,"",new HostKeyStore(Path.Combine(directory,"hosts.json")),_=>true,_=>throw new Exception("Unexpected challenge"));
            var received=new StringBuilder();session.Output+=bytes=>{lock(received)received.Append(Encoding.UTF8.GetString(bytes));};await session.ConnectAsync(120,30);
            var blocks=Enumerable.Range(0,300).Select(i=>"ROW-"+i.ToString("D4")+":"+new string('x',1000)+"\r\n").ToArray();
            await Task.WhenAll(blocks.Select(block=>session.WriteInputAsync(block)));
            var deadline=DateTime.UtcNow.AddSeconds(15);var expected=string.Concat(blocks);while(DateTime.UtcNow<deadline){lock(received)if(received.ToString().Contains(expected))break;await Task.Delay(25);}
            lock(received)if(!received.ToString().Contains(expected))throw new Exception("Output missing or reordered");Console.WriteLine("PASS 300 queued writes preserve output order across 300 KiB");
            var navigation="\x1b[A\x1b[A\x1b[B\x1b[D\x1b[C";await session.WriteInputAsync(navigation);await session.ResizeTerminalAsync(90,25);await session.ResizeTerminalAsync(170,40);
            deadline=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<deadline){lock(received)if(received.ToString().Contains(navigation))break;await Task.Delay(25);}lock(received)if(!received.ToString().Contains(navigation))throw new Exception("History keys lost");Console.WriteLine("PASS Arrow history sequences survive queued writes and resize");
            var shell=(Renci.SshNet.ShellStream)typeof(SshSession).GetField("_shell",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(session)!;if(shell.Length!=0)throw new Exception("SSH read buffer was not consumed");Console.WriteLine("PASS SSH output is consumed without an unbounded second buffer");return 0;
        }finally{var full=Path.GetFullPath(directory);if(full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("relay-terminal-check-",StringComparison.Ordinal))Directory.Delete(full,true);}
    }
}
