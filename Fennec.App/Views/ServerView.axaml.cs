using Avalonia.Controls;
using Avalonia.Input;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class ServerView : UserControl
{
    public ServerView()
    {
        InitializeComponent();
    }

    private void MessageBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ServerViewModel vm)
        {
            vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RenameChannelGroup_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ServerViewModel vm) return;
        if (sender is not TextBox { DataContext: ChannelGroupItem group }) return;

        if (e.Key == Key.Enter)
        {
            vm.ConfirmRenameChannelGroupCommand.Execute(group);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelRenameChannelGroupCommand.Execute(group);
            e.Handled = true;
        }
    }
}
