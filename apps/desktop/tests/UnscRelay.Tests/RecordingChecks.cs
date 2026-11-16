using System.Security.Cryptography;
using System.Text;
using UnscRelay.Core;
static class RecordingChecks
{
    public static int Run()
    {
        int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Reject(Action f,string name){try{f();}catch(Exception e)when(e is IOException or CryptographicException or InvalidOperationException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
        var folder=Path.Combine(Path.GetTempPath(),"relay-recording-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        using var vault=new KeyVault(Path.Combine(folder,"keys.vault"));const string master="Recording fixture master!";vault.Unlock(master);var key=vault.RecordingKey();
        try
        {
            Check(vault.List().Count==0&&vault.Passwords().Count==0,"Recording key hidden from SSH keys and saved passwords");
            var again=vault.RecordingKey();Check(again.Id==key.Id&&again.Key.SequenceEqual(key.Key),"Recording key reused without changing vault identity");CryptographicOperations.ZeroMemory(again.Key);
            var path=Path.Combine(folder,"fixture.urec");var title="Fixture title secret";var content=Encoding.UTF8.GetBytes("TOP_SECRET_OUTPUT_0123456789\r\n\x1b[31mfixture\x1b[0m");
            using(var recorder=new SessionRecording(path,key.Id,key.Key,title)){recorder.Write(content);recorder.Write(Encoding.UTF8.GetBytes("second frame\r\n"));}
            var bytes=File.ReadAllBytes(path);var stored=Encoding.UTF8.GetString(bytes);Check(!stored.Contains(title)&&!stored.Contains("TOP_SECRET_OUTPUT"),"Metadata and terminal output encrypted on disk");
            Check(SessionRecording.KeyId(path)==key.Id,"Header references recording key without key material");
            var replay=SessionRecording.Read(path,key.Key);Check(replay.Title==title&&replay.Frames.Length==2&&Convert.FromBase64String(replay.Frames[0].Data).SequenceEqual(content),"Replay preserves output bytes including terminal control sequences");
            Check(replay.Frames[0].Milliseconds<=replay.Frames[1].Milliseconds&&replay.Duration>=replay.Frames[1].Milliseconds,"Replay timing monotonic and authenticated");
            var wrong=RandomNumberGenerator.GetBytes(32);Reject(()=>SessionRecording.Read(path,wrong),"Wrong recording key rejected");CryptographicOperations.ZeroMemory(wrong);
            var tampered=bytes.ToArray();tampered[80]^=1;File.WriteAllBytes(Path.Combine(folder,"tampered"),tampered);Reject(()=>SessionRecording.Read(Path.Combine(folder,"tampered"),key.Key),"Modified ciphertext rejected");
            File.WriteAllBytes(Path.Combine(folder,"truncated"),bytes[..^1]);Reject(()=>SessionRecording.Read(Path.Combine(folder,"truncated"),key.Key),"Truncated authenticated footer rejected");
            File.WriteAllBytes(Path.Combine(folder,"nofooter"),bytes[..^33]);Reject(()=>SessionRecording.Read(Path.Combine(folder,"nofooter"),key.Key),"Missing footer rejected rather than replayed as complete");
            File.WriteAllBytes(Path.Combine(folder,"trailing"),[..bytes,1]);Reject(()=>SessionRecording.Read(Path.Combine(folder,"trailing"),key.Key),"Trailing unauthenticated data rejected");
            var alteredHeader=bytes.ToArray();alteredHeader[30]^=1;File.WriteAllBytes(Path.Combine(folder,"header"),alteredHeader);Reject(()=>SessionRecording.Read(Path.Combine(folder,"header"),key.Key),"Header and derived-key substitution rejected");
            vault.Lock();Reject(()=>vault.RecordingKey(key.Id),"Locked vault cannot release recording key");vault.Unlock(master);var reopened=vault.RecordingKey(key.Id);Check(reopened.Key.SequenceEqual(key.Key),"Recording key survives encrypted vault reopen");CryptographicOperations.ZeroMemory(reopened.Key);
            Reject(()=>vault.RecordingKey(Guid.NewGuid().ToString("N")),"Unknown recording key not silently recreated");
            Console.WriteLine("Recording checks: "+count);return 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.Key);vault.Dispose();
            if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(folder).StartsWith("relay-recording-fixture-",StringComparison.Ordinal))Directory.Delete(folder,true);
        }
    }
}
