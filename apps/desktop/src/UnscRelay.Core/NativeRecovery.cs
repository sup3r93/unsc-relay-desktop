using System.Text.Json;
namespace UnscRelay.Core;
public static class NativeRecovery
{
    public static bool Supported(ConnectionProfile p)=>p.Protocol=="native"&&p.Native.Kind is "ssh" or "sftp"&&p.Native.Executable.Length==0;
    public static string Snapshot(ConnectionProfile p)=>JsonSerializer.Serialize(p,ProfileStore.Json);
    public static void Validate(ConnectionProfile p,string snapshot,bool unlocked)
    {
        if(!Supported(p)||Snapshot(p)!=snapshot)throw new IOException("Profilo OpenSSH modificato: apri una nuova connessione.");
        if(!unlocked)throw new IOException("Archivio bloccato: sbloccalo prima del recupero.");
        // External config may contain RemoteCommand, Match exec and ProxyCommand.
        // Never silently replay these commands or ask for missing credentials during recovery.
        if(p.Native.ConfigFile.Length>0||!p.Native.SharedHostKeys||p.Native.IdentityFile.Length>0)throw new IOException("Config o identita esterna: riapri manualmente la sessione.");
        if(p.Authentication=="password"&&p.PasswordId.Length>0||p.Authentication=="key"&&p.KeyId.Length>0)return;
        throw new IOException("Il recupero richiede una password o chiave salvata nell'archivio. Riapri manualmente la sessione.");
    }
}
