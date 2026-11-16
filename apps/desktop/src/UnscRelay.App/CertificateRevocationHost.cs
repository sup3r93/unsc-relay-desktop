using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task<object?> CertificateRevocationAction(string action, JsonElement payload)
    {
        if (!vault.IsUnlocked) throw new InvalidOperationException("Sblocca prima l'archivio chiavi.");
        if (action == "certificateRevoke")
        {
            var key = vault.List().Single(k => k.Id == S(payload, "keyId"));
            if (key.Certificate == null) throw new InvalidOperationException("Nessun certificato associato.");
            if (!Confirm("Revocare il certificato di «" + key.Name + "»?\nI nuovi accessi da questo archivio saranno bloccati. Le sessioni gia aperte continuano. Per bloccarlo sui server esporta e distribuisci la KRL; l'app non modifica i server. La revoca resta nel backup cifrato anche eliminando la chiave.")) return Snapshot();
            await Task.Run(() => vault.RevokeCertificate(key.Id, S(payload, "reason"))); return Snapshot();
        }
        if (action == "certificateKrlExport")
        {
            var revocations = vault.Revocations();
            if (revocations.Count == 0) throw new InvalidOperationException("Non ci sono certificati revocati.");
            var destination = SaveFile("Esporta la lista pubblica delle revoche OpenSSH", "relay-revoked.krl"); if (destination == null) return new { exported = false };
            var bytes = await OpenSshKrl.GenerateAsync(revocations); AtomicFile.Write(destination, bytes);
            return new { exported = true, count = revocations.Count, fileName = Path.GetFileName(destination) };
        }
        var info = vault.List().Single(k => k.Id == S(payload, "keyId")); var cert = info.Certificate ?? throw new InvalidOperationException("Nessun certificato associato.");
        var path = OpenFile("Seleziona una lista KRL OpenSSH da verificare"); if (path == null) return new { cancelled = true };
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new ArgumentException("Lista KRL troppo grande.");
        var revoked = await OpenSshKrl.QueryAsync(await File.ReadAllBytesAsync(path), cert.Text);
        return new { cancelled = false, revoked, fileName = Path.GetFileName(path) };
    }
}
