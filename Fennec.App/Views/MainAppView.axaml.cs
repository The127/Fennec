using Avalonia.Controls;
using Avalonia.Input;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class MainAppView : UserControl
{
    public MainAppView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainAppViewModel { IsSettingsOpen: true } vm)
        {
            vm.SettingsViewModel?.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
