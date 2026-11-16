using Renci.SshNet;
using Renci.SshNet.Security;

namespace UnscRelay.Core;
internal sealed class ModernKeySource(IPrivateKeySource source, bool certificateOnly) : IPrivateKeySource
{
    public IReadOnlyCollection<HostAlgorithm> HostKeyAlgorithms => source.HostKeyAlgorithms
        .Where(a => a.Name is not ("ssh-rsa" or "ssh-dss" or "ssh-rsa-cert-v01@openssh.com") && (!certificateOnly || a.Name.Contains("-cert-", StringComparison.Ordinal))).ToArray();
}
