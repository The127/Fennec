using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Fennec.App.ViewModels.Settings;

namespace Fennec.App.Views.Settings;

public partial class SettingsView : UserControl
{
    private IDisposable? _boundsSubscription;
    private Window? _window;

    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            _window = window;
            UpdateSize();
            _boundsSubscription = window.GetObservable(BoundsProperty)
                .Subscribe(new BoundsObserver(this));
        }

        if (DataContext is SettingsViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;

        _boundsSubscription?.Dispose();
        _boundsSubscription = null;
        _window = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ZoomLevel))
            UpdateSize();
    }

    private void UpdateSize()
    {
        if (_window is null) return;
        var zoom = DataContext is SettingsViewModel vm ? vm.ZoomLevel : 1.0;
        Width = Math.Max(400, _window.Bounds.Width / zoom - 120);
        Height = Math.Max(300, _window.Bounds.Height / zoom - 120);
    }

    private sealed class BoundsObserver(SettingsView view) : IObserver<Rect>
    {
        public void OnNext(Rect value) => view.UpdateSize();
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
