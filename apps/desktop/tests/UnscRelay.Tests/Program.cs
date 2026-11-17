if(args.Length==1&&args[0]=="--security-review")return SecurityReviewChecks.Run();
if(args.Length==1&&args[0]=="--native-recovery")return await NativeRecoveryChecks.Run();
if(args.Length==1&&args[0]=="--account-transport")return await AccountTransportChecks.Run();
if(args.Length==4&&args[0]=="--native-control")return await NativeControlChecks.Run(args[1],args[2],int.Parse(args[3]));
if(args.Length==3&&args[0]=="--storage-untrusted-tls")return await StorageFilesChecks.RejectUntrustedTls(args[1],int.Parse(args[2]));
if(args.Length==5&&args[0]=="--storage-files")return await StorageFilesChecks.Run(args[1],args[2],int.Parse(args[3]),args[4]);
if(args.Length==4&&args[0]=="--native-hook-integration")return await NativeHookIntegrationChecks.Run(args[1],args[2],int.Parse(args[3]));
if(args.Length==1&&args[0]=="--native-hooks-only")return await NativeHookChecks.Run();
if(args.Length==1&&args[0]=="--native-host-certificates-only")return NativeHostCertificateChecks.Run();
if(args.Length==1&&args[0]=="--key-parsing-only")return KeyParsingChecks.Run();
if(args.Length==1&&args[0]=="--account-unlock-only")return AccountUnlockChecks.Run();
if(args.Length==1&&args[0]=="--native-certificates-only")return NativeCertificateChecks.Run();
if(args.Length==1&&args[0]=="--transfer-window-only"){
 var checks=new[]{UnscRelay.Core.TransferWindow.Allowed(12*60,9*60,18*60),!UnscRelay.Core.TransferWindow.Allowed(18*60,9*60,18*60),UnscRelay.Core.TransferWindow.Allowed(23*60,22*60,6*60),UnscRelay.Core.TransferWindow.Allowed(5*60,22*60,6*60),!UnscRelay.Core.TransferWindow.Allowed(12*60,22*60,6*60),UnscRelay.Core.TransferWindow.Allowed(0,null,null)};
 if(checks.Any(x=>!x))return 1;using var cancellation=new CancellationTokenSource();cancellation.Cancel();try{var n=DateTime.Now.Hour*60+DateTime.Now.Minute;UnscRelay.Core.TransferWindow.Wait((n+1)%1440,(n+2)%1440,cancellation.Token);return 1;}catch(OperationCanceledException){}Console.WriteLine("PASS: daytime, overnight, boundary and cancellation transfer windows");return 0;
}
if(args.Length==1&&args[0]=="--cloud-sync-only")return CloudSyncChecks.Run();
if(args.Length==1&&args[0]=="--cloud-interop-only")return CloudSyncChecks.Interop();
if(args.Length==2&&args[0]=="--terminal-only")return await TerminalChecks.Run(int.Parse(args[1]));
if(args.Length==1&&args[0]=="--startup-macros-only")return await StartupMacroChecks.Run();
if(args.Length==1&&args[0]=="--assistant-policy-only")return await AssistantChecks.Run();
﻿if(args.Length>=3&&args[0]=="--native-integration-only")return await NativeIntegrationChecks.Run(args[1],args[2],args.Length>3?int.Parse(args[3]):0);
if(args.Length==2&&args[0]=="--native-pack-only")return await NativePackChecks.Run(args[1]);
if(args.Length==2&&args[0]=="--large-rsa-only")return await LargeRsaChecks.Run(int.Parse(args[1]));
if(args.Length==1&&args[0]=="--revocations-only")return await RevocationChecks.Run();
if(args.Length==1&&args[0]=="--local-sessions-only")return await LocalSessionChecks.Run();
if(args.Length==1&&args[0]=="--workspace-windows-only")return WorkspaceChecks.Run();
if(args.Length==1&&args[0]=="--explorer-only")return ExplorerChecks.Run();
if(args.Length==1&&args[0]=="--config-logs-only")return ConfigLogChecks.Run();
if(args.Length==2&&args[0]=="--conversion-only")return ConversionChecks.Run(args[1]);
if(args.Length==1&&args[0]=="--retention-only")return RetentionChecks.Run();
if(args.Length==1&&args[0]=="--folders-sync-only")return FolderSyncChecks.Run();
if(args.Length==1&&args[0]=="--sequences-only")return SequenceChecks.Run();
if(args.Length==3&&args[0]=="--hook-trace"){System.IO.File.AppendAllText(args[1],args[2]+"\n");return 0;}
if(args.Length>1&&args[0]=="--hook-helper"){System.IO.File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(args.Skip(2).ToArray()));return 0;}
if(args.Length==1&&args[0]=="--hook-fail")return 7;
if(args.Length==1&&args[0]=="--hook-delay"){await Task.Delay(5000);return 0;}
if(args.Length==1&&args[0]=="--hooks-only")return await HookChecks.Run();
if (args.Length == 1 && args[0] == "--recordings-only") return RecordingChecks.Run();
if (args.Length == 1 && args[0] == "--workflow-tools-only") return WorkflowToolsChecks.Run();
if (args.Length == 2 && args[0] == "--files-only") return await SftpAdvancedChecks.Run(int.Parse(args[1]));
if (args.Length == 1 && args[0] == "--key-metadata-only") return KeyMetadataChecks.RunIsolated();
if (args.Length == 1 && args[0] == "--passwords-only") return PasswordChecks.Run();
if (args.Length == 1 && args[0] == "--update-workflow-only") return await UpdateWorkflowChecks.Run();
if (args.Length == 1 && args[0] == "--published-update-only") return await PublishedUpdateChecks.Run();
if (args.Length == 1 && args[0] == "--saved-update-only") return await PublishedUpdateChecks.Run(useSavedToken: true);
if (args.Length == 4 && args[0] == "--routes-only") return await RouteChecks.Run(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
return await Checks.Run(args);
