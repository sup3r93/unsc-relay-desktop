using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    static readonly bool accountBrowserSmokeTest=Environment.GetCommandLineArgs().Contains("--account-browser-smoke-test");
    static readonly bool presentationSmokeTest=Environment.GetCommandLineArgs().Contains("--presentation-smoke-test");
    static readonly bool presentationPreview=Environment.GetCommandLineArgs().Contains("--presentation-preview");
    static readonly bool accessSmokeTest=Environment.GetCommandLineArgs().Contains("--access-smoke-test");
    static readonly bool recoverySmokeTest=Environment.GetCommandLineArgs().Contains("--recovery-smoke-test");
    static readonly bool nativeIntegrationSmokeTest=Environment.GetCommandLineArgs().Contains("--native-integration-smoke-test");
    static readonly bool nativePackSmokeTest=Environment.GetCommandLineArgs().Contains("--native-pack-smoke-test");
    static readonly bool localSessionsSmokeTest=Environment.GetCommandLineArgs().Contains("--local-sessions-smoke-test");
    static readonly bool windowExplorerSmokeTest=Environment.GetCommandLineArgs().Contains("--windows-explorer-smoke-test");
    static readonly bool configLogsSmokeTest=Environment.GetCommandLineArgs().Contains("--config-logs-smoke-test");
    static readonly bool conversionSmokeTest=Environment.GetCommandLineArgs().Contains("--conversion-smoke-test");
    static readonly bool filesSmokeTest = Environment.GetCommandLineArgs().Contains("--files-smoke-test");
    static readonly bool desktopSmokeTest = Environment.GetCommandLineArgs().Contains("--desktop-smoke-test");
    static readonly bool shutdownSmokeTest = Environment.GetCommandLineArgs().Contains("--update-shutdown-smoke-test");
    static readonly bool sequenceSmokeTest = Environment.GetCommandLineArgs().Contains("--sequence-smoke-test");
    static readonly bool smokeTest = accountBrowserSmokeTest || accessSmokeTest || recoverySmokeTest || nativeIntegrationSmokeTest || nativePackSmokeTest || localSessionsSmokeTest || windowExplorerSmokeTest || configLogsSmokeTest || conversionSmokeTest || presentationSmokeTest || presentationPreview || sequenceSmokeTest || filesSmokeTest || desktopSmokeTest || shutdownSmokeTest || Environment.GetCommandLineArgs().Contains("--smoke-test");
    readonly string rootDataPath = smokeTest ? Path.Combine(AppContext.BaseDirectory, ".smoke-data",accountBrowserSmokeTest?"AccountBrowser":accessSmokeTest?"Access-"+Guid.NewGuid().ToString("N"):recoverySmokeTest?"Recovery":nativeIntegrationSmokeTest?"NativeIntegration":localSessionsSmokeTest?"LocalSessions":sequenceSmokeTest?"Sequences":configLogsSmokeTest?"ConfigLogs":windowExplorerSmokeTest?"WindowExplorer":desktopSmokeTest?"Desktop":"") : LocalDataFiles.ApplicationRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    string dataPath=null!;
    ProfileStore store = null!; KeyVault vault = null!; HostKeyStore hosts = null!; AppData data = null!;
    bool keyGenerationBusy;
    readonly Dictionary<string, SshSession> sessions = [];
    readonly HashSet<string> connectingSessions = [];
    readonly Dictionary<string, string> connectionPasswords = [];
    readonly DispatcherTimer lockTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    bool startupSequencePlaying; readonly Stopwatch startupSequenceWatch=new(); Stopwatch? shutdownSequenceWatch;
    bool exitAllowed, closing; TaskCompletionSource? shutdownDone;
    UpdateRelease? update; string updateToken = ""; UpdateTokenStore updateTokens = null!; bool updating, updateClosing; int activeOperations;
    const string Origin = "https://relay.local";
    static Version CurrentVersion { get { var v = typeof(MainWindow).Assembly.GetName().Version!; return new Version(v.Major, v.Minor, v.Build); } }
    LocalApplicationLease? applicationLease;Guid actualWorkspace;
    bool initializingRelay;
    async void InitializeRelay(){initializingRelay=true;Closing+=(_,e)=>{if(initializingRelay)e.Cancel=true;};try{await InitializeRelayChecked();}catch(Exception error)when(error is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException or JsonException){if(blockedStartupWorkspace is{} blocked)ShowErasureVerificationLock(blocked);else ShowLocalDataLock(error is LocalAccountErasedException);}finally{initializingRelay=false;}}
    async Task InitializeRelayChecked()
    {
        try{applicationLease=new LocalApplicationLease(rootDataPath);Closed+=(_,_)=>applicationLease?.Dispose();await ResumeAccountErasure();actualWorkspace=new AccountWorkspaces(rootDataPath).Load().Active;new LocalErasureJournal(rootDataPath).GuardWorkspace(actualWorkspace);}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException or JsonException){if(blockedStartupWorkspace is{} blocked)ShowErasureVerificationLock(blocked);else ShowLocalDataLock(error is LocalAccountErasedException);return;}
        dataPath=new AccountWorkspaces(rootDataPath).DirectoryFor(actualWorkspace);Directory.CreateDirectory(dataPath);FolderSync.CheckLocalParents(dataPath);
        SyncLocalBackup.Recover(dataPath);
        store = new(dataPath); vault = new(Path.Combine(dataPath, "keys.vault")); hosts = new(Path.Combine(dataPath, "hostkeys.json")); data = store.Load();nativeLanguage=InterfaceLocale.Normalize(data.Settings.Language);LocalLanguageChoice.Save(dataPath,nativeLanguage); updateTokens = new(rootDataPath); InitializeEditorPreference();
        foreach (var folder in data.Profiles.Select(p => p.Folder).ToArray()) FolderCatalog.Add(data, folder.Length == 0 ? "Connessioni" : folder);
        try{ExplorerExport.Prune(Path.Combine(dataPath,"DragExports"),DateTime.UtcNow);}catch(IOException){}catch(UnauthorizedAccessException){}
        LoadFileQueue();InitializeRecordingRetention();InitializeKeyConversion();
        InitializeCloud();InitializeErasureMonitor();
        InitializeBackgroundLifecycle();
        InitializeAssistantApi();
        if(smokeTest&&!sequenceSmokeTest){StartupCover.Visibility=System.Windows.Visibility.Collapsed;Browser.Visibility=System.Windows.Visibility.Visible;consoleReady=true;}
        if(sequenceSmokeTest)data.Settings=data.Settings with{StartupVolume=.23,ShutdownVolume=.61};
        data.Settings=SequencePolicy.Apply(data.Settings);startupSequencePlaying=sequenceSmokeTest||!smokeTest;startupSequenceWatch.Start();
        if (smokeTest) data.Settings = data.Settings with { StartupVideo = false, ShutdownVideo = false };
        if(IsLoaded)await InitializeBrowser();else Loaded += async (_, _) => await InitializeBrowser(); Closing += OnRelayClosing;
        Microsoft.Win32.SystemEvents.SessionSwitch += OnWindowsSessionSwitch;Closed += (_,_) => Microsoft.Win32.SystemEvents.SessionSwitch -= OnWindowsSessionSwitch;
        lockTimer.Tick += (_, _) => { if(nativeErasure is{} erasure&&(erasure.Expires<=DateTimeOffset.UtcNow||CriticalUpdateRequired||!vault.IsUnlocked||AppLocked||AccountLocked))ClearNativeErasure(); if(nativePlainExport is{} export&&(export.Expires<=DateTimeOffset.UtcNow||CriticalUpdateRequired||!vault.IsUnlocked||AppLocked||AccountLocked))ClearNativePlainExport(); if(!CriticalUpdateRequired)RenewExternalCertificates(); if (!helloBusy && vault.IsUnlocked && DateTime.UtcNow - vault.LastAccessUtc > TimeSpan.FromMinutes(data.Settings.AutoLockMinutes)) { StopAllRecordings(); StopBundledAgent(); ClearNativePlainExport(); LockVault(); Emit("vault", new { unlocked = false }); } }; lockTimer.Start();
    }
    void ShowLocalDataLock(bool erased)
    {
        var title=NativeText(erased?"Account cancellato":"Dati locali non verificabili");
        var description=NativeText(erased?"L’accesso a questo account è stato revocato. I dati locali sono bloccati e nessun vecchio backup verrà ripristinato. La rimozione delle copie gestite deve essere completata.":"Relay non riesce a verificare in sicurezza il collegamento dell’account o il registro locale. Nessun archivio è stato aperto o ripristinato.");
        var panel=new System.Windows.Controls.StackPanel{MaxWidth=640,Margin=new Thickness(32),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        panel.Children.Add(new System.Windows.Controls.TextBlock{Text=title,FontSize=28,FontWeight=FontWeights.Bold,Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});
        panel.Children.Add(new System.Windows.Controls.TextBlock{Text=description,FontSize=17,Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,24)});
        var exit=CreateNativeButton("Esci da Relay");exit.Click+=(_,_)=>Application.Current.Shutdown();panel.Children.Add(exit);Content=ErasureSurface(panel);Loaded+=(_,_)=>exit.Focus();
    }
    async Task InitializeBrowser()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataPath, "WebView"), new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"+(smokeTest?" --mute-audio":"")));
            await Browser.EnsureCoreWebView2Async(env); var core = Browser.CoreWebView2;
            core.IsMuted = smokeTest;
            core.SetVirtualHostNameToFolderMapping("relay.local", Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDevToolsEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false; core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsPasswordAutosaveEnabled = false; core.Settings.IsGeneralAutofillEnabled = false;
            core.NewWindowRequested += OpenDetachedWindow; core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith(Origin + "/", StringComparison.Ordinal)) e.Cancel = true; };
            core.NavigationCompleted += (_, e) => { if(e.IsSuccess){ Browser.Visibility=System.Windows.Visibility.Visible; StartupCover.Visibility=System.Windows.Visibility.Collapsed; } };
            core.WebMessageReceived += OnRelayMessage; core.Navigate(Origin + "/index.html");
            if (smokeTest) _ = WatchSmokeAsync();
            else _ = CheckBackgroundUpdates(true);
        }
        catch (Exception ex) { MessageBox.Show("Avvio interfaccia non riuscito. Verifica WebView2 Runtime.\n" + ex.Message, "UNSC Relay"); exitAllowed = true; Close(); }
    }
    void Emit(string type, object payload)
    {
        if (exitAllowed || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => {
            if (exitAllowed || Dispatcher.HasShutdownStarted) return;
            Browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, payload }, ProfileStore.Json));
        });
    }
    string Secret(string title, string text) => Dispatcher.Invoke(() => nativePresentationMode?throw new OperationCanceledException():SecretDialog.Ask(this, title, text));
    bool Confirm(string text) {var generation=authenticationGeneration;var approved=configLogsSmokeTest&&diagnosticConfirm!=null?diagnosticConfirm(text):Dispatcher.Invoke(() => !nativePresentationMode&&new ConfirmationDialog(this,text).ShowDialog()==true);return approved&&generation==authenticationGeneration&&!AccountErasureLocked;}
    bool Trust(HostKey key) => Confirm($"Prima connessione a {key.Host}:{key.Port}\n\n{(key.IsCertificateAuthority ? "CA del certificato host" : "Chiave host")} · {key.Algorithm}\nSHA256: {key.Fingerprint}\n\nVerifica l'impronta con il gestore del server. {(key.IsCertificateAuthority ? "La fiducia nella CA sara limitata a questo host e questa porta." : "Memorizzare questa chiave?")}");
    string? OpenFile(string title) { if(nativePresentationMode)return null;var d = new OpenFileDialog { Title = title }; var generation=authenticationGeneration;var accepted=d.ShowDialog(this)==true;RequireOperationSession(generation);return accepted?d.FileName:null; }
    string? SaveFile(string title, string name) { if(nativePresentationMode)return null;if(configLogsSmokeTest&&diagnosticSave!=null)return diagnosticSave(title,name);var d = new SaveFileDialog { Title = title, FileName = name }; var generation=authenticationGeneration;var accepted=d.ShowDialog(this)==true;RequireOperationSession(generation);return accepted?d.FileName:null; }
    static string S(JsonElement p, string k, string fallback = "") => p.TryGetProperty(k, out var v) ? v.GetString() ?? fallback : fallback;
    static int I(JsonElement p, string k, int fallback) => p.TryGetProperty(k, out var v) && v.TryGetInt32(out var n) ? n : fallback;
    SshSession Session(JsonElement p) => sessions.TryGetValue(S(p, "sessionId"), out var s) ? s : throw new InvalidOperationException("Sessione non disponibile.");
    object Snapshot() => new { account=CloudStatus(), data, vault = new { exists = vault.Exists, unlocked = vault.IsUnlocked, keys = vault.IsUnlocked ? vault.List() : [], passwords = vault.IsUnlocked ? vault.Passwords() : [], revocations = vault.IsUnlocked ? vault.Revocations() : [], windowsHello = AccountHelloAllowed() }, hostKeys = hosts.List(), version = CurrentVersion.ToString(), dataPath, criticalUpdateRequired=CriticalUpdateRequired, diagnostic = smokeTest&&!sequenceSmokeTest };
    async void OnRelayMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!e.Source.StartsWith(Origin + "/", StringComparison.Ordinal)) return; string? id = null;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson); var root = doc.RootElement;
            id = S(root, "id"); var action = S(root, "action"); var p = root.GetProperty("payload").Clone();
            if (smokeTest && action == "diagnosticReady") { if(accountBrowserSmokeTest)await CheckAccountBrowser();else if(accessSmokeTest)await CheckApplicationAccess();else if(recoverySmokeTest)await CheckCoordinatedRecovery();else if(nativeIntegrationSmokeTest)await CheckNativeIntegration();else if(nativePackSmokeTest)await CheckNativePack();else if(localSessionsSmokeTest)await CheckLocalSessions();else if(windowExplorerSmokeTest)await CheckWindowExplorer();else if(configLogsSmokeTest)CheckConfigLogsBridge(); else if(conversionSmokeTest)await CheckConversionBridge(); else if(presentationSmokeTest||presentationPreview)await CheckPresentation(); else if (filesSmokeTest) await CheckFileWorkspace(); else if (desktopSmokeTest) await CheckDesktopComponents(); else if (shutdownSmokeTest) BeginShutdownCheck(); else CompleteSmoke(true); return; }
            if (action == "shutdownDone") { var failed=p.TryGetProperty("failed",out var failure)&&failure.GetBoolean(); if(updateClosing||shutdownSequenceWatch!=null&&SequencePolicy.CanFinish(shutdownSequenceWatch.Elapsed,failed))shutdownDone?.TrySetResult(); return; } if (closing) return;
            activeOperations++;
            try
            {
                object? result;
                if (action == "dropPaths")
                {
                    if (CriticalUpdateRequired||updating||AccountLocked||AppLocked) throw new IOException("Attendi l’aggiornamento o autentica l’accesso.");
                    var files = e.AdditionalObjects.OfType<CoreWebView2File>().Select(f => f.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
                    if (files.Length is < 1 or > 1000) throw new IOException("Trascina da 1 a 1000 elementi da Esplora file.");
                    result = new { paths = files };
                }
                else result = await Execute(action, p);
                Emit("reply", new { id, result });
            }
            finally { activeOperations--; }
        }
        catch (Exception ex) { Emit("reply", new { id, retryable = NetworkReconnect.IsTransient(ex), error = NativeText(ex is CryptographicException ? "Password principale errata o archivio non integro." : ex is OperationCanceledException ? "Operazione annullata." : ex.Message) }); }
    }
    async Task<object?> Execute(string action, JsonElement p)
    {
        if(action=="presentationMode")return SetPresentationMode(p.GetProperty("enabled").GetBoolean());
        if(nativePresentationMode&&action is not("bootstrap" or "startupStarted" or "startupPresented" or "startupFinished"))throw new InvalidOperationException(NativeText("Disattiva presentazione"));
        if(Volatile.Read(ref erasureRevocationFlag)!=0)throw new OperationCanceledException("Account cancellato. Chiusura delle attività in corso.");
        if(CriticalUpdateRequired && action is not("keyInstallCancel" or "fileBridgeCancel" or "remoteSearchCancel" or "languageSettings" or "cancelTransfer" or "storageCancel" or "folderCancel" or "filePause" or "fileDiscard" or "conversionCancel" or "recordingStop" or "bootstrap" or "startupStarted" or "startupPresented" or "startupFinished" or "applicationExit" or "exit" or "checkUpdates" or "installUpdate" or "updateTokenStatus" or "updateTokenSave" or "updateTokenDelete" or "activateMain"))throw new InvalidOperationException("È richiesto un aggiornamento di sicurezza critico. Apri Aggiornamenti per installarlo.");
        if (updating && action != "bootstrap") throw new InvalidOperationException("Attendi la fine dell’aggiornamento.");
        if((AppLocked||AccountLocked) && action is not("languageSettings" or "workspaceList" or "workspaceSwitch" or "workspaceAdd" or "workspaceForget" or "accountLogin" or "accountRegister" or "accountAvailability" or "accountStatus" or "accountSite" or "bootstrap" or "startupStarted" or "startupPresented" or "startupFinished" or "applicationAuthenticate" or "applicationExit" or "updateTokenStatus" or "updateTokenSave" or "updateTokenDelete" or "checkUpdates" or "installUpdate"))throw new InvalidOperationException("Autentica prima l’accesso alla console.");
        switch (action)
        {
            case "workspaceCustomize": case "workspaceList": case "workspaceSwitch": case "workspaceAdd": case "workspaceForget": return await WorkspaceAccountAction(action,p);
            case "nativeFilesOpen": return await OpenNativeFileWorkspace(p);
            case "storageNativeOpen": case "storageOpen": case "storageList": case "storageTransfer": case "storageCompare": case "storageChange": case "storageCancel": return await StorageAction(action,p);
            case "diagnosticPreview": return DiagnosticReport();
            case "diagnosticExport": return ExportDiagnostic();
            case "accountErasureStatus": case "accountErasureReview": case "accountErasureBegin": case "accountErasureConfirm": case "accountErasureClose": return await NativeErasureAction(action,p);
            case "archivePlainReview": case "archivePlainBegin": case "archivePlainSave": case "archivePlainCancel": return await NativePlainExportAction(action,p);
            case "archiveRequestAccess": case "archiveAccessRequests": case "archiveRequestApprove": case "archiveRequestDismiss": case "archiveList": case "archiveLocalList": case "archivePrepare": case "archiveCreate": case "archiveRead": case "archiveRecover": case "archiveStorageUsage": case "archiveActivity": case "archivePermissionsReport": case "archiveAccessReview": case "archiveAccessReviewed": case "archiveSharingReview": case "archiveSharingSet": case "archiveIntegrity": case "archiveIntegrityLast": case "archiveMembers": case "archiveRename": case "archiveRevoke": case "archiveAuthorize": case "archiveShare": case "archiveTransfer": case "archiveCopySecret": case "archiveCommentAdd": case "archiveUpdateRecord": case "archiveAddPassword": case "archiveDeleteRecord": case "archivePublish": case "archiveExport":
            case "accountProfileSave": case "accountPasswordSave": case "accountMethodRename": case "accountMethodRemove": case "accountMethods": case "notificationOverview": case "notificationRead": case "notificationReadAll": case "notificationDisplay": case "accountStorageAttachmentDelete": case "accountStorageAttachments": case "accountStorageUsage": case "homeOverview": case "ticketFind": case "ticketLinks": case "ticketLinkCreate": case "ticketLinkDelete": case "ticketAttachmentPreview": case "ticketAttachmentSave": case "ticketList": case "ticketRead": case "ticketCreate": case "ticketReply":
            case "accountAvailability": case "accountRegister": case "accountSecurity": case "accountProfile": case "accountLogout": case "accountLogin": case "accountStatus": case "accountSyncOverview": case "accountSite": case "accountSetup": case "accountSync": case "accountUiBusy": return await CloudAction(action,p);
            case "remoteServerInfo": var infoGeneration=authenticationGeneration;RequireOperationSession(infoGeneration);using(var infoStop=CancellationTokenSource.CreateLinkedTokenSource(accountOperationStop.Token)){infoStop.CancelAfter(TimeSpan.FromSeconds(15));var info=await Session(p).ServerInfoAsync(S(p,"path"),infoStop.Token);RequireOperationSession(infoGeneration);return info;}
            case "operationsOverview": return OperationsOverview(p);
            case "privacyInventory": case "privacyCacheClear": case "privacyClean": return await PrivacyAction(action,p);
            case "keyInstallKeys": case "keyInstallPreview": case "keyInstallApply": case "keyInstallCancel": return await PublicKeyInstallationAction(action,p);
            case "remoteSearch": case "remoteSearchCancel": return await RemoteSearchAction(action,p);
            case "fileLocation": case "localList": case "localChoose": case "remoteLog": case "sftpDirectory": case "sftpHome": case "fileCompare": case "sftpOperation":
            case "folderCompare": case "folderSync": case "folderCancel": case "fileBridgeCancel": case "fileBridgeTargets": case "fileBridgePlan": case "fileBridgeEnqueue": case "filePlan": case "fileParallelism": case "fileEnqueue": case "fileQueue": case "fileClear": case "filePause": case "fileResume": case "fileDiscard":
            case "editorOpen": case "editorSave": case "editorExternal": case "editorPublish": case "editorAuto": case "editorDirty": case "editorReload": case "editorClose":
            case "clipboardWrite": case "clipboardRead": case "machineDelete": case "machineSave": return await FileAction(action, p);
            case "confirmAction": return Confirm(S(p,"text") is{Length:>0} text?text:S(p,"message"));
            case "nativeTools": case "nativeSelectFile": case "nativeTask": case "externalCertificateSave": case "externalCertificateIssue": case "externalCertificateStatus": return await AdvancedAction(action,p);
            case "localAvailability": return await LocalAvailability();
            case "localShellFolder": var localFolder=new OpenFolderDialog{Title="Cartella iniziale della shell"};return localFolder.ShowDialog(this)==true?localFolder.FolderName:null;
            case "bootstrap": return AccessBootstrap();
            case "audioSettings":
                var musicVolume=p.GetProperty("volume").GetDouble();
                if(!double.IsFinite(musicVolume)||musicVolume<0||musicVolume>1)throw new ArgumentException("Volume non valido.");
                double ReadSequenceVolume(string name,double current){if(!p.TryGetProperty(name,out var value))return current;var number=value.GetDouble();if(!double.IsFinite(number)||number<0||number>1)throw new ArgumentException("Volume non valido.");return number;}
                var introVolume=ReadSequenceVolume("startupVolume",data.Settings.StartupVolume);
                var outroVolume=ReadSequenceVolume("shutdownVolume",data.Settings.ShutdownVolume);
                var previousAudioSettings=data.Settings;
                data.Settings=data.Settings with{AppMusicEnabled=p.GetProperty("enabled").GetBoolean(),AppMusicVolume=musicVolume,StartupVolume=introVolume,ShutdownVolume=outroVolume};
                try{store.Save(data);}catch{data.Settings=previousAudioSettings;throw;}return Snapshot();
            case "musicSet": data.Settings=data.Settings with{AppMusicEnabled=p.GetProperty("enabled").GetBoolean()};store.Save(data);return Snapshot();
            case "assistantConfig":return new{mcpServers=new Dictionary<string,object>{{"unsc-relay",new{command=Path.Combine(AppContext.BaseDirectory,"ChatGPT","UNSC Relay MCP.exe"),args=Array.Empty<string>()}}}};
            case "applicationAuthenticate": case "applicationExit": case "accessSettings": return await AccessAction(action,p);
            case "startupPresented": Browser.Visibility=System.Windows.Visibility.Visible; StartupCover.Visibility = System.Windows.Visibility.Collapsed; return null;
            case "startupStarted": startupSequencePlaying=true;startupSequenceWatch.Restart();if(sequenceSmokeTest)ProbeIntroClose();return null;
            case "startupFinished": var startupFailed=p.TryGetProperty("failed",out var startupFailure)&&startupFailure.GetBoolean();if(!SequencePolicy.CanFinish(startupSequenceWatch.Elapsed,startupFailed))throw new InvalidOperationException("La sequenza non e ancora terminata.");startupSequencePlaying=false;return null;
            case "consoleReady": if(accessFinalWatch!=null&&accessFinalWatch.Elapsed.TotalSeconds<2.1)throw new InvalidOperationException("Attendi la sequenza finale.");if(AppLocked||AccountLocked)throw new InvalidOperationException("Accesso richiesto.");consoleReady=true;if(sequenceSmokeTest)_ = Dispatcher.BeginInvoke(async()=>await ProbeSequenceOutro());return null;
            case "prepareExplorerExport": return await PrepareExplorerExport(p);
            case "workspaceWindowCapture": case "workspaceWindowPrepare": return WorkspaceWindowAction(action,p);
            case "activateMain": case "detachedClose": case "detachedFocus": return DetachedAction(action,p);
            case "recordingRetentionStatus": case "recordingRetentionSet": case "recordingStart": case "recordingStop": case "recordingList": case "recordingExportText": case "recordingRead": case "recordingDelete": case "recordingPrune": return RecordingAction(action,p);
            case "multiSend": case "sshConfigPreview": case "sshConfigImport": case "sshConfigWritePreview": case "sshConfigWriteApply": case "sshConfigExport": return WorkflowAction(action, p);
            case "desktopLayout": DesktopLayout(p); return null;
            case "connectionTemplateSave": {if(data.ConnectionTemplates.Count>=50)throw new InvalidOperationException("Massimo 50 modelli di connessione.");var source=data.Profiles.Single(profile=>profile.Id==S(p,"profileId"));var template=ConnectionTemplate.Create(source,S(p,"name"));var previous=data.ConnectionTemplates;data.ConnectionTemplates=[..previous,template];try{store.Save(data);}catch{data.ConnectionTemplates=previous;throw;}return Snapshot();}
            case "connectionTemplateDelete": {var previous=data.ConnectionTemplates;data.ConnectionTemplates=previous.Where(value=>value.Id!=S(p,"id")).ToList();try{store.Save(data);}catch{data.ConnectionTemplates=previous;throw;}return Snapshot();}
            case "connectionTemplateUse": return data.ConnectionTemplates.Single(value=>value.Id==S(p,"id")).Instantiate(p.GetProperty("network").GetBoolean(),p.GetProperty("appearance").GetBoolean());
            case "connectionProbe": {var generation=authenticationGeneration;var result=await ConnectionProbe.Run(data.Profiles.Single(profile=>profile.Id==S(p,"profileId")),accountOperationStop.Token);RequireOperationSession(generation);return result;}
            case "connectionViewSave":
                var view=p.GetProperty("view").Deserialize<ConnectionSearchView>(ProfileStore.Json)??throw new ArgumentException("Ricerca salvata non valida.");var previousViews=data.ConnectionViews;var proposedViews=previousViews.Where(value=>value.Id!=view.Id).Append(view).ToList();ConnectionSearchView.Validate(proposedViews);proposedViews=proposedViews.Select(value=>value.Id==view.Id?value with{Name=value.Name.Trim()}:value).ToList();data.ConnectionViews=proposedViews;try{store.Save(data);}catch{data.ConnectionViews=previousViews;throw;}return Snapshot();
            case "connectionViewDelete":
                var removedView=Guid.Parse(S(p,"id"));var oldViews=data.ConnectionViews;data.ConnectionViews=oldViews.Where(value=>value.Id!=removedView).ToList();try{store.Save(data);}catch{data.ConnectionViews=oldViews;throw;}return Snapshot();
            case "connectionNotes":
                var notesProfile=data.Profiles.Single(profile=>profile.Id==S(p,"profileId"));var notes=S(p,"notes");if(notes.Length>32768)throw new ArgumentException("Le istruzioni non possono superare 32.768 caratteri.");var operations=p.GetProperty("operations").Deserialize<ConnectionOperations>(ProfileStore.Json)??throw new ArgumentException("Note operative non valide.");operations.Validate();var expectedNotes=p.GetProperty("expected");var expectedOperations=expectedNotes.GetProperty("operations").Deserialize<ConnectionOperations>(ProfileStore.Json)??new();expectedOperations.Validate();if(S(expectedNotes,"notes")!=notesProfile.Notes||JsonSerializer.Serialize(expectedOperations,ProfileStore.Json)!=JsonSerializer.Serialize(notesProfile.Operations,ProfileStore.Json))throw new IOException("Le note sono cambiate. Conserva le modifiche e riapri il profilo prima di sostituirle.");var previousNotes=notesProfile.Notes;var previousOperations=notesProfile.Operations;notesProfile.Notes=notes;notesProfile.Operations=operations;try{store.Save(data);}catch{notesProfile.Notes=previousNotes;notesProfile.Operations=previousOperations;throw;}return Snapshot();
            case "saveProfile": return SaveConnectionProfile(p);
            case "folderCreate": FolderCatalog.Add(data, S(p, "path")); store.Save(data); return Snapshot();
            case "folderRename": FolderCatalog.Rename(data, S(p, "path"), S(p, "replacement")); store.Save(data); return Snapshot();
            case "folderDelete":
                if (Confirm("Eliminare la cartella vuota selezionata?")) { FolderCatalog.RemoveEmpty(data, S(p, "path")); store.Save(data); } return Snapshot();
            case "deleteProfile": return DeleteConnectionProfile(S(p, "profileId"));
            case "exportProfiles": var export = SaveFile("Esporta profili senza chiavi o password", "relay-profiles.json"); if (export != null) { var safe = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, ProfileStore.Json), ProfileStore.Json)!; safe.Profiles = safe.Profiles.Select(profile => profile with { PasswordId = "" }).ToList(); AtomicFile.Write(export, JsonSerializer.SerializeToUtf8Bytes(safe, ProfileStore.Json)); } return null;
            case "importProfiles": var import = OpenFile("Importa profili Relay"); if (import != null) store.Import(import, data); return Snapshot();
            case "saveWorkspace": var w = p.Deserialize<Workspace>(ProfileStore.Json)!; w.Validate(); if (w.ProfileIds.Any(id => !data.Profiles.Any(profile => profile.Id == id))) throw new ArgumentException("Un profilo del workspace non esiste più."); var previousWorkspaces=data.Workspaces;data.Workspaces=[..previousWorkspaces.Where(x=>x.Name!=w.Name),w];try{store.Save(data);}catch{data.Workspaces=previousWorkspaces;throw;}return Snapshot();
            case "saveSnippet": var snippet = p.Deserialize<Snippet>(ProfileStore.Json)!; SnippetCatalog.ValidateShortcut(snippet,data.Snippets); var previousSnippets=data.Snippets;data.Snippets=[..previousSnippets.Where(x=>x.Id!=snippet.Id),snippet];try{store.Save(data);}catch{data.Snippets=previousSnippets;throw;} return Snapshot();
            case "deleteSnippet":
                if (!Confirm("Eliminare lo snippet selezionato?")) throw new OperationCanceledException();
                data.Snippets.RemoveAll(x => x.Id == S(p, "snippetId")); store.Save(data); return Snapshot();
            case "exportSnippets":
                var snippetExport = SaveFile("Esporta libreria snippet", "relay-snippets.json");
                if (snippetExport != null) AtomicFile.Write(snippetExport, JsonSerializer.SerializeToUtf8Bytes(data.Snippets, ProfileStore.Json)); return null;
            case "importSnippets":
                var snippetImport = OpenFile("Importa libreria snippet Relay"); if (snippetImport == null) return Snapshot();
                if (new FileInfo(snippetImport).Length > 1024 * 1024) throw new ArgumentException("La libreria supera 1 MiB.");
                var importedSnippets = SnippetCatalog.Import(File.ReadAllBytes(snippetImport));
                if (!Confirm("Aggiungere " + importedSnippets.Count + " snippet alla libreria? I comandi vengono soltanto salvati e non eseguiti.")) throw new OperationCanceledException();
                data.Snippets.AddRange(importedSnippets); store.Save(data); return Snapshot();
            case "editorChooseProgram":
                var editorProgram=OpenFile("Scegli il programma EXE per modificare i file remoti");if(editorProgram==null)return Snapshot();
                if(!Path.IsPathFullyQualified(editorProgram)||!editorProgram.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(editorProgram))throw new ArgumentException("Scegli un file EXE esistente.");
                var editorSettings=data.Settings;data.Settings=data.Settings with{ExternalEditor=Path.GetFullPath(editorProgram),EditorPreference="custom"};try{store.Save(data);}catch{data.Settings=editorSettings;throw;}return Snapshot();
            case "editorDetectProgram":
                var beforeDetection=data.Settings;data.Settings=data.Settings with{ExternalEditor=EditorDiscovery.FindNotepadPlusPlus(),EditorPreference="auto"};try{store.Save(data);}catch{data.Settings=beforeDetection;throw;}return Snapshot();
            case "openNotepadWebsite": Process.Start(new ProcessStartInfo("https://notepad-plus-plus.org/"){UseShellExecute=true});return null;
            case "editorResetProgram":
                var oldEditorSettings=data.Settings;data.Settings=data.Settings with{ExternalEditor="",EditorPreference="notepad"};try{store.Save(data);}catch{data.Settings=oldEditorSettings;throw;}return Snapshot();
            case "languageSettings":
                var locale=InterfaceLocale.Require(S(p,"language"));var oldLocale=data.Settings;data.Settings=data.Settings with{Language=locale};try{store.Save(data);}catch{data.Settings=oldLocale;throw;}LocalLanguageChoice.Save(dataPath,locale);nativeLanguage=locale;cloud.UiLanguage=locale;return AccessBootstrap();
            case "homeSettings": var homeCards=p.GetProperty("cards").Deserialize<string[]>()??throw new ArgumentException("Preferenze non valide.");if(homeCards.Length>4||homeCards.Distinct(StringComparer.Ordinal).Count()!=homeCards.Length||homeCards.Any(card=>card is not("archives" or "activity" or "tickets" or "notifications")))throw new ArgumentException("Preferenze non valide.");var previousHome=data.Settings;data.Settings=data.Settings with{HomeCards=homeCards};try{store.Save(data);}catch{data.Settings=previousHome;throw;}return Snapshot();
            case "preferenceSyncSettings": var previousSyncSettings=data.Settings;data.Settings=data.Settings with{SyncLanguage=p.GetProperty("language").GetBoolean(),SyncMusic=p.GetProperty("music").GetBoolean()};try{store.Save(data);}catch{data.Settings=previousSyncSettings;throw;}return Snapshot();
            case "resourceSettings":
                var previousResources=data.Settings;data.Settings=data.Settings with{MeteredConnection=p.GetProperty("meteredConnection").GetBoolean(),ResourceSaving=p.GetProperty("resourceSaving").GetBoolean()};
                try{store.Save(data);}catch{data.Settings=previousResources;throw;}
                if(previousResources.MeteredConnection&&!data.Settings.MeteredConnection)foreach(var pendingEdit in editBuffers.Values.Where(edit=>edit.Dirty&&edit.AutoUpload).ToArray()){pendingEdit.MeteredPaused=false;ScheduleExternalSave(pendingEdit);}return Snapshot();
            case "lifecycleSettings":
                var previousLifecycle=data.Settings;data.Settings=data.Settings with { CloseToTray=p.GetProperty("closeToTray").GetBoolean(), CheckUpdatesOnStartup=p.GetProperty("checkUpdatesOnStartup").GetBoolean(), CheckUpdatesDaily=p.GetProperty("checkUpdatesDaily").GetBoolean() };
                try{store.Save(data);}catch{data.Settings=previousLifecycle;throw;}return Snapshot();
            case "settings":
                var settings = p.Deserialize<UserSettings>(ProfileStore.Json) ?? new();
                data.Settings = SequencePolicy.Apply(settings with { AutoLockMinutes = Math.Clamp(I(p, "autoLockMinutes", 10), 1, 120), StartupVolume=data.Settings.StartupVolume, ShutdownVolume=data.Settings.ShutdownVolume, RecordingRetentionDays = data.Settings.RecordingRetentionDays, RequireAppAuthentication=data.Settings.RequireAppAuthentication, AssistantApiEnabled=data.Settings.AssistantApiEnabled, AppMusicEnabled=data.Settings.AppMusicEnabled, AppMusicVolume=data.Settings.AppMusicVolume, ExternalEditor=data.Settings.ExternalEditor, EditorPreference=data.Settings.EditorPreference, CloseToTray=data.Settings.CloseToTray, CheckUpdatesOnStartup=data.Settings.CheckUpdatesOnStartup, CheckUpdatesDaily=data.Settings.CheckUpdatesDaily, Language=data.Settings.Language, MeteredConnection=data.Settings.MeteredConnection, ResourceSaving=data.Settings.ResourceSaving,SyncLanguage=data.Settings.SyncLanguage,SyncMusic=data.Settings.SyncMusic,HomeCards=data.Settings.HomeCards }); store.Save(data); return Snapshot();
            case "vaultUnlock":
                if (helloBusy) throw new InvalidOperationException("Completa prima la richiesta Windows Hello.");
                return await CloudAction("accountLogin",p);
            case "vaultLock": if (helloBusy) throw new InvalidOperationException("Completa prima la richiesta Windows Hello."); StopAllRecordings(); StopBundledAgent(); ClearNativePlainExport(); LockVault(); return Snapshot();
            case "helloEnable": return await EnableHello();
            case "helloUnlock": return await UnlockHello();
            case "helloDisable":
                await VerifyLocalIdentity("Disabilita Windows Hello");
                if(!Confirm("Disabilitare Windows Hello su questo PC? Senza Hello dovrai essere online per accedere con il tuo account e la 2FA."))return Snapshot();
                AccountUnlocks.ConfirmHello(cloud.State.Owner,"");
                await Task.Run(()=>vault.DisableWindowsHelloForUnlockedVault());return Snapshot();
            case "vaultGenerate":
                if (keyGenerationBusy) throw new InvalidOperationException("Una generazione e gia in corso.");
                keyGenerationBusy = true;
                try { await Task.Run(() => KeyGeneration.Generate(vault, S(p, "name"), S(p, "algorithm", "ed25519"))); } finally { keyGenerationBusy = false; }
                return Snapshot();
            case "vaultRename": await Task.Run(() => vault.UpdateMetadata(S(p, "keyId"), S(p, "name"), S(p, "comment"))); return Snapshot();
            case "agentList": return await Task.Run(() => AgentKeys.List(S(p, "kind", "openssh-agent")));
            case "agentCopyFingerprint": Clipboard.SetText(S(p, "fingerprint")); return null;
            case "agentLoadSmartcard":
                var provider = OpenFile("Seleziona il modulo PKCS#11 del produttore (.dll)"); if (provider == null) return null;
                if (!Confirm("Caricare tramite l'agent il modulo PKCS#11 selezionato?\n" + provider + "\nUsa esclusivamente il driver originale del tuo token. L'app non modifica i servizi Windows.")) return null;
                var pin = Secret("PIN smart card / HSM", "Il PIN viene trasmesso all'agent e non viene salvato. Il token puo limitare i tentativi errati.");
                await Task.Run(() => AgentKeys.LoadSmartcard(S(p, "kind", "openssh-agent"), provider, pin)); return null;
            case "certificateRevoke": case "certificateKrlExport": case "certificateKrlQuery": return await CertificateRevocationAction(action, p);
            case "certificateImport":
                var certPath = OpenFile("Seleziona il certificato utente OpenSSH (-cert.pub)"); if (certPath == null) return Snapshot();
                if (new FileInfo(certPath).Length > 1024 * 1024) throw new ArgumentException("Certificato troppo grande.");
                var certPassphrase = Secret("Passphrase della chiave", "Passphrase della chiave associata, vuoto se assente.");
                vault.AttachCertificate(S(p, "keyId"), File.ReadAllText(certPath), certPassphrase); return Snapshot();
            case "certificateIssue":
                if (!Confirm("Firmare un certificato SSH con la CA selezionata? Il server deve gia fidarsi della relativa chiave pubblica. Nessuna fiducia viene configurata automaticamente.")) return Snapshot();
                var targetPass = Secret("Passphrase chiave utente", "Passphrase della chiave da certificare, vuoto se assente.");
                var caPass = Secret("Passphrase chiave CA", "Passphrase della chiave CA selezionata, vuoto se assente.");
                await Task.Run(() => vault.IssueCertificate(S(p, "keyId"), S(p, "caId"), S(p, "principal"), I(p, "hours", 1), targetPass, caPass)); return Snapshot();
            case "certificateRemove":
                if (Confirm("Rimuovere il certificato associato? La chiave privata resta nel deposito; le connessioni successive useranno la chiave ordinaria.")) vault.RemoveCertificate(S(p, "keyId")); return Snapshot();
            case "certificateExport":
                var certificateInfo = vault.List().Single(k => k.Id == S(p, "keyId")).Certificate ?? throw new InvalidOperationException("Certificato non associato.");
                var certificatePath = SaveFile("Esporta il certificato pubblico SSH", "relay-key-cert.pub");
                if (certificatePath != null) AtomicFile.Write(certificatePath, System.Text.Encoding.UTF8.GetBytes(certificateInfo.Text + "\n")); return null;
            case "vaultExportPrivate":
                if(!vault.IsUnlocked)throw new InvalidOperationException("Sblocca prima l’archivio.");
                var exportInfo=vault.List().Single(k=>k.Id==S(p,"keyId"));
                await VerifyLocalIdentity("Esporta chiave privata / "+exportInfo.Name);
                var exportName=string.Concat(exportInfo.Name.Where(c=>!Path.GetInvalidFileNameChars().Contains(c))).Trim();if(exportName.Length==0)exportName="relay-key";
                var privateExportPath=SaveFile("Esporta la chiave privata: conserva il file in una cartella riservata",exportName+".key");if(privateExportPath==null)return new{exported=false};
                if(!vault.IsUnlocked)throw new InvalidOperationException("Archivio bloccato durante la verifica.");
                var exportedBytes=vault.Read(exportInfo.Id);try{PrivateKeyExport.Write(privateExportPath,exportedBytes);}finally{CryptographicOperations.ZeroMemory(exportedBytes);}
                return new{exported=true,fileName=Path.GetFileName(privateExportPath)};
            case "vaultRevealPrivate": case "vaultCopyPrivate":
                if(!vault.IsUnlocked)throw new InvalidOperationException("Sblocca prima l’archivio.");
                var privateId=S(p,"keyId");var privateInfo=vault.List().Single(k=>k.Id==privateId);
                await VerifyLocalIdentity((action=="vaultCopyPrivate"?"Copia":"Mostra")+" chiave privata / "+privateInfo.Name);
                if(!vault.IsUnlocked)throw new InvalidOperationException("Archivio bloccato durante la verifica.");
                var privateBytes=vault.Read(privateId);
                try{
                    var privateText=new System.Text.UTF8Encoding(false,true).GetString(privateBytes);
                    if(privateText.Length>1048576||privateText.Contains('\0'))throw new InvalidOperationException("Il formato binario non può essere mostrato come testo. Usa la conversione PPK/OpenSSH.");
                    if(action=="vaultCopyPrivate"){System.Windows.Clipboard.SetText(privateText);return new{copied=true};}
                    return new{text=privateText};
                }finally{CryptographicOperations.ZeroMemory(privateBytes);}
            case "vaultCopyPublic":
                var publicKey = vault.List().Single(k => k.Id == S(p, "keyId")).PublicKey;
                if (publicKey.Length == 0) throw new InvalidOperationException("Reimporta la chiave per calcolare i metadati pubblici.");
                Clipboard.SetText(publicKey); return null;
            case "vaultExportPublic":
                var publicInfo = vault.List().Single(k => k.Id == S(p, "keyId"));
                if (publicInfo.PublicKey.Length == 0) throw new InvalidOperationException("Reimporta la chiave per calcolare i metadati pubblici.");
                var publicPath = SaveFile("Esporta la chiave pubblica (.pub)", "relay-key.pub");
                if (publicPath != null) AtomicFile.Write(publicPath, System.Text.Encoding.UTF8.GetBytes(publicInfo.PublicKey + "\n")); return null;
            case "conversionStatus": case "conversionCancel": case "conversionStart": case "conversionOpen": case "conversionFinish": return await KeyConversionAction(action,p);
            case "vaultSelectImport":
                if (!vault.IsUnlocked) throw new InvalidOperationException("Sblocca prima l'archivio chiavi.");
                var selectedFile = OpenFile("Seleziona la chiave privata SSH da importare"); if (selectedFile == null) return null;
                if (new FileInfo(selectedFile).Length > 1024 * 1024) throw new ArgumentException("La chiave supera 1 MiB.");
                pendingKeyImport = (Guid.NewGuid().ToString("N"), selectedFile, DateTime.UtcNow.AddMinutes(10));
                return new { selectionId = pendingKeyImport.Value.Id, fileName = Path.GetFileName(selectedFile), name = Path.GetFileNameWithoutExtension(selectedFile) };
            case "vaultImportNative": return await ImportNativeVault(p);
            case "vaultImport":
                if (!vault.IsUnlocked) throw new InvalidOperationException("Sblocca prima l'archivio chiavi.");
                if (pendingKeyImport is not {} selection || selection.Id != S(p, "selectionId") || DateTime.UtcNow > selection.Expires) throw new InvalidOperationException("Seleziona nuovamente il file della chiave.");
                var importName = S(p, "name").Trim(); var importComment = S(p, "comment");
                if (importName.Length is < 1 or > 100 || importComment.Length > 1000) throw new ArgumentException("Indica un nome da 1 a 100 caratteri e un commento di massimo 1000 caratteri.");
                var file = selection.Path;
                if (new FileInfo(file).Length > 1024 * 1024) throw new ArgumentException("La chiave supera 1 MiB.");
                var pass = Secret("Passphrase chiave", "Passphrase del file, vuoto se assente. Non viene salvata."); var bytes = File.ReadAllBytes(file);
                try { await Task.Run(() => vault.Import(importName, bytes, pass, importComment)); pendingKeyImport = null; } finally { CryptographicOperations.ZeroMemory(bytes); } return Snapshot();
            case "vaultBackup": var backup = SaveFile("Backup cifrato chiavi SSH", "relay-keys.vault"); if (backup != null) vault.Backup(backup); return null;
            case "vaultDelete":
                var removingKey = vault.List().Single(k => k.Id == S(p, "keyId"));
                var linkedProfiles = data.Profiles.Where(profile => profile.KeyId == removingKey.Id).Select(profile => profile.Name).ToArray();
                var linkedWarning = linkedProfiles.Length == 0 ? "" : "\n\nProfili associati: " + string.Join(", ", linkedProfiles) + ". Non potranno collegarsi con questa chiave finché non ne assegni un'altra.";
                if (!Confirm("Eliminare definitivamente la chiave «" + removingKey.Name + "» dall'archivio cifrato?\nI file originali e le chiavi sui server non vengono eliminati. Verifica di avere un backup." + linkedWarning)) throw new OperationCanceledException();
                vault.Delete(removingKey.Id); StopBundledAgent(); return Snapshot();
            case "hostForget": if (!Confirm("Dimenticare le impronte verificate di " + S(p, "host") + ":" + I(p, "port", 22) + "?\nAlla prossima connessione sarà necessaria una nuova verifica dell'identità del server.")) throw new OperationCanceledException(); hosts.Forget(S(p, "host"), I(p, "port", 22)); return Snapshot();
            case "recoveryPolicy": return SetRecoveryPolicy(p);
            case "recoverProfile": return await RecoverProfile(p);
            case "connect":
                var connectGeneration=authenticationGeneration;RequireOperationSession(connectGeneration);
                if (updating) throw new InvalidOperationException("Attendi la fine dell’aggiornamento.");
                var automatic = p.TryGetProperty("automatic", out var auto) && auto.GetBoolean();
                var selected = data.Profiles.FirstOrDefault(x => x.Id == S(p, "profileId")) ?? nativeTasks.GetValueOrDefault(S(p,"profileId")) ?? throw new ArgumentException("Profilo non trovato.");
                if (automatic && selected.Protocol != "ssh") throw new InvalidOperationException("Riconnessione automatica disponibile soltanto per terminali SSH.");
                if (p.TryGetProperty("filesOnly", out var filesOnly) && filesOnly.GetBoolean() && selected.Protocol is "ssh" or "sftp") selected = selected with { Protocol = "sftp", StartupMacro=null };
                var sid = S(p, "sessionId"); if (!Guid.TryParse(sid, out _) || sessions.ContainsKey(sid) || desktopSessions.ContainsKey(sid) || localSessions.ContainsKey(sid) || !connectingSessions.Add(sid)) throw new ArgumentException("Identificatore sessione non valido.");
                try
                {
                    if(selected.Storage.Browser)throw new IOException("Apri il browser storage dalla scheda dedicata.");
                    if(selected.Protocol=="native"&&selected.Native.Kind=="browser")return await ConnectBrowser(selected,sid);
                    var startupMacro=PrepareStartupMacro(selected,automatic);
                    if(selected.Protocol is "local" or "native")return await ConnectLocal(selected.Protocol=="local"?StartupMacros.LocalProfile(selected,startupMacro):selected,sid,p,startupMacro);
                    if (selected.Protocol is "rdp" or "vnc") return await ConnectDesktop(selected, sid);
                    if(!automatic)await RunHooks(selected,"before-connect",required:true);RequireOperationSession(connectGeneration);
                    var session = await SshRoutes.CreateAsync(selected, data.Profiles, async (profile, endpointHost, endpointPort) => {RequireOperationSession(connectGeneration);var created=await CreateAuthenticatedSession(profile, endpointHost, endpointPort, profile.Id == selected.Id ? sid : Guid.NewGuid().ToString("N"), automatic);try{RequireOperationSession(connectGeneration);return created;}catch{created.Dispose();throw;}});
                    try{RequireOperationSession(connectGeneration);}catch{session.Dispose();throw;}
                    WireManagedSession(session); sessions.Add(session.Id, session);
                    try {
                        await Task.Run(() => session.ConnectAsync(I(p, "cols", 100), I(p, "rows", 30)));RequireOperationSession(connectGeneration);
                        if(!automatic){hookedSessions[sid]=selected;await RunHooks(selected,"after-connect",session);RequireOperationSession(connectGeneration);await RunStartupMacro(session,startupMacro);RequireOperationSession(connectGeneration);}
                        if (selected.Protocol == "sftp") RebindFileQueue(session.Profile.Id, session.Id);
                        return new { sessionId = session.Id, filesAvailable = session.FilesAvailable };
                    } catch {hookedSessions.TryRemove(sid,out _);sessions.Remove(session.Id);sessionRouteSnapshots.Remove(session.Id);brokenSessions.Remove(session.Id);session.Dispose();throw;}
                }
                finally { connectingSessions.Remove(sid); }
            case "input": if(localSessions.TryGetValue(S(p,"sessionId"),out var localInput))localInput.Write(S(p,"data"));else await Session(p).WriteInputAsync(S(p, "data")); return null;
            case "resize": if(localSessions.TryGetValue(S(p,"sessionId"),out var localResize))await localResize.Resize(I(p,"cols",100),I(p,"rows",30));else await Session(p).ResizeTerminalAsync(I(p, "cols", 100), I(p, "rows", 30)); return null;
            case "disconnect": if(storageSessions.ContainsKey(S(p,"sessionId"))||nativeStorageParents.ContainsKey(S(p,"sessionId"))){if(advancedNativeChildren.Contains(S(p,"sessionId"))&&(fileJobs.Values.Any(j=>j.SessionId==S(p,"sessionId")&&j.Cancellation!=null)||editBuffers.Values.Any(e=>e.SessionId==S(p,"sessionId")&&!e.ExternalOnly)))throw new IOException("Metti in pausa la coda e chiudi gli editor prima di chiudere SFTP.");PrepareCloseSessionEditors(S(p,"sessionId"));await CloseStorage(S(p,"sessionId"));return null;}if(localSessions.ContainsKey(S(p,"sessionId"))){await CloseLocal(S(p,"sessionId"));return null;}if (transferActive || QueueBusy || EditorsBusy || ExportBusy) throw new InvalidOperationException("Attendi la fine del trasferimento."); PrepareCloseSessionEditors(S(p,"sessionId")); DisconnectDesktop(S(p, "sessionId")); await DisconnectWithHooks(S(p,"sessionId"),!p.TryGetProperty("automatic",out var autoDisconnect)||!autoDisconnect.GetBoolean()); return null;
            case "sftpList": return await Session(p).ListAsync(S(p, "path", "."));
            case "transfer": return await Transfer(p);
            case "cancelTransfer": transferCancellation?.Cancel(); return null;
            case "tunnelStatus": return Session(p).TunnelStatus();
            case "saveTunnel":
                var tunnelSession = Session(p); var tunnelRule = p.GetProperty("rule").Deserialize<TunnelRule>(ProfileStore.Json) ?? throw new ArgumentException("Regola non valida."); tunnelRule.Validate();
                if (sessions.Values.Any(s => s.Profile.Id == tunnelSession.Profile.Id && s.TunnelActive(tunnelRule.Id))) throw new InvalidOperationException("Disattiva il tunnel in tutte le sessioni del profilo prima di modificarlo.");
                var tunnelProfile = data.Profiles.Single(x => x.Id == tunnelSession.Profile.Id);
                var tunnelRules = tunnelProfile.Tunnels.Where(t => t.Id != tunnelRule.Id).Append(tunnelRule).ToList();
                var oldTunnelRules = tunnelProfile.Tunnels; tunnelProfile.Tunnels = tunnelRules;
                try { store.Save(data); } catch { tunnelProfile.Tunnels = oldTunnelRules; throw; }
                foreach (var openSession in sessions.Values.Where(s => s.Profile.Id == tunnelProfile.Id)) openSession.Profile.Tunnels = tunnelRules;
                return tunnelSession.TunnelStatus();
            case "deleteTunnel":
                var deletingSession = Session(p); var deletingId = S(p, "ruleId");
                if (!Confirm("Eliminare questa regola tunnel? Se attiva verrà arrestata.")) throw new OperationCanceledException();
                var deletingRule = deletingSession.Profile.Tunnels.Single(t => t.Id == deletingId);
                foreach (var openSession in sessions.Values.Where(s => s.Profile.Id == deletingSession.Profile.Id)) openSession.SetTunnel(deletingRule, false);
                var deletingProfile = data.Profiles.Single(x => x.Id == deletingSession.Profile.Id);
                var priorRules = deletingProfile.Tunnels; deletingProfile.Tunnels = deletingProfile.Tunnels.Where(t => t.Id != deletingId).ToList();
                try { store.Save(data); } catch { deletingProfile.Tunnels = priorRules; throw; }
                foreach (var openSession in sessions.Values.Where(s => s.Profile.Id == deletingProfile.Id)) openSession.Profile.Tunnels = deletingProfile.Tunnels;
                return deletingSession.TunnelStatus();
            case "toggleTunnel": var ts = Session(p); var rule = ts.Profile.Tunnels.Single(t => t.Id == S(p, "ruleId")); await Task.Run(() => ts.SetTunnel(rule, p.GetProperty("enabled").GetBoolean())); return ts.TunnelStatus();
            case "updateTokenStatus": return new { saved = updateTokens.Exists };
            case "updateTokenSave":
                var githubToken = Secret("Token aggiornamenti GitHub", "Incolla il token con Contents: Read-only per sup3r93/unsc-relay-desktop. Sarà salvato cifrato per questo account Windows, separato dalle chiavi SSH.");
                if (string.IsNullOrWhiteSpace(githubToken)) throw new OperationCanceledException();
                updateTokens.Save(githubToken); updateToken = ""; update = null; return new { saved = true };
            case "updateTokenDelete":
                if (!Confirm("Rimuovere il token GitHub salvato? Profili e chiavi SSH restano invariati.")) throw new OperationCanceledException();
                updateTokens.Delete(); updateToken = ""; update = null; return new { saved = false };
            case "checkUpdates":
                if (updating) throw new InvalidOperationException("Aggiornamento già in corso.");
                return await CheckForUpdates();
            case "installUpdate":
                if(criticalStopping)throw new InvalidOperationException("Attendi l’arresto sicuro delle sessioni prima di installare.");
                if(checkingUpdates)throw new InvalidOperationException("Attendi la fine del controllo aggiornamenti.");
                if (KeyConversionBusy) throw new InvalidOperationException("Termina o annulla la conversione delle chiavi prima di aggiornare.");
                if (update == null) throw new InvalidOperationException("Controlla prima gli aggiornamenti.");
                if (transferActive || QueueBusy || EditorsBusy || ExportBusy || helloBusy || certificateRenewBusy || connectingSessions.Count > 0 || activeOperations != 1) throw new InvalidOperationException("Completa i trasferimenti, le connessioni e le operazioni in corso prima di aggiornare.");
                if (updating) throw new InvalidOperationException("Aggiornamento già in corso.");
                updating = true;
                try
                {
                    if (!await CloseSessionsForUpdate()) return null;
                    Emit("updateProgress", new { status = "Download dell’aggiornamento…", stage = "download", receivedBytes = 0 });
                    var installer = await new GitHubUpdates().DownloadAsync(update, updateToken, Path.Combine(dataPath, "Updates"), CancellationToken.None, progress => Emit("updateProgress", new { status = progress.Stage == "verify" ? "Verifica del pacchetto…" : "Download dell’aggiornamento…", stage=progress.Stage, progress.ReceivedBytes, progress.TotalBytes, progress.BytesPerSecond, progress.RemainingSeconds }));
                    try { InstallerSignature.Verify(installer, Path.Combine(AppContext.BaseDirectory, "Assets", "Publisher.cer")); }
                    catch { File.Delete(installer); throw; }
                    if (sessions.Count + desktopSessions.Count + localSessions.Count > 0 || transferActive || QueueBusy || EditorsBusy || ExportBusy || helloBusy || certificateRenewBusy) throw new InvalidOperationException("Chiudi le sessioni e completa le operazioni prima di installare.");
                    var installRoot = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.FullName;
                    Emit("updateProgress", new { status = "Pronto per installare", stage = "ready" });
                    if (new UpdateConfirmation(this, update.Version, installRoot).ShowDialog() == true)
                    {
                        store.Save(data); UpdateBackup.Create(rootDataPath, CurrentVersion.ToString());
                        if (!File.Exists(Path.Combine(installRoot, "UNSC Relay.exe"))) throw new InvalidOperationException("Avvia l’aggiornamento dalla copia installata di UNSC Relay.");
                        var start = new ProcessStartInfo(installer) { UseShellExecute = false };
                        foreach (var argument in new[] { "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/RELAYUPDATE=1", "/DIR=" + installRoot, "/WAITFORPID=" + Environment.ProcessId }) start.ArgumentList.Add(argument);
                        Emit("updateProgress", new { status = "Apertura dell’installer…", stage = "launch" });
                        Process.Start(start); updateClosing = true; Close();
                    }
                    return null;
                }
                finally { updating = false; Emit("updateProgress", new { status = "" }); }
            case "openData": Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { dataPath }, UseShellExecute = false }); return null;
            case "exit": explicitExit=true;Close(); return null;
            default: throw new ArgumentException("Azione non disponibile.");
        }
    }
    bool transferActive;
    async Task<SshSession> CreateAuthenticatedSession(ConnectionProfile profile, string? endpointHost, int endpointPort, string id, bool automatic = false)
    {
        if(CriticalUpdateRequired)throw new OperationCanceledException("Aggiornamento critico richiesto.");
        var method = profile.Authentication == "auto" ? profile.KeyId.Length > 0 ? "key" : "password" : profile.Authentication;
        var info = method == "key" ? vault.List().SingleOrDefault(k => k.Id == profile.KeyId) ?? throw new InvalidOperationException("La chiave assegnata a «" + profile.Name + "» non è disponibile. Sblocca l’archivio o modifica il profilo.") : null;
        if(info?.Kind=="ssh-native-key")throw new IOException("Questa chiave richiede una sessione OpenSSH nativa. Seleziona OpenSSH nella scelta del tipo di connessione.");
        if (info?.Certificate != null) vault.ValidateCertificateAccess(info.Certificate, profile.Username);
        var key = info == null ? null : vault.Read(info.Id);
        try
        {
            var credential = method == "password" ? ConnectionPassword(profile, automatic) : method == "key" ? connectionPassphrases.GetValueOrDefault(profile.Id) ?? "" : "";
            return await Task.Run(() => new SshSession(profile, key == null ? credential : "", key, key != null ? credential : "", hosts, automatic ? _ => false : Trust, text => automatic ? throw new InvalidOperationException("Autenticazione interattiva richiesta: riconnetti manualmente.") : Secret("Autenticazione SSH / " + profile.Name, text), info?.Certificate?.Text ?? "", endpointHost, endpointPort) { Id = id });
        }
        catch (Renci.SshNet.Common.SshPassPhraseNullOrEmptyException ex) { throw new InvalidOperationException("La chiave SSH è protetta. Inserisci la sua passphrase nella scheda Autenticazione della connessione.", ex); }
        finally { if (key != null) CryptographicOperations.ZeroMemory(key); }
    }
    (string Id, string Path, DateTime Expires)? pendingKeyImport;
    CancellationTokenSource? transferCancellation;
    async Task<object?> Transfer(JsonElement p)
    {
        var generation=authenticationGeneration;RequireOperationSession(generation);var operationToken=accountOperationStop.Token;
        if (transferActive || QueueBusy || EditorsBusy || ExportBusy) throw new InvalidOperationException("Attendi il trasferimento in corso.");
        var s = Session(p); var upload = S(p, "direction") == "upload"; var remote = S(p, "remote");
        var local = upload ? OpenFile("Seleziona il file da caricare") : SaveFile("Salva il file remoto", remote.Split('/').Last()); if (local == null) return null;
        if (upload) { remote = remote.TrimEnd('/') + "/" + Path.GetFileName(local); if (!Confirm($"Caricare in {remote}? Un file esistente verrà sostituito.")) return null; }
        if(upload)ConfirmProductionOperation(s.Profile,"Caricamento di file",resource:remote);RequireOperationSession(generation);transferActive = true; var last = DateTime.MinValue; transferCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
        try { await s.TransferAsync(local, remote, upload, false, (done, total) => { if (DateTime.UtcNow - last > TimeSpan.FromMilliseconds(200)) { last = DateTime.UtcNow; Emit("transfer", new { name = Path.GetFileName(local), progress = total > 0 ? done * 100.0 / total : 0, status = "Trasferimento" }); } }, transferCancellation.Token);RequireOperationSession(generation); Emit("transfer", new { name = Path.GetFileName(local), progress = 100, status = "Completato", finished = true }); }
        catch { Emit("transfer", new { name = Path.GetFileName(local), progress = 0, status = transferCancellation.IsCancellationRequested ? "Annullato" : "Non completato", finished = true }); throw; }
        finally { transferActive = false; transferCancellation.Dispose(); transferCancellation = null; } return null;
    }
    async void OnRelayClosing(object? sender, CancelEventArgs e)
    {
        if (exitAllowed) return; e.Cancel = true;var requestedExit=explicitExit;explicitExit=false;
        if(!requestedExit&&!updateClosing&&!smokeTest&&data.Settings.CloseToTray&&backgroundIcon!=null){Hide();return;} if (closing) return; if(certificateRenewBusy){MessageBox.Show(this,"Attendi la fine della richiesta di certificato.","UNSC Relay");return;}
        if(nativePresentationMode)return;
        if(accessFinalWatch!=null&&!consoleReady&&accessFinalWatch.Elapsed.TotalSeconds<10)return;
        if(!updateClosing&&startupSequencePlaying&&startupSequenceWatch.Elapsed<TimeSpan.FromSeconds(20))return;
        if (updating && !updateClosing) { MessageBox.Show(this, "Attendi il completamento del download prima di chiudere.", "UNSC Relay"); return; }
        if (connectingSessions.Count > 0) { MessageBox.Show(this, "Completa la connessione SSH prima di chiudere.", "UNSC Relay"); return; }
        if(conversionOperation){MessageBox.Show(this,"Attendi l’operazione sulla chiave prima di chiudere.","UNSC Relay");return;}
        if (accessBusy) return;
        if (helloBusy) { MessageBox.Show(this, "Completa o annulla la richiesta Windows Hello prima di chiudere.", "UNSC Relay"); return; }
        if(!updateClosing&&!smokeTest){if(!ReviewExit())return;}
        if(ExitReviewSnapshot().Busy)return;
        CancelKeyConversion();
        closing = true; assistantStop.Cancel();
        try
        {
            backgroundUpdateTimer.Stop();backgroundIcon?.Dispose();backgroundIcon=null;
            cloudTimer.Stop();erasureMonitor.Stop();cloudLifetime.Cancel();
            await CloseAllStorage();
            await CloseAllLocal(!CriticalUpdateRequired&&!updateClosing);
            foreach (var id in editBuffers.Keys.ToArray()) CloseEditor(id);
            foreach (var id in desktopSessions.Keys.ToArray()) DisconnectDesktop(id);
            foreach(var id in sessions.Keys.ToArray())await DisconnectWithHooks(id,!CriticalUpdateRequired&&!updateClosing); sessions.Clear(); store.Save(data); StopAllRecordings(); StopBundledAgent(); ClearNativePlainExport(); LockVault(); updateToken = ""; lockTimer.Stop();
            connectionPasswords.Clear();
            connectionPassphrases.Clear();
            shutdownDone = new(TaskCreationOptions.RunContinuationsAsynchronously); shutdownSequenceWatch=Stopwatch.StartNew(); Emit("shutdown", new { enabled = !updateClosing, volume = data.Settings.ShutdownVolume });
            if (!updateClosing) await Task.WhenAny(shutdownDone.Task, Task.Delay(TimeSpan.FromSeconds(SequencePolicy.DurationSeconds+3)));
        }
        finally { FinishShutdownCheck(); FinishSequenceCheck(); exitAllowed = true; _ = Dispatcher.BeginInvoke(()=>{if(IsLoaded){CloseDetachedWindows();CloseExportWindows();Browser.Dispose();Close();}}); }
    }
}
