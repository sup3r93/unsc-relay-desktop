using Renci.SshNet;
using SshNet.Agent;
using System.Security.Cryptography;

namespace UnscRelay.Core;
public sealed record AgentKeyInfo(string Comment, string Algorithm, string Fingerprint, bool Hardware, bool Certificate);
public static class AgentKeys
{
    public static SshAgent Open(string kind) => kind switch
    {
        "openssh-agent" => new SshAgent(TimeSpan.FromSeconds(10)),
        "pageant" => new Pageant(TimeSpan.FromSeconds(10)),
        _ => throw new ArgumentException("Seleziona un agent compatibile.")
    };
    public static string Fingerprint(IPrivateKeySource key) => "SHA256:" + Convert.ToBase64String(SHA256.HashData(key.HostKeyAlgorithms.First().Data)).TrimEnd('=');
    public static AgentKeyInfo[] List(string kind)
    {
        var agent = Open(kind);
        return agent.RequestIdentities().Select(key => new AgentKeyInfo(key.Comment, key.HostKeyAlgorithms.First().Name, Fingerprint(key),
            key.HostKeyAlgorithms.Any(a => a.Name.StartsWith("sk-", StringComparison.Ordinal)), key.HostKeyAlgorithms.Any(a => a.Name.Contains("-cert-", StringComparison.Ordinal)))).ToArray();
    }
    public static void LoadSmartcard(string kind, string provider, string pin)
    {
        if (!File.Exists(provider) || !Path.GetExtension(provider).Equals(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Seleziona il modulo PKCS#11 del produttore (.dll).");
        Open(kind).AddSmartcardIdentity(Path.GetFullPath(provider), pin, TimeSpan.FromHours(1), confirm: true);
    }
}
