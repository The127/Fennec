using System;
using Avalonia.Controls;
using Avalonia.Input;
using Fennec.App.Models;

namespace Fennec.App.Views;

public partial class ShortcodeAutocompleteView : UserControl
{
    public event Action<EmojiEntry>? EntrySelected;

    public ShortcodeAutocompleteView()
    {
        InitializeComponent();
    }

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: EmojiEntry entry })
        {
            EntrySelected?.Invoke(entry);
        }
    }
}
