using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UnscRelay.Core;

namespace UnscRelay.App;
public sealed class SecretDialog : Window
{
    private readonly PasswordBox _password = new() { Margin = new(0, 15, 0, 15), FontSize = 18, Padding = new(10) };
    public string Secret => _password.Password;
    public SecretDialog(Window owner, string title, string message, bool confirmCreation = false)
    {
        var host=owner as MainWindow;string Text(string value)=>host?.NativeText(value)??value;
        Owner = owner;ShowInTaskbar=false; Title = Text(title); Width = 560;MinWidth=360;MaxHeight=SystemParameters.WorkArea.Height*.9; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(9, 16, 19)); Foreground = Brushes.White;
        var root = new StackPanel { Margin = new(24) };
        var label=new Label{Content=new TextBlock{Text=Text(message),TextWrapping=TextWrapping.Wrap,FontSize=17},Target=_password,Padding=new Thickness(0),Foreground=Brushes.White};System.Windows.Automation.AutomationProperties.SetName(label,Text(message));root.Children.Add(label);System.Windows.Automation.AutomationProperties.SetName(_password,Text("Password"));System.Windows.Automation.AutomationProperties.SetHelpText(_password,Text(message));root.Children.Add(_password);
        var confirmation = new PasswordBox { Margin = new(0, 8, 0, 15), FontSize = 18, Padding = new(10) };
        var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(243,182,164)), TextWrapping = TextWrapping.Wrap };
        if (confirmCreation) { root.Children.Add(new Label { Content = Text("Ripeti la password principale:"),Target=confirmation,FontSize=16,Foreground=Brushes.White,Padding=new Thickness(0) }); System.Windows.Automation.AutomationProperties.SetName(confirmation,Text("Ripeti la password principale:"));root.Children.Add(confirmation);System.Windows.Automation.AutomationProperties.SetLiveSetting(error,System.Windows.Automation.AutomationLiveSetting.Assertive);root.Children.Add(error); }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok=host?.CreateNativeButton("Conferma",true)??new Button{Content=Text("Conferma"),MinHeight=44};ok.IsDefault=true;ok.Margin=new Thickness(6);
        var cancel=host?.CreateNativeButton("Annulla")??new Button{Content=Text("Annulla"),MinHeight=44};cancel.IsCancel=true;cancel.Margin=new Thickness(6);
        ok.Click += (_, _) => {
            if (confirmCreation) {
                try { VaultPasswordPolicy.ValidateCreation(_password.Password, confirmation.Password); }
                catch (ArgumentException ex) { error.Text = Text(ex.Message); confirmation.Clear(); confirmation.Focus(); return; }
            }
            DialogResult = true;
        }; buttons.Children.Add(cancel); buttons.Children.Add(ok); root.Children.Add(buttons); Content = new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        Closed += (_, _) => confirmation.Clear();
        Loaded += (_, _) => _password.Focus();
    }
    protected override void OnClosed(EventArgs e) { _password.Clear(); base.OnClosed(e); }
    public static string Ask(Window owner, string title, string message, bool confirmCreation = false)
    {
        // Capture before the control is cleared at close; the master password is never persisted.
        var dialog = new SecretDialog(owner, title, message, confirmCreation); string? result = null;
        dialog.Closing += (_, _) => { if (dialog.DialogResult == true) result = dialog._password.Password; };
        if (dialog.ShowDialog() != true) throw new OperationCanceledException("Operazione annullata.");
        return result ?? "";
    }
}
