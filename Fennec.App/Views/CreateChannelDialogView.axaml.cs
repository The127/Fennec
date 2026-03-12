using Avalonia.Controls;
using Avalonia.Input;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class CreateChannelDialogView : UserControl
{
    public CreateChannelDialogView()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is CreateChannelDialogViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && DataContext is CreateChannelDialogViewModel vm2)
        {
            vm2.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
