using Avalonia.Controls;
using Avalonia.Input;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class MainAppView : UserControl
{
    public MainAppView()
    {
        InitializeComponent();
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
