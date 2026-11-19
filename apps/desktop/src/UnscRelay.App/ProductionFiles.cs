using System.IO;
using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
 Func<ConnectionProfile,string,int,bool>? productionTestConsent;
 bool ConfirmProductionOperation(ConnectionProfile profile,string action,int count=1,string resource="")
 {
  var current=data.Profiles.SingleOrDefault(value=>value.Id==profile.Id);if(!ProductionFilePolicy.RequiresConfirmation(profile,count)&&(current==null||!ProductionFilePolicy.RequiresConfirmation(current,count)))return false;
  var generation=authenticationGeneration;RequireOperationSession(generation);
  var approved=accessSmokeTest&&productionTestConsent!=null?productionTestConsent(profile,action,count):Confirm(NativeText("AMBIENTE DI PRODUZIONE")+"\n\n"+profile.Name+"\n"+(profile.Protocol=="native"?profile.Native.Target:profile.Host+":"+profile.Port)+"\n\n"+NativeText(action)+" · "+count+"\n"+resource+"\n\n"+NativeText("Questa operazione modifica i file sul server di produzione. Verifica destinazione e selezione prima di continuare."));
  RequireOperationSession(generation);if(!approved)throw new OperationCanceledException(NativeText("Operazione di produzione annullata. Nessuna nuova modifica è stata avviata."));return true;
 }
}
