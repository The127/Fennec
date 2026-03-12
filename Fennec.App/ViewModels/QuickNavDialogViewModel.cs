using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;

namespace Fennec.App.ViewModels;

public record QuickNavItem(string Name, string Category, string Icon, Action Navigate);

public partial class QuickNavDialogViewModel : ObservableObject
{
    private readonly DialogManager _dialogManager;
    private readonly List<QuickNavItem> _allItems;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    public ObservableCollection<QuickNavItem> FilteredItems { get; } = [];

    public QuickNavDialogViewModel(DialogManager dialogManager, List<QuickNavItem> items)
    {
        _dialogManager = dialogManager;
        _allItems = items;
        UpdateFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilter();
    }

    private void UpdateFilter()
    {
        FilteredItems.Clear();
        var query = SearchText.Trim();
        foreach (var item in _allItems)
        {
            if (string.IsNullOrEmpty(query)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredItems.Add(item);
            }
        }

        SelectedIndex = FilteredItems.Count > 0 ? 0 : -1;
    }

    public void MoveSelection(int delta)
    {
        if (FilteredItems.Count == 0) return;
        SelectedIndex = (SelectedIndex + delta + FilteredItems.Count) % FilteredItems.Count;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedIndex >= 0 && SelectedIndex < FilteredItems.Count)
        {
            var item = FilteredItems[SelectedIndex];
            _dialogManager.Close(this);
            item.Navigate();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    [RelayCommand]
    private void SelectItem(QuickNavItem item)
    {
        _dialogManager.Close(this);
        item.Navigate();
    }
}
