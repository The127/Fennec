using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Fennec.App.Models;

namespace Fennec.App.Views;

public partial class EmojiPickerView : UserControl
{
    public event Action<string>? EmojiSelected;

    public EmojiPickerView()
    {
        InitializeComponent();
    }

    public void FocusSearch()
    {
        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Loaded);
    }

    private void EmojiButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: EmojiEntry entry })
        {
            EmojiSelected?.Invoke(entry.Unicode);
        }
    }
}
