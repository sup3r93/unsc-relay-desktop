namespace UnscRelay.App;
public partial class MainWindow
{
    async Task<bool> CloseSessionsForUpdate(Func<string,bool>? testConsent = null)
    {
        if (testConsent != null && !accessSmokeTest) throw new InvalidOperationException();
        if (sessions.Count + desktopSessions.Count + localSessions.Count == 0 && editBuffers.Count == 0) return true;
        var text = "Per aggiornare verranno chiuse tutte le sessioni SSH, SFTP, desktop e shell locali, inclusi i programmi avviati al loro interno.";
        if (editBuffers.Values.Any(edit => edit.Dirty)) text += " Le modifiche non salvate negli editor saranno scartate.";
        if (!(testConsent ?? Confirm)(text + " Continuare?")) return false;
        await CloseAllLocal();
        foreach (var editorId in editBuffers.Keys.ToArray()) CloseEditor(editorId);
        foreach (var desktopId in desktopSessions.Keys.ToArray()) DisconnectDesktop(desktopId);
        foreach (var sessionId in sessions.Keys.ToArray()) await DisconnectWithHooks(sessionId, false);
        CloseDetachedWindows(); StopAllRecordings();
        Emit("updateSessionsClosed", new { });
        return true;
    }
}
