using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace UnscRelay.App;
public partial class MainWindow
{
    async Task CheckPresentation()
    {
        var checks=new Dictionary<string,bool>();string? error=null;
        try
        {
            checks["startsMaximizedWithWindowChrome"]=WindowState==WindowState.Maximized && WindowStyle==WindowStyle.SingleBorderWindow;
            var dialog=new UpdateConfirmation(this,"v0.1.13",@"A:\UNSC Relay");
            dialog.Loaded+=async(_,_)=>{await Task.Delay(200);checks["cancelFocusedByDefault"]=dialog.CancelButton.IsKeyboardFocused;var image=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(dialog);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(AppContext.BaseDirectory,"presentation-confirmation.png")))encoder.Save(stream);if(presentationSmokeTest)dialog.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
            checks["cancelDoesNotInstall"]=dialog.ShowDialog()!=true;
            if(presentationSmokeTest)
            {
                var approved=new UpdateConfirmation(this,"v0.1.13",@"A:\UNSC Relay");approved.Loaded+=(_,_)=>Dispatcher.BeginInvoke(()=>approved.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));checks["installReturnsExplicitApproval"]=approved.ShowDialog()==true;
                var floating=new DetachedWindow{Owner=this,ShowActivated=false};var hidden=new DetachedWindow{Owner=this,ShowActivated=false};var floatingId=Guid.NewGuid().ToString("N");var hiddenId=Guid.NewGuid().ToString("N");
                detachedWindows.Add(floatingId,floating);detachedWindows.Add(hiddenId,hidden);floating.Show();
                var host=new System.Windows.Forms.Integration.WindowsFormsHost{Child=new System.Windows.Forms.Panel()};var desktopId=Guid.NewGuid().ToString("N");desktopSessions.Add(desktopId,new DesktopSession{Host=host});DesktopLayer.Children.Add(host);desktopRects[desktopId]=new(0,0,100,100);
                try{
                 var nativeDialog=new Window{Owner=this,Width=100,Height=100,ShowActivated=false};nativeDialog.Show();try{try{SetPresentationMode(true);checks["refusesMaskWhileNativeDialogIsVisible"]=false;}catch(InvalidOperationException){checks["refusesMaskWhileNativeDialogIsVisible"]=!nativePresentationMode;}}finally{nativeDialog.Close();}
                 SetPresentationMode(true);checks["masksNativeLayerAndDetachedWindow"]=nativePresentationMode&&DesktopLayer.Visibility==Visibility.Collapsed&&!floating.IsVisible&&!hidden.IsVisible&&host.Visibility==Visibility.Hidden;
                 ApplyDesktopLayout();checks["layoutCannotRevealNativeContent"]=host.Visibility==Visibility.Hidden;
                 checks["backgroundConfirmCannotShowSensitiveDialog"]=!Confirm("Synthetic sensitive confirmation");
                 try{await Execute("clipboardRead",JsonSerializer.SerializeToElement(new{}));checks["refusesSensitiveActionsWhileMasked"]=false;}catch(InvalidOperationException){checks["refusesSensitiveActionsWhileMasked"]=true;}
                 SetPresentationMode(false);checks["restoresOnlyPreviouslyVisibleDetachedWindows"]=floating.IsVisible&&!hidden.IsVisible&&DesktopLayer.Visibility==Visibility.Visible;
                 SetPresentationMode(true);windowsSessionLocked=true;try{SetPresentationMode(false);checks["unmaskCannotRevealLockedWindows"]=!floating.IsVisible&&DesktopLayer.Visibility==Visibility.Collapsed;}finally{windowsSessionLocked=false;}
                }finally{nativePresentationMode=false;presentationHiddenWindows.Clear();desktopSessions.Remove(desktopId);desktopRects.Remove(desktopId);DesktopLayer.Children.Remove(host);host.Dispose();detachedWindows.Remove(floatingId);detachedWindows.Remove(hiddenId);floating.Close();hidden.Close();}

            }
        }
        catch(Exception ex){error=ex.ToString();}
        var success=error==null&&checks.Values.All(v=>v);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"presentation-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error}));await Task.Yield();CompleteSmoke(success);
    }
}
