using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace UnscRelay.App;

// Native UI: request text is plain text, never HTML or a clickable link.
sealed class AccessApprovalDialog : Window
{
    public bool HasExpiration { get; }
    public string Method { get; private set; } = "";
    public AccessApprovalDialog(Window owner,string title,string detail,bool hello,bool allowExit=false,bool account=false)
    {
        Owner=owner;Title=title;Width=720;Height=560;MinWidth=500;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(9,20,21));Foreground=Brushes.White;
        var grid=new DockPanel{Margin=new Thickness(26)};
        var header=new TextBlock{Text=title,FontSize=23,Foreground=new SolidColorBrush(Color.FromRgb(182,204,134)),Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);grid.Children.Add(header);
        var controls=new WrapPanel{Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(controls,Dock.Bottom);grid.Children.Add(controls);
        void Add(string label,string method){var b=new Button{Content=label,Padding=new Thickness(15,10,15,10),Margin=new Thickness(0,0,12,10)};b.Click+=(_,_)=>{Method=method;DialogResult=true;};controls.Children.Add(b);}
        if(hello)Add("Autorizza con Windows Hello","hello");Add(account?"Autorizza con il tuo account":"Autorizza con password",account?"account":"password");
        var deny=new Button{Content=allowExit?"Esci":"Rifiuta",IsCancel=true,Padding=new Thickness(15,10,15,10),Margin=new Thickness(0,0,12,10)};controls.Children.Add(deny);
        grid.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=detail,TextWrapping=TextWrapping.Wrap,FontFamily=new FontFamily("Consolas"),FontSize=13,LineHeight=21}});Content=grid;
        HasExpiration=!allowExit;
        if(HasExpiration){var timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(2)};timer.Tick+=(_,_)=>{timer.Stop();DialogResult=false;};Loaded+=(_,_)=>timer.Start();Closed+=(_,_)=>timer.Stop();}
    }
}
