namespace UnscRelay.Core;

/// <summary>Certificate-only authentication options for the bundled OpenSSH client.</summary>
public static class NativeCertificates
{
    public static string LoginUser(string effectiveConfiguration)
    {
        if(effectiveConfiguration.Length>1048576)throw new IOException("Configurazione OpenSSH troppo grande.");
        var users=effectiveConfiguration.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)
            .Where(line=>line.StartsWith("user ",StringComparison.OrdinalIgnoreCase)).Select(line=>line[5..].Trim()).ToArray();
        if(users.Length!=1||string.IsNullOrWhiteSpace(users[0])||users[0].Length>256||users[0].Any(char.IsControl))throw new IOException("Impossibile determinare lo username effettivo di OpenSSH per verificare il certificato.");
        // CertificateFile accumulates across config sources: refuse an additional certificate.
        if(effectiveConfiguration.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Any(line=>line.StartsWith("certificatefile ",StringComparison.OrdinalIgnoreCase)&&line[16..].Trim()!="none"))
            throw new IOException("Il config specifica già CertificateFile. Rimuovi questa opzione per usare esclusivamente il certificato dell'archivio Relay.");
        return users[0];
    }
    public static string Algorithms(SshCertificateInfo certificate,string requestedSignature)
    {
        if(requestedSignature!="default")throw new IOException("Per usare un certificato dell'archivio scegli Algoritmi del client / config. Nessun ripiego alla firma della sola chiave.");
        var name=CertificateRevocations.Parse(certificate.Text).Name;
        return name switch {
            "ssh-ed25519-cert-v01@openssh.com"=>name,
            "ecdsa-sha2-nistp256-cert-v01@openssh.com" or "ecdsa-sha2-nistp384-cert-v01@openssh.com" or "ecdsa-sha2-nistp521-cert-v01@openssh.com"=>name,
            "ssh-rsa-cert-v01@openssh.com"=>"rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com",
            _=>throw new IOException("Tipo di certificato non supportato dal percorso OpenSSH dell'archivio.")
        };
    }
    public static void Apply(LocalLaunch launch,string certificatePath,string username,string algorithms)
    {
        if(!Path.IsPathFullyQualified(certificatePath)||!File.Exists(certificatePath)||certificatePath.Any(char.IsControl))throw new IOException("File pubblico del certificato non disponibile.");
        // A -o value is parsed again by OpenSSH: quote the filename inside the option.
        var path=NativeClient.CygwinPath(certificatePath);
        if(path.Contains('"'))throw new IOException("Percorso del certificato non valido.");
        launch.Arguments.InsertRange(0,["-o","User="+username,"-o","CertificateFile=\""+path+"\"","-o","PubkeyAcceptedAlgorithms="+algorithms]);
    }
}
