using System;
using Avalonia;
using Avalonia.Controls;

namespace Fennec.App.Views.Settings;

public partial class SettingsView : UserControl
{
    private IDisposable? _boundsSubscription;

    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            UpdateSize(window);
            _boundsSubscription = window.GetObservable(BoundsProperty)
                .Subscribe(new BoundsObserver(this, window));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _boundsSubscription?.Dispose();
        _boundsSubscription = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateSize(Window window)
    {
        Width = Math.Max(400, window.Bounds.Width - 120);
        Height = Math.Max(300, window.Bounds.Height - 120);
    }

    private sealed class BoundsObserver(SettingsView view, Window window) : IObserver<Rect>
    {
        public void OnNext(Rect value) => view.UpdateSize(window);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
