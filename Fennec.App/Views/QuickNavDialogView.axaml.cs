using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class QuickNavDialogView : UserControl
{
    public QuickNavDialogView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Input);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickNavDialogViewModel vm) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.ConfirmCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.CancelCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                vm.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down:
                vm.MoveSelection(1);
                e.Handled = true;
                break;
        }
    }
}
