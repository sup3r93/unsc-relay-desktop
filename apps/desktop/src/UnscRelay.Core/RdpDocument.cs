using System.Text;
namespace UnscRelay.Core;
public static class RdpDocument
{
    public static string Create(ConnectionProfile profile,string endpoint,int port)
    {
        profile.Validate();if(profile.Protocol!="rdp")throw new ArgumentException("Serve un profilo RDP.");var o=profile.Rdp;
        if(endpoint.Any(char.IsControl)||port is <1 or >65535)throw new ArgumentException("Endpoint RDP non valido.");
        var host=endpoint.Contains(':')?"["+endpoint+"]":endpoint;var text=new StringBuilder();void S(string name,string value)=>text.Append(name).Append(":s:").Append(value).Append("\r\n");void I(string name,int value)=>text.Append(name).Append(":i:").Append(value).Append("\r\n");
        S("full address",host+":"+port);S("username",profile.Username);S("domain",profile.Domain);I("authentication level",2);I("enablecredsspsupport",1);I("prompt for credentials",1);I("screen mode id",2);I("use multimon",o.MultiMonitor?1:0);I("autoreconnection enabled",0);
        I("redirectclipboard",profile.ShareClipboard?1:0);I("redirectprinters",o.RedirectPrinters?1:0);I("redirectsmartcards",o.RedirectSmartCards?1:0);I("redirectcomports",o.RedirectPorts?1:0);S("drivestoredirect",o.RedirectDrives?"*":"");I("audiomode",o.AudioMode);
        I("gatewayusagemethod",o.Gateway.Length>0?1:0);I("gatewayprofileusagemethod",1);I("gatewaycredentialssource",0);S("gatewayhostname",o.Gateway);S("gatewayusername",o.GatewayUsername);S("gatewaydomain",o.GatewayDomain);
        return text.ToString();
    }
}
