using System.IO;
using System.Text.Json;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    readonly Dictionary<string,bool> sequenceChecks=[];
    System.Diagnostics.Stopwatch? sequenceCloseWatch;
    async void ProbeIntroClose()
    {
        await Task.Delay(600);if(exitAllowed)return;Close();sequenceChecks["introWindowCloseBlocked"]=!closing&&!exitAllowed;
    }
    async Task ProbeSequenceOutro()
    {
        sequenceChecks["introCompletedAfterFiveSeconds"]=SequencePolicy.CanFinish(startupSequenceWatch.Elapsed,false);
        var json=await Browser.CoreWebView2.ExecuteScriptAsync("({width:document.getElementById('sequence-video').videoWidth,height:document.getElementById('sequence-video').videoHeight,duration:document.getElementById('sequence-video').duration,volume:document.getElementById('sequence-video').volume,muted:document.getElementById('sequence-video').muted,controls:document.getElementById('sequence-video').controls,skip:!!document.getElementById('sequence-skip'),mute:!!document.getElementById('sequence-mute')})");
        var info=JsonSerializer.Deserialize<JsonElement>(json);
        sequenceChecks["nativeUltrawideMedia"]=info.GetProperty("width").GetInt32()==3440&&info.GetProperty("height").GetInt32()==1440&&Math.Abs(info.GetProperty("duration").GetDouble()-5)<.1;
        sequenceChecks["introUsesSavedLevel"]=Math.Abs(info.GetProperty("volume").GetDouble()-.23)<.001&&!info.GetProperty("muted").GetBoolean();
        sequenceChecks["noSkipMuteOrVideoControls"]=!info.GetProperty("controls").GetBoolean()&&!info.GetProperty("skip").GetBoolean()&&!info.GetProperty("mute").GetBoolean();
        sequenceCloseWatch=System.Diagnostics.Stopwatch.StartNew();Close();
        await Task.Delay(600);if(exitAllowed)return;
        var outroJson=await Browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('sequence-video').volume");
        sequenceChecks["outroUsesItsOwnSavedLevel"]=Math.Abs(double.Parse(outroJson,System.Globalization.CultureInfo.InvariantCulture)-.61)<.001;
        await Browser.CoreWebView2.ExecuteScriptAsync("window.chrome.webview.postMessage({id:'early-sequence-finish',action:'shutdownDone',payload:{failed:false}});true");
        await Task.Delay(150);sequenceChecks["earlyOutroCompletionIgnored"]=closing&&!exitAllowed;
    }
    void FinishSequenceCheck()
    {
        if(!sequenceSmokeTest)return;
        sequenceChecks["outroCompletedAfterFiveSeconds"]=sequenceCloseWatch?.Elapsed.TotalSeconds is >=4.8 and <8;
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"sequences-smoke-result.json"),JsonSerializer.Serialize(new{success=sequenceChecks.Count==8&&sequenceChecks.Values.All(v=>v),checks=sequenceChecks,seconds=sequenceCloseWatch?.Elapsed.TotalSeconds},ProfileStore.Json));
    }
}
