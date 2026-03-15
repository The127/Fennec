using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Fennec.App.ViewModels.Settings;
using SelectionChangedEventArgs = Avalonia.Controls.SelectionChangedEventArgs;

namespace Fennec.App.Views.Settings;

public partial class KeybindingsSettingsView : UserControl
{
    public KeybindingsSettingsView()
    {
        InitializeComponent();
    }

    private void OnShortcutPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not ShortcutBindingItem item)
            return;

        if (DataContext is not KeybindingsSettingsViewModel vm)
            return;

        vm.StartCaptureCommand.Execute(item);

        // Focus the capture border so it receives key events
        // Need to defer because the capture border becomes visible after the binding update
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var captureBox = FindCaptureBoxForItem(border);
            captureBox?.Focus();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not ShortcutBindingItem item)
            return;

        if (DataContext is not KeybindingsSettingsViewModel vm)
            return;

        // Ignore bare modifier keys
        if (e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        e.Handled = true;

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            vm.CancelCaptureCommand.Execute(item);
            return;
        }

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        vm.HandleCapturedGesture(item, gesture);
    }

    private void OnCaptureLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not ShortcutBindingItem item)
            return;

        if (DataContext is not KeybindingsSettingsViewModel vm)
            return;

        if (item.IsCapturing)
            vm.CancelCaptureCommand.Execute(item);
    }

    private void OnMouseSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.DataContext is not MouseBindingItem item)
            return;

        if (DataContext is not KeybindingsSettingsViewModel vm)
            return;

        vm.HandleMouseBindingChanged(item);
    }

    private static Border? FindCaptureBoxForItem(Border originalBorder)
    {
        // The capture border is a sibling in the same Panel
        var parent = originalBorder.GetVisualParent();
        if (parent is null) return null;

        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Border b && b.Name == "CaptureBox")
                return b;
        }

        return null;
    }
}
