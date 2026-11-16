namespace UnscRelay.Core;

public static class NativePolicy
{
    public static string Configuration(string helper, string original) { string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; var command = "\"" + NativeClient.CygwinPath(helper) + "\" --native-bridge host %I %h %p %H %t %K"; var text = "Host *\n KnownHostsCommand " + command + "\n StrictHostKeyChecking yes\n UserKnownHostsFile none\n GlobalKnownHostsFile none\n UpdateHostKeys no\n CheckHostIP no\n"; if (original.Length > 0) text += "Include " + Q(NativeClient.CygwinPath(original)) + "\n"; return text; }
}
