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
        Owner = owner; Title = title; Width = 480; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(9, 16, 19)); Foreground = Brushes.White;
        var root = new StackPanel { Margin = new(24) };
        root.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 15 }); root.Children.Add(_password);
        var confirmation = new PasswordBox { Margin = new(0, 8, 0, 15), FontSize = 18, Padding = new(10) };
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        if (confirmCreation) { root.Children.Add(new TextBlock { Text = "Ripeti la password principale:" }); root.Children.Add(confirmation); root.Children.Add(error); }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Conferma", IsDefault = true, Padding = new(15, 8, 15, 8), Margin = new(6) };
        var cancel = new Button { Content = "Annulla", IsCancel = true, Padding = new(15, 8, 15, 8), Margin = new(6) };
        ok.Click += (_, _) => {
            if (confirmCreation) {
                try { VaultPasswordPolicy.ValidateCreation(_password.Password, confirmation.Password); }
                catch (ArgumentException ex) { error.Text = ex.Message; confirmation.Clear(); confirmation.Focus(); return; }
            }
            DialogResult = true;
        }; buttons.Children.Add(cancel); buttons.Children.Add(ok); root.Children.Add(buttons); Content = root;
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
