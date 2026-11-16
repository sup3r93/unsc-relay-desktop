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
            }
        }
        catch(Exception ex){error=ex.ToString();}
        var success=error==null&&checks.Values.All(v=>v);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"presentation-smoke-result.json"),JsonSerializer.Serialize(new{success,checks,error}));await Task.Yield();CompleteSmoke(success);
    }
}
