using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class MainAppView : UserControl
{
    private bool _isDraggingPip;
    private Point _pipDragStart;
    private double _pipTranslateStartX;
    private double _pipTranslateStartY;
    private TranslateTransform? _pipTranslate;

    public MainAppView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainAppViewModel vm)
                vm.SearchFocusRequested += () => SearchBox.Focus();
        };
    }

    private TranslateTransform GetPipTranslate()
    {
        if (_pipTranslate is not null) return _pipTranslate;
        _pipTranslate = new TranslateTransform();
        FloatingPip.RenderTransform = _pipTranslate;
        return _pipTranslate;
    }

    private void ProfileMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ProfileDropdown.IsDropDownOpen = false;
    }

    private void SwitchAccountMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ProfileDropdown.IsDropDownOpen = false;
        if (DataContext is MainAppViewModel vm)
            vm.SwitchAccountCommand.Execute(null);
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

    private void FloatingPip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var t = GetPipTranslate();
            _isDraggingPip = true;
            _pipDragStart = e.GetPosition(this);
            _pipTranslateStartX = t.X;
            _pipTranslateStartY = t.Y;
            e.Pointer.Capture(FloatingPip);
            e.Handled = true;
        }
    }

    private void FloatingPip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingPip) return;

        var t = GetPipTranslate();
        var pos = e.GetPosition(this);
        t.X = _pipTranslateStartX + (pos.X - _pipDragStart.X);
        t.Y = _pipTranslateStartY + (pos.Y - _pipDragStart.Y);
        e.Handled = true;
    }

    private void FloatingPip_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingPip) return;

        var pos = e.GetPosition(this);
        var totalDrag = Math.Abs(pos.X - _pipDragStart.X) + Math.Abs(pos.Y - _pipDragStart.Y);

        _isDraggingPip = false;
        e.Pointer.Capture(null);
        e.Handled = true;

        // If barely moved, treat as click → navigate to voice server
        if (totalDrag < 5 && DataContext is MainAppViewModel vm)
        {
            vm.NavigateToFloatingScreenShareCommand.Execute(null);
        }
    }
}
