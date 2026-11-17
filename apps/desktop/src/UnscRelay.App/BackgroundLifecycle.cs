using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Threading;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    System.Windows.Forms.NotifyIcon? backgroundIcon;
    readonly DispatcherTimer backgroundUpdateTimer=new(){Interval=TimeSpan.FromMinutes(15)};
    bool explicitExit,checkingUpdates,criticalStopping;
    DateTimeOffset? lastUpdateAttempt;
    UpdatePolicy? requiredUpdatePolicy;
    bool CriticalUpdateRequired=>requiredUpdatePolicy?.Blocks(CurrentVersion)==true;
    string PublisherPath=>Path.Combine(AppContext.BaseDirectory,"Assets","Publisher.cer");
    string PolicyCachePath=>LocalDataFiles.Child(rootDataPath,"update-policy.json");
    void InitializeBackgroundLifecycle()
    {
        if(smokeTest)return;
        backgroundIcon=new System.Windows.Forms.NotifyIcon { Text="UNSC Relay",Visible=true,Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) };
        var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Apri Relay",null,(_,_)=>Dispatcher.Invoke(RestoreRelay));
        menu.Items.Add("Esci da Relay",null,(_,_)=>Dispatcher.Invoke(()=>{explicitExit=true;RestoreRelay();Close();}));
        backgroundIcon.ContextMenuStrip=menu;backgroundIcon.DoubleClick+=(_,_)=>Dispatcher.Invoke(RestoreRelay);
        Closed+=(_,_)=>{backgroundUpdateTimer.Stop();backgroundIcon?.Dispose();menu.Dispose();};
        try
        {
            FolderSync.CheckLocalParents(PolicyCachePath);
            if(File.Exists(PolicyCachePath))
            {using var certificate=X509CertificateLoader.LoadCertificateFromFile(PublisherPath);requiredUpdatePolicy=UpdatePolicy.Verify(System.Text.Encoding.UTF8.GetString(LocalDataFiles.Read(rootDataPath,"update-policy.json",32768)),certificate);}
        }
        catch(Exception ex)when(ex is UnauthorizedAccessException or IOException or InvalidDataException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException or FormatException){ /* Invalid local metadata never authorises an update or a lock. */ }
        backgroundUpdateTimer.Tick+=async(_,_)=>await CheckBackgroundUpdates(false);backgroundUpdateTimer.Start();
    }
    void RestoreRelay(){Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();if(CriticalUpdateRequired)Emit("updateAvailable",new{release=update,required=true});}
    void EnforceCriticalUpdate()
    {
        if(!CriticalUpdateRequired)return;
        transferCancellation?.Cancel();
        foreach(var job in fileJobs.Values){job.RecoveryPaused=false;job.Cancellation?.Cancel();}
        foreach(var editor in editBuffers.Values)editor.AutoUpload=false;
        foreach(var session in desktopSessions.Values){session.Host.IsEnabled=false;if(session.Host.Child!=null)session.Host.Child.Enabled=false;}
        foreach(var window in detachedWindows.Values)window.Hide();
        _=StopCriticalSessions();
        Emit("updateAvailable",new{release=update,required=true});
    }
    async Task StopCriticalSessions()
    {
        if(criticalStopping)return;criticalStopping=true;
        try
        {
            await CloseAllStorage();await CloseAllLocal(false);
            foreach(var id in desktopSessions.Keys.ToArray())DisconnectDesktop(id);
            foreach(var id in sessions.Keys.ToArray())await DisconnectWithHooks(id,false);
            StopAllRecordings();StopBundledAgent();Emit("updateSessionsClosed",new{});
        }
        catch(Exception ex)when(ex is IOException or InvalidOperationException or ObjectDisposedException){Emit("notice",new{text="Arresto delle sessioni non completato. La console resta bloccata; esci da Relay e riprova l’aggiornamento."});}
        finally{criticalStopping=false;}
    }
    void AcceptUpdatePolicy(UpdateRelease? release)
    {
        if(release?.Policy is not{Security:"critical"} policy||!policy.Blocks(CurrentVersion))return;
        if(requiredUpdatePolicy==null||Version.Parse(policy.MinimumSupportedVersion)>Version.Parse(requiredUpdatePolicy.MinimumSupportedVersion))
        {
            requiredUpdatePolicy=policy;
            LocalDataFiles.Write(rootDataPath,"update-policy.json",System.Text.Encoding.UTF8.GetBytes(release.SignedPolicy!));
        }
    }
    async Task<UpdateRelease?> CheckForUpdates()
    {
        if(checkingUpdates)throw new InvalidOperationException("Controllo aggiornamenti già in corso.");
        checkingUpdates=true;lastUpdateAttempt=DateTimeOffset.UtcNow;
        try{var previouslyRequired=CriticalUpdateRequired;updateToken=updateTokens.Read();var release=await new GitHubUpdates().CheckAsync(updateToken,CurrentVersion,CancellationToken.None,PublisherPath);AcceptUpdatePolicy(release);if(CriticalUpdateRequired&&release!=null&&Version.Parse(release.Version.TrimStart('v'))<Version.Parse(requiredUpdatePolicy!.MinimumSupportedVersion))throw new InvalidDataException("La release disponibile non soddisfa il requisito di sicurezza firmato.");update=release;if(CriticalUpdateRequired&&!previouslyRequired)EnforceCriticalUpdate();return release;}
        finally{checkingUpdates=false;}
    }
    async Task CheckBackgroundUpdates(bool startup)
    {
        if(smokeTest||closing||updating||checkingUpdates)return;
        if(CriticalUpdateRequired)EnforceCriticalUpdate();
        if(!UpdateSchedule.Due(startup,data.Settings.CheckUpdatesOnStartup,data.Settings.CheckUpdatesDaily,lastUpdateAttempt,DateTimeOffset.UtcNow))return;
        try{var release=await CheckForUpdates();if(release!=null){Emit("updateAvailable",new{release,required=CriticalUpdateRequired});backgroundIcon?.ShowBalloonTip(6000,"UNSC Relay",CriticalUpdateRequired?"Aggiornamento di sicurezza critico richiesto. Apri Relay per installarlo.":"È disponibile un aggiornamento. Apri Relay per leggere le novità.",System.Windows.Forms.ToolTipIcon.Info);}}
        catch(Exception ex)when(ex is System.Net.Http.HttpRequestException or TaskCanceledException or IOException or System.Security.Cryptography.CryptographicException or InvalidOperationException or System.Text.Json.JsonException or FormatException){Emit("updateCheckFailed",new{required=CriticalUpdateRequired,text="Controllo aggiornamenti non completato. Riprova dalla schermata Aggiornamenti."});}
    }
}
