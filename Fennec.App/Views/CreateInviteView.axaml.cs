using Avalonia;
using Avalonia.Controls;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class CreateInviteView : UserControl
{
    public CreateInviteView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is CreateInviteViewModel vm)
        {
            vm.Clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        }
    }
}
