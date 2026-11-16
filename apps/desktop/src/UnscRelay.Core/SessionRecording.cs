using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace UnscRelay.Core;
public sealed partial class KeyVault
{
    public (string Id, byte[] Key) RecordingKey(string id = "")
    {
        lock (_gate)
        {
            RequireUnlocked(); var prior = _records.SingleOrDefault(k => k.Info.Kind == "recording-key" && (id.Length == 0 || k.Info.Id == id));
            if (prior != null) return (prior.Info.Id, prior.Content.ToArray());
            if (id.Length > 0) throw new InvalidOperationException("Chiave della registrazione non presente in questo archivio.");
            var record = new KeyRecord(new KeyInfo(Guid.NewGuid().ToString("N"), "Registrazioni locali", "", DateTime.UtcNow, Kind: "recording-key"), RandomNumberGenerator.GetBytes(32));
            _records.Add(record); try { Save(); return (record.Info.Id, record.Content.ToArray()); }
            catch { _records.Remove(record); CryptographicOperations.ZeroMemory(record.Content); throw; }
        }
    }
}
public sealed record RecordingFrame(long Milliseconds, string Data);
public sealed record RecordingReplay(string Title, RecordingFrame[] Frames, long Duration);
public sealed class SessionRecording : IDisposable
{
    public const int MaxOutputBytes = 32 * 1024 * 1024;
    readonly FileStream stream; readonly byte[] header; readonly AesGcm cipher; readonly Stopwatch watch = Stopwatch.StartNew();
    readonly object gate = new(); uint sequence; int outputBytes; bool disposed;
    public string FilePath { get; private set; } = "";
    public SessionRecording(string path, string keyId, byte[] masterKey, string title)
    {
        FilePath=Path.GetFullPath(path);
        if(title.Length>4096)throw new ArgumentException("Titolo registrazione troppo lungo.");
        header = new byte[48]; "UREC0001"u8.CopyTo(header); Guid.Parse(keyId).TryWriteBytes(header.AsSpan(8)); RandomNumberGenerator.Fill(header.AsSpan(24));
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, header.AsSpan(24,16).ToArray(), "UNSC Relay recording v1"u8.ToArray());
        try { cipher = new(key,16); } finally { CryptographicOperations.ZeroMemory(key); }
        stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        try { stream.Write(header); Frame(2, Encoding.UTF8.GetBytes(title)); stream.Flush(true); }
        catch { stream.Dispose(); cipher.Dispose(); throw; }
    }
    public void Write(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if (outputBytes + bytes.Length > MaxOutputBytes || sequence > 99990) throw new IOException("Limite registrazione raggiunto (32 MiB / 100000 frammenti). Registrazione arrestata.");
            while (bytes.Length > 0) { int size=Math.Min(bytes.Length,65536); Frame(1,bytes[..size]); outputBytes+=size; bytes=bytes[size..]; }
            stream.Flush();
        }
    }
    void Frame(byte type, ReadOnlySpan<byte> bytes)
    {
        Span<byte> frame=stackalloc byte[17];frame[0]=type;BinaryPrimitives.WriteUInt32LittleEndian(frame[1..],sequence++);BinaryPrimitives.WriteInt64LittleEndian(frame[5..],watch.ElapsedMilliseconds);BinaryPrimitives.WriteInt32LittleEndian(frame[13..],bytes.Length);
        var aad=new byte[65];header.CopyTo(aad,0);frame.CopyTo(aad.AsSpan(48));Span<byte> nonce=stackalloc byte[12];header.AsSpan(40,8).CopyTo(nonce);frame.Slice(1,4).CopyTo(nonce[8..]);
        var encrypted=new byte[bytes.Length];Span<byte>tag=stackalloc byte[16];cipher.Encrypt(nonce,bytes,encrypted,tag,aad);stream.Write(frame);stream.Write(encrypted);stream.Write(tag);
    }
    public void Dispose()
    {
        lock(gate){if(disposed)return;disposed=true;try{Frame(0,[]);stream.Flush(true);}finally{stream.Dispose();cipher.Dispose();}}
    }
    public static string KeyId(string path)
    {
        using var input=File.OpenRead(path);Span<byte> head=stackalloc byte[48];input.ReadExactly(head);if(!head[..8].SequenceEqual("UREC0001"u8))throw new IOException("Formato registrazione non valido.");return new Guid(head.Slice(8,16)).ToString("N");
    }
    public static RecordingReplay Read(string path,byte[] masterKey)
    {
        using var input=File.OpenRead(path);if(input.Length>48*1024*1024)throw new IOException("Registrazione troppo grande.");
        var head=new byte[48];input.ReadExactly(head);if(!head.AsSpan(0,8).SequenceEqual("UREC0001"u8))throw new IOException("Formato registrazione non valido.");
        var key=HKDF.DeriveKey(HashAlgorithmName.SHA256,masterKey,32,head.AsSpan(24,16).ToArray(),"UNSC Relay recording v1"u8.ToArray());
        try
        {
            using var aes=new AesGcm(key,16);uint expected=0;long elapsed=0;int total=0;string title="";var frames=new List<RecordingFrame>();
            while(input.Position<input.Length)
            {
                if(expected>100000)throw new IOException("Troppi frammenti.");
                var frame=new byte[17];input.ReadExactly(frame);var sequence=BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1));var at=BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(5));int length=BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(13));
                if(sequence!=expected++||at<elapsed||length is <0 or >65536)throw new IOException("Sequenza registrazione non valida.");elapsed=at;
                var encrypted=new byte[length];input.ReadExactly(encrypted);var tag=new byte[16];input.ReadExactly(tag);var plain=new byte[length];var nonce=new byte[12];head.AsSpan(40,8).CopyTo(nonce);frame.AsSpan(1,4).CopyTo(nonce.AsSpan(8));var aad=new byte[65];head.CopyTo(aad,0);frame.CopyTo(aad,48);
                aes.Decrypt(nonce,encrypted,tag,plain,aad);
                try
                {
                    if(frame[0]==0){if(length!=0||input.Position!=input.Length||sequence==0)throw new IOException("Fine registrazione non valida.");return new(title,frames.ToArray(),elapsed);}
                    if(sequence==0&&frame[0]==2)title=new UTF8Encoding(false,true).GetString(plain);
                    else if(frame[0]==1&&sequence>0){total+=length;if(total>MaxOutputBytes)throw new IOException("Registrazione troppo grande.");frames.Add(new(at,Convert.ToBase64String(plain)));}
                    else throw new IOException("Tipo frammento non valido.");
                }
                finally{CryptographicOperations.ZeroMemory(plain);}
            }
            throw new IOException("Registrazione incompleta: manca la chiusura autenticata.");
        }
        finally{CryptographicOperations.ZeroMemory(key);}
    }
}
