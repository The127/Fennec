using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Fennec.App.Models;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class ShortcodeAutocompleteView : UserControl
{
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#20808080"));

    public event Action<EmojiEntry>? EntrySelected;

    public ShortcodeAutocompleteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ShortcodeAutocompleteViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShortcodeAutocompleteViewModel.SelectedIndex)
            or nameof(ShortcodeAutocompleteViewModel.IsVisible))
        {
            UpdateHighlight();
        }
    }

    private void UpdateHighlight()
    {
        if (DataContext is not ShortcodeAutocompleteViewModel vm) return;

        for (var i = 0; i < SuggestionsList.ItemCount; i++)
        {
            var container = SuggestionsList.ContainerFromIndex(i);
            if (container is null) continue;

            var border = FindChildBorder(container);
            if (border is not null)
            {
                border.Background = i == vm.SelectedIndex ? SelectedBrush : Brushes.Transparent;
            }
        }
    }

    private static Border? FindChildBorder(Control control)
    {
        if (control is ContentPresenter { Child: Border border })
            return border;
        if (control is Border b)
            return b;
        return null;
    }

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: EmojiEntry entry })
        {
            EntrySelected?.Invoke(entry);
        }
    }

    private void Row_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is not ShortcodeAutocompleteViewModel vm) return;
        if (sender is not Border { DataContext: EmojiEntry entry }) return;

        var index = vm.Suggestions.IndexOf(entry);
        if (index >= 0)
        {
            vm.SelectedIndex = index;
        }
    }
}
