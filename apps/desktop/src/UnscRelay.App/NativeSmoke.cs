using System.IO;
using System.Text.Json;

namespace UnscRelay.App;
public partial class MainWindow
{
    bool smokeFinished;
    System.Diagnostics.Stopwatch? shutdownWatch;
    void BeginShutdownCheck()
    {
        data.Settings = data.Settings with { ShutdownVideo = true }; updateClosing = true;
        shutdownWatch = System.Diagnostics.Stopwatch.StartNew(); Close();
    }
    void FinishShutdownCheck()
    {
        if (!shutdownSmokeTest || shutdownWatch == null) return;
        shutdownWatch.Stop();
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "shutdown-smoke-result.json"), JsonSerializer.Serialize(new { success = shutdownWatch.ElapsedMilliseconds < 3000, shutdownConfigured = data.Settings.ShutdownVideo, updateClosing, elapsedMilliseconds = shutdownWatch.ElapsedMilliseconds }));
    }
    async Task WatchSmokeAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(accessSmokeTest&&Environment.GetEnvironmentVariable("RELAY_VERIFY_LOGIN_WAIT")=="1"?200:localSessionsSmokeTest?75:windowExplorerSmokeTest?(Environment.GetEnvironmentVariable("RELAY_DRAG_PREVIEW")=="1"?180:75):25));
        if (!smokeFinished) CompleteSmoke(false);
    }
    void CompleteSmoke(bool success)
    {
        if (!smokeTest || smokeFinished) return;
        smokeFinished = true;
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.json"), JsonSerializer.Serialize(new
        {
            success, version = CurrentVersion.ToString(), webView = Browser.CoreWebView2.Environment.BrowserVersionString,
            origin = Browser.CoreWebView2.Source, utc = DateTime.UtcNow
        }));
        exitAllowed = true;foreach(var session in localSessions.Values)session.DisposeAsync().AsTask().GetAwaiter().GetResult();localSessions.Clear();CloseDetachedWindows();CloseExportWindows(); lockTimer.Stop(); StopAllRecordings(); vault.Dispose(); Browser.Dispose(); System.Windows.Application.Current.Shutdown(success ? 0 : 1);
    }
}
