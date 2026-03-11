using Avalonia.Controls;
using Avalonia.Interactivity;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        UsernameTextBox.LostFocus += OnUsernameLostFocus;
    }

    private void OnUsernameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (!textBox.Text?.Contains("://") ?? true) return;

        textBox.Text = textBox.Text
            .Replace("https://", "")
            .Replace("http://", "");

        if (DataContext is LoginViewModel vm)
            vm.NotifySchemeStripped();
    }
}
