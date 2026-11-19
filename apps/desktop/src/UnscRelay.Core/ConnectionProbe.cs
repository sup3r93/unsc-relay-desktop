using System.Net;
using System.Net.Sockets;
namespace UnscRelay.Core;
public sealed record ConnectionProbeStep(string Stage,string Status,string Detail);
public sealed record ConnectionProbeResult(DateTimeOffset CheckedAt,ConnectionProbeStep[] Steps);
public static class ConnectionProbe
{
 public static async Task<ConnectionProbeResult> Run(ConnectionProfile profile,CancellationToken cancellation)
 {
  profile.Validate();var now=DateTimeOffset.UtcNow;var steps=new List<ConnectionProbeStep>();
  if(profile.Protocol is "local" or "native"||profile.JumpHost.Length>0||profile.Protocol=="rdp"&&profile.Rdp.Gateway.Length>0)return new(now,[new("route","skipped","La verifica diretta è disponibile per collegamenti senza jump host, gateway o client esterno. Nessuna connessione è stata avviata.")]);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(8));IPAddress[] addresses;
  try{
  try{addresses=await Dns.GetHostAddressesAsync(profile.Host,timeout.Token);addresses=addresses.Where(ip=>ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6).Distinct().Take(4).ToArray();if(addresses.Length==0)throw new SocketException((int)SocketError.HostNotFound);steps.Add(new("dns","passed",string.Join(", ",addresses.Select(ip=>ip.ToString()))));}
  catch(SocketException){return new(now,[new("dns","failed","Impossibile risolvere l’indirizzo. Controlla nome host, DNS e connessione di rete.")]);}
  bool connected=false;
  foreach(var address in addresses){cancellation.ThrowIfCancellationRequested();using var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(new IPEndPoint(address,profile.Port),timeout.Token);connected=true;break;}catch(SocketException){} }
  steps.Add(new("tcp",connected?"passed":"failed",connected?"La porta TCP risponde. Il servizio e la sua identità non sono ancora verificati.":"La porta non risponde. Controlla porta, firewall e disponibilità del servizio."));
  steps.Add(new("authentication","not-tested","La verifica non invia credenziali e non esegue macro o comandi. L’autenticazione verrà verificata quando scegli Connetti."));steps.Add(new("host-identity","not-tested","L’identità del server va verificata nella connessione protetta prima di inviare credenziali. Una porta aperta non prova l’identità del server."));return new(now,steps.ToArray());
  }catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){steps.Add(new("timeout","failed","La verifica ha superato il limite di 8 secondi. Controlla la rete e riprova."));return new(DateTimeOffset.UtcNow,steps.ToArray());}
 }
}
