using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class MainAppView : UserControl
{
    public MainAppView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainAppViewModel vm)
                vm.SearchFocusRequested += () => SearchBox.Focus();
        };
    }

    private void ProfileMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ProfileDropdown.IsDropDownOpen = false;
    }

    private void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ProfileDropdown.IsDropDownOpen = false;
        if (DataContext is MainAppViewModel vm)
            vm.OpenSettingsCommand.Execute(null);
    }

    private void SearchBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (DataContext is MainAppViewModel { IsSearchable: false } vm)
        {
            // Unfocus the search box and open quick nav instead
            var topLevel = TopLevel.GetTopLevel(this);
            topLevel?.FocusManager?.ClearFocus();
            vm.OpenQuickNavCommand.Execute(null);
        }
    }
}
