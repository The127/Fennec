using Avalonia.Controls;
using Avalonia.Input;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class ScreenSharePickerView : UserControl
{
    public ScreenSharePickerView()
    {
        InitializeComponent();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ScreenSharePickerViewModel vm && vm.SelectedTarget is not null)
            vm.ConfirmCommand.Execute(null);
    }
}
