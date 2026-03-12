using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Fennec.App.Models;

namespace Fennec.App.Views;

public partial class EmojiPickerView : UserControl
{
    public event Action<string>? EmojiSelected;

    public EmojiPickerView()
    {
        InitializeComponent();
    }

    private void EmojiButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: EmojiEntry entry })
        {
            EmojiSelected?.Invoke(entry.Unicode);
        }
    }
}
