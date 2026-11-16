using Renci.SshNet.Messages.Transport;
using System.Net.Sockets;
using Renci.SshNet.Common;
namespace UnscRelay.Core;
public static class NetworkReconnect
{
    public static bool IsTransient(Exception error) => error is SocketException or SshOperationTimeoutException || error is SshConnectionException connection && connection.DisconnectReason == DisconnectReason.ConnectionLost;
}
