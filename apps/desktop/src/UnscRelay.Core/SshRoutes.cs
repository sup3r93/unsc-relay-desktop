namespace UnscRelay.Core;

/// <summary>Composes SSH.NET transports and direct-tcpip forwards. No custom SSH protocol implementation.</summary>
public static class SshRoutes
{
    public static async Task<SshForward> ForwardAsync(ConnectionProfile destination, IReadOnlyCollection<ConnectionProfile> profiles, Func<ConnectionProfile, string?, int, Task<SshSession>> create)
    {
        var jump = profiles.SingleOrDefault(p => p.Id == destination.JumpHost) ?? throw new ArgumentException("Seleziona un profilo jump host valido.");
        var transport = await CreateAsync(jump, profiles, create);
        try { await transport.ConnectTransportAsync(); return new SshForward(transport.ForwardTo(destination.Host, destination.Port), transport); }
        catch { transport.Dispose(); throw; }
    }
    public static IReadOnlyList<ConnectionProfile> Resolve(ConnectionProfile target, IReadOnlyCollection<ConnectionProfile> profiles)
    {
        var route = new List<ConnectionProfile>(); var seen = new HashSet<string>(StringComparer.Ordinal); var current = target;
        while (true)
        {
            current.Validate();
            if (!seen.Add(current.Id)) throw new ArgumentException("La catena jump host contiene un ciclo.");
            if (current.Protocol is not ("ssh" or "sftp")) throw new ArgumentException("I jump host richiedono profili SSH/SFTP.");
            route.Add(current);
            if (route.Count > 4) throw new ArgumentException("Sono supportati al massimo tre jump host per connessione.");
            if (string.IsNullOrEmpty(current.JumpHost)) break;
            current = profiles.SingleOrDefault(p => p.Id == current.JumpHost) ?? throw new ArgumentException("Il profilo jump host non è più disponibile. Modifica la connessione.");
        }
        route.Reverse(); return route;
    }
    public static async Task<SshSession> CreateAsync(ConnectionProfile target, IReadOnlyCollection<ConnectionProfile> profiles,
        Func<ConnectionProfile, string?, int, Task<SshSession>> create)
    {
        var route = Resolve(target, profiles); SshSession? transport = null;
        try
        {
            foreach (var profile in route)
            {
                var port = transport?.ForwardTo(profile.Host, profile.Port) ?? 0;
                var next = await create(profile, transport == null ? null : "127.0.0.1", port);
                next.OwnRoute(transport); transport = next;
                if (profile == route[^1]) { var result = transport; transport = null; return result; }
                await next.ConnectTransportAsync();
            }
            throw new InvalidOperationException("Percorso SSH vuoto.");
        }
        finally { transport?.Dispose(); }
    }
}
public sealed class SshForward(int port, IDisposable owner) : IDisposable
{
    public int Port { get; } = port;
    public void Dispose() => owner.Dispose();
}
