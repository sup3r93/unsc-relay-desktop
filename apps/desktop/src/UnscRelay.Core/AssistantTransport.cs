using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
namespace UnscRelay.Core;
public static class AssistantTransport
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static string PipeName => "UNSC-Relay-Assistant-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WindowsIdentity.GetCurrent().User!.Value+"/"+System.Diagnostics.Process.GetCurrentProcess().SessionId)))[..24];
    public static async Task<byte[]> Read(Stream stream,CancellationToken token)
    {
        var prefix=new byte[4];await stream.ReadExactlyAsync(prefix,token);var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if(length<2||length>4*1024*1024)throw new InvalidDataException("Messaggio API troppo grande.");
        var bytes=new byte[length];await stream.ReadExactlyAsync(bytes,token);return bytes;
    }
    public static async Task Write(Stream stream,object value,CancellationToken token)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,ProfileStore.Json);if(bytes.Length>4*1024*1024)throw new InvalidDataException("Risposta API troppo grande.");
        var prefix=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(prefix,bytes.Length);await stream.WriteAsync(prefix,token);await stream.WriteAsync(bytes,token);await stream.FlushAsync(token);
    }
}
