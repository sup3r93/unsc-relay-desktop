using Renci.SshNet;
namespace UnscRelay.Core;
// Keep supported AES modes and ChaCha20; never negotiate retired 64-bit-block TDEA or future unknown ciphers.
public static class SshTransportPolicy
{
 static readonly HashSet<string> Ciphers=new(StringComparer.Ordinal){"aes128-ctr","aes192-ctr","aes256-ctr","aes128-gcm@openssh.com","aes256-gcm@openssh.com","chacha20-poly1305@openssh.com","aes128-cbc","aes192-cbc","aes256-cbc"};
 internal static void Apply(ConnectionInfo connection){foreach(var name in connection.Encryptions.Keys.Where(name=>!Ciphers.Contains(name)).ToArray())connection.Encryptions.Remove(name);if(connection.Encryptions.Count==0)throw new NotSupportedException("Il motore SSH non offre un cifrario consentito.");}
}
