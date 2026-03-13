using Avalonia;
using Avalonia.Controls;
using Fennec.App.ViewModels.Settings;

namespace Fennec.App.Views.Settings;

public partial class AudioSettingsView : UserControl
{
    public AudioSettingsView()
    {
        InitializeComponent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        (DataContext as AudioSettingsViewModel)?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
