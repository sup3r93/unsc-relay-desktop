using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    Func<string,bool>? diagnosticConfirm;
    Func<string,string,string?>? diagnosticSave;
    void CheckConfigLogsBridge()
    {
        var checks=new Dictionary<string,bool>();string? error=null;
        JsonElement P(object value)=>JsonSerializer.SerializeToElement(value,ProfileStore.Json);
        bool Reject(Action f){try{f();return false;}catch(Exception ex)when(ex is IOException or OperationCanceledException or InvalidOperationException or CryptographicException){return true;}}
        try
        {
            var root=Path.Combine(AppContext.BaseDirectory,".config-log-fixtures");Directory.CreateDirectory(root);
            var config=Path.Combine(root,"config");File.WriteAllText(config,"# fixture original\nHost personal\n User fixture\n");
            var original=File.ReadAllBytes(config);var profile=new ConnectionProfile{Name="native-lab",Host="192.0.2.50",Username="fixture"};data.Profiles.Add(profile);
            int confirmations=0;diagnosticSave=(_,_)=>config;diagnosticConfirm=_=>{confirmations++;return false;};
            var plan=P(WorkflowAction("sshConfigWritePreview",P(new{profileIds=new[]{profile.Id}}))!);var id=plan.GetProperty("id").GetString();
            checks["configPreviewDoesNotWrite"]=File.ReadAllBytes(config).SequenceEqual(original)&&confirmations==0;
            checks["configCancelKeepsOriginal"]=Reject(()=>WorkflowAction("sshConfigWriteApply",P(new{id})))&&File.ReadAllBytes(config).SequenceEqual(original);
            diagnosticConfirm=_=>{confirmations++;return true;};var applied=P(WorkflowAction("sshConfigWriteApply",P(new{id}))!);
            checks["configConfirmedCreatesExactBackup"]=applied.GetProperty("saved").GetBoolean()&&File.ReadAllBytes(applied.GetProperty("backup").GetString()!).SequenceEqual(original);
            checks["configPlanCannotReplay"]=Reject(()=>WorkflowAction("sshConfigWriteApply",P(new{id})));
            diagnosticSave=(_,_)=>Path.Combine(dataPath,"profiles.json");checks["configCannotOverwriteAppData"]=Reject(()=>WorkflowAction("sshConfigWritePreview",P(new{profileIds=new[]{profile.Id}})));
            vault.Unlock("Native config log fixture master 2026!");Directory.CreateDirectory(RecordingsPath);var material=vault.RecordingKey();var recId=Guid.NewGuid().ToString("N");var recPath=RecordingPath(recId);
            try{using(var rec=new SessionRecording(recPath,material.Id,material.Key,"fixture")){rec.Write(Encoding.UTF8.GetBytes("\x1b[31mfixture native\x1b[0m\r\n"));}}finally{CryptographicOperations.ZeroMemory(material.Key);}
            var encrypted=File.ReadAllBytes(recPath);var log=Path.Combine(root,"native-"+Guid.NewGuid().ToString("N")+".txt");diagnosticSave=(_,_)=>log;
            diagnosticConfirm=_=>false;checks["logCancelCreatesNoFile"]=Reject(()=>RecordingAction("recordingExportText",P(new{recordingId=recId})))&&!File.Exists(log);
            diagnosticConfirm=_=>true;var exported=P(RecordingAction("recordingExportText",P(new{recordingId=recId}))!);
            checks["logNativeExportSanitized"]=exported.GetProperty("saved").GetBoolean()&&File.ReadAllText(log)=="fixture native\r\n";
            checks["logEncryptedOriginalUnchanged"]=File.ReadAllBytes(recPath).SequenceEqual(encrypted);
            checks["logNoOverwrite"]=Reject(()=>RecordingAction("recordingExportText",P(new{recordingId=recId})))&&File.ReadAllText(log)=="fixture native\r\n";
            var badId=Guid.NewGuid().ToString("N");var bad=encrypted.ToArray();bad[^1]^=1;File.WriteAllBytes(RecordingPath(badId),bad);var tamperedLog=Path.Combine(root,"tampered.txt");diagnosticSave=(_,_)=>tamperedLog;
            checks["tamperedRecordingNeverExported"]=Reject(()=>RecordingAction("recordingExportText",P(new{recordingId=badId})))&&!File.Exists(tamperedLog);
            vault.Lock();checks["lockedVaultBlocksLogExport"]=Reject(()=>RecordingAction("recordingExportText",P(new{recordingId=recId})));
        }
        catch(Exception ex){error=ex.ToString();}
        finally{diagnosticConfirm=null;diagnosticSave=null;sshConfigEdit=null;}
        var success=error==null&&checks.Count==11&&checks.Values.All(v=>v);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"config-logs-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error},ProfileStore.Json));CompleteSmoke(success);
    }
}
