namespace UnscRelay.App;
public partial class MainWindow
{
    string PendingEditorWarning()
    {
        var messages=new List<string>();
        if(editBuffers.Values.Any(e=>e.ExternalOnly&&e.Dirty))messages.Add("Alcuni salvataggi esterni non sono stati caricati sul server. Le copie locali restano sul PC e non vengono cancellate.");
        if(editBuffers.Values.Any(e=>!e.ExternalOnly&&e.Dirty))messages.Add("Le modifiche non salvate negli editor integrati saranno scartate.");
        return string.Join(" ",messages);
    }
    async Task<bool> CloseSessionsForUpdate(Func<string,bool>? testConsent = null)
    {
        if (testConsent != null && !accessSmokeTest) throw new InvalidOperationException();
        if (sessions.Count + desktopSessions.Count + localSessions.Count + storageSessions.Count == 0 && editBuffers.Count == 0) return true;
        var text = "Per aggiornare verranno chiuse tutte le sessioni SSH, SFTP, desktop e shell locali, inclusi i programmi avviati al loro interno.";
        if (editBuffers.Values.Any(edit => edit.Dirty)) text += " "+PendingEditorWarning();
        if (!(testConsent ?? Confirm)(text + " Continuare?")) return false;
        await CloseAllStorage();
        await CloseAllLocal(false);
        foreach (var editorId in editBuffers.Keys.ToArray()) CloseEditor(editorId);
        foreach (var desktopId in desktopSessions.Keys.ToArray()) DisconnectDesktop(desktopId);
        foreach (var sessionId in sessions.Keys.ToArray()) await DisconnectWithHooks(sessionId, false);
        CloseDetachedWindows(); StopAllRecordings();
        Emit("updateSessionsClosed", new { });
        return true;
    }
}
