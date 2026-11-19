using System.Windows;
namespace UnscRelay.App;
public partial class MainWindow
{
 bool nativePresentationMode;
 readonly HashSet<string> presentationHiddenWindows=[];
 object SetPresentationMode(bool enabled)
 {
  void HideDesktopControls(){foreach(var session in desktopSessions.Values){session.Host.Visibility=Visibility.Hidden;if(session.Rdp!=null&&!session.Rdp.TryReturnToTabForPresentation())throw new InvalidOperationException(NativeText("Impossibile verificare che RDP sia nascosto. La presentazione non è stata confermata."));}}
  if(enabled==nativePresentationMode){if(enabled)HideDesktopControls();return new{enabled=nativePresentationMode};}
  if(enabled){
   if(activeOperations>1||accessBusy||helloBusy||cloudRunning||connectingSessions.Count>0||certificateRenewBusy||conversionOperation||exportWindows.Count>0||Application.Current.Windows.Cast<Window>().Any(window=>window!=this&&window is not DetachedWindow&&window.IsVisible))throw new InvalidOperationException(NativeText("Operazione annullata."));
   nativePresentationMode=true;
   foreach(var pair in detachedWindows)if(pair.Value.IsVisible){presentationHiddenWindows.Add(pair.Key);pair.Value.Hide();}
   DesktopLayer.Visibility=Visibility.Collapsed;
   foreach(var window in detachedWindows.Values)window.DesktopLayer.Visibility=Visibility.Collapsed;
   HideDesktopControls();ApplyDesktopLayout();
  }else{
   nativePresentationMode=false;
   if(!windowsSessionLocked&&!OperationsLocked&&!closing&&!updating){
    DesktopLayer.Visibility=Visibility.Visible;
    foreach(var id in presentationHiddenWindows)if(detachedWindows.TryGetValue(id,out var window)){window.DesktopLayer.Visibility=Visibility.Visible;var activate=window.ShowActivated;window.ShowActivated=false;try{window.Show();}finally{window.ShowActivated=activate;}}
    ApplyDesktopLayout();
   }
   presentationHiddenWindows.Clear();
  }
  return new{enabled=nativePresentationMode};
 }
}
