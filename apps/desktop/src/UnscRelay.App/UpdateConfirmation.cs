using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace UnscRelay.App;
public sealed class UpdateConfirmation : Window
{
    public Button InstallButton { get; }
    public Button CancelButton { get; }
    static SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    public UpdateConfirmation(Window owner, string version, string destination)
    {
        Icon=BitmapFrame.Create(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","Relay.ico")));
        Owner=owner; Title="UNSC Relay · Aggiornamento verificato"; Width=820; SizeToContent=SizeToContent.Height; MaxHeight=SystemParameters.WorkArea.Height-40;
        ResizeMode=ResizeMode.NoResize; WindowStartupLocation=WindowStartupLocation.CenterOwner; Background=B("#0B171A");Foreground=B("#DFE7DB");FontFamily=new("Segoe UI");
        var root=new StackPanel{Margin=new(34)};var header=new DockPanel{Margin=new(0,0,0,24)};
        var logo=new Image{Width=70,Height=90,Margin=new(0,0,22,0),Source=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","UNSC.png")))};DockPanel.SetDock(logo,Dock.Left);header.Children.Add(logo);
        var title=new StackPanel{VerticalAlignment=VerticalAlignment.Center};title.Children.Add(Text("UNSC // DEPLOYMENT READY",11,"#B5CA83"));title.Children.Add(Text("Aggiornamento pronto",27,"#DFE7DB"));title.Children.Add(Text("Pacchetto verificato · "+version,14,"#A6BBA5"));header.Children.Add(title);root.Children.Add(header);
        var card=new StackPanel{Margin=new(18)};card.Children.Add(Text("VERIFICHE COMPLETATE",11,"#B5CA83"));card.Children.Add(Text("✓ Integrità SHA-256 del download",15));card.Children.Add(Text("✓ Firma del produttore prevista dall’app",15));root.Children.Add(new Border{Background=B("#14271F"),BorderBrush=B("#344D39"),BorderThickness=new(1),CornerRadius=new(6),Child=card});
        root.Children.Add(Text("Profili, archivio chiavi, Windows Hello, impostazioni e token vengono conservati. Prima dell’installazione viene creata una copia dei dati locali.",14,"#BCCBBC",new(0,22,0,12)));
        var destinationCard=new StackPanel{Margin=new(16)};destinationCard.Children.Add(Text("DESTINAZIONE DEL PROGRAMMA",10,"#B5CA83"));destinationCard.Children.Add(Text(destination,15,"#DFE7DB"));root.Children.Add(new Border{Background=B("#101E1C"),BorderBrush=B("#344D39"),BorderThickness=new(1),CornerRadius=new(5),Child=destinationCard});root.Children.Add(Text("La console si chiuderà senza outro e si riaprirà dopo l’installazione.",14,"#DFE7DB",new(0,12,0,20)));
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};CancelButton=Action("Non ora",false);CancelButton.IsCancel=true;CancelButton.Click+=(_,_)=>DialogResult=false;InstallButton=Action("Installa e riapri",true);InstallButton.IsDefault=true;InstallButton.Click+=(_,_)=>DialogResult=true;actions.Children.Add(CancelButton);actions.Children.Add(InstallButton);root.Children.Add(actions);
        Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Loaded+=(_,_)=>CancelButton.Focus();
    }
    static TextBlock Text(string text,double size,string color="#DFE7DB",Thickness? margin=null)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Foreground=B(color),Margin=margin??new(0,5,0,5)};
    static Button Action(string text,bool primary)
    {
        var button=new Button{Content=text,FontSize=14,Padding=new(20,11,20,11),Margin=new(10,0,0,0),Background=B(primary?"#B3C782":"#152820"),Foreground=B(primary?"#101B13":"#D2DFC3"),BorderBrush=B("#557043"),BorderThickness=new(1),MinHeight=44};
        var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush"){RelativeSource=new(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.SetValue(Border.BorderThicknessProperty,new Thickness(1));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.MarginProperty,new Thickness(20,11,20,11));content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);border.AppendChild(content);template.VisualTree=border;button.Template=template;
        button.MouseEnter+=(_,_)=>button.Opacity=.85;button.MouseLeave+=(_,_)=>button.Opacity=1;return button;
    }
}
