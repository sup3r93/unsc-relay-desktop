using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 sealed record ExitReviewState(int Ssh,int Desktop,int Local,int Native,int Storage,int Transfers,int InternalEdits,int ExternalEdits,bool Busy,bool Conversion);
 ExitReviewState ExitReviewSnapshot()=>new(sessions.Count,desktopSessions.Count,localSessions.Values.Count(s=>s.Running),nativeSessionProfiles.Count,storageSessions.Count,fileJobs.Values.Count(j=>j.Status is "In coda" or "Trasferimento" or "Pausa richiesta")+(transferActive?1:0),editBuffers.Values.Count(e=>!e.ExternalOnly&&e.Dirty),editBuffers.Values.Count(e=>e.ExternalOnly&&e.Dirty),transferActive||QueueBusy||EditorsBusy||ExportBusy||PlainExportBusy||cloudRunning||AccountErasureBusy,KeyConversionBusy);
 string ExitReviewText(ExitReviewState value)
 {
  var lines=new List<string>{NativeText("Controlla le attività prima di uscire."),""};
  foreach(var (label,count) in new[]{("Sessioni SSH/SFTP",value.Ssh),("Sessioni desktop remoto",value.Desktop),("Shell locali",value.Local),("Sessioni native",value.Native),("Browser storage",value.Storage),("Trasferimenti attivi o in coda",value.Transfers),("Editor integrati non salvati",value.InternalEdits),("Salvataggi esterni non caricati",value.ExternalEdits)})lines.Add(NativeText(label)+": "+count);
  lines.Add("");lines.Add(NativeText("Le sessioni aperte saranno chiuse, inclusi i programmi avviati nelle shell locali."));
  if(value.InternalEdits>0)lines.Add(NativeText("Le modifiche non salvate negli editor integrati saranno scartate."));
  if(value.ExternalEdits>0)lines.Add(NativeText("Alcuni salvataggi esterni non sono stati caricati sul server. Le copie locali restano sul PC e non vengono cancellate."));
  if(value.Conversion)lines.Add(NativeText("La conversione verrà abbandonata. PuTTYgen e i file esportati resteranno sul PC."));
  if(value.Busy)lines.Add(NativeText("Ci sono operazioni in corso. Torna al lavoro e attendi il completamento o annullale dai rispettivi pannelli."));
  return string.Join("\n",lines);
 }
 bool exitReviewOpen;
 bool ReviewExit()
 {
  if(exitReviewOpen)return false;exitReviewOpen=true;try{
  var generation=authenticationGeneration;var wasErasureLocked=AccountErasureLocked;var snapshot=ExitReviewSnapshot();
  var approved=new ConfirmationDialog(this,ExitReviewText(snapshot),"Prima di uscire","Esci da Relay","Torna al lavoro",!snapshot.Busy){Height=700}.ShowDialog()==true;
  return approved&&generation==authenticationGeneration&&(wasErasureLocked||!AccountErasureLocked)&&snapshot==ExitReviewSnapshot()&&!ExitReviewSnapshot().Busy;
  }finally{exitReviewOpen=false;}
 }
}
