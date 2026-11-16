namespace UnscRelay.Core;

/// <summary>Owns a desktop endpoint and its optional authenticated SSH transport.</summary>
public sealed class DesktopRoute(string host, int port, IDisposable? owner = null) : IDisposable
{
    public string Host { get; } = host;
    public int Port { get; } = port;
    public void Dispose() => owner?.Dispose();
}
public static class DesktopRoutes
{
    public static void Validate(ConnectionProfile destination, IReadOnlyCollection<ConnectionProfile> profiles)
    {
        destination.Validate();
        if (destination.Protocol is not ("rdp" or "vnc")) throw new ArgumentException("Il percorso desktop richiede RDP o VNC.");
        if (string.IsNullOrEmpty(destination.JumpHost)) return;
        if (destination.JumpHost == destination.Id) throw new ArgumentException("La connessione non puo usare se stessa come jump host.");
        var jump = profiles.SingleOrDefault(p => p.Id == destination.JumpHost) ?? throw new ArgumentException("Il profilo jump host non e piu disponibile.");
        if (SshRoutes.Resolve(jump, profiles).Count > 3) throw new ArgumentException("Sono supportati al massimo tre jump host per connessione.");
    }
    public static async Task<DesktopRoute> OpenAsync(ConnectionProfile destination, IReadOnlyCollection<ConnectionProfile> profiles, Func<ConnectionProfile, string?, int, Task<SshSession>> create)
    {
        Validate(destination, profiles);
        if (string.IsNullOrEmpty(destination.JumpHost)) return new(destination.Host, destination.Port);
        var forward = await SshRoutes.ForwardAsync(destination, profiles, create);
        return new("127.0.0.1", forward.Port, forward);
    }
}
