using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Models;

namespace Fennec.App.ViewModels;

public partial class EmojiPickerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryName = EmojiDatabase.Categories[0].Name;

    [ObservableProperty]
    private IReadOnlyList<EmojiEntry> _filteredEmojis = EmojiDatabase.Categories[0].Emojis;

    public IReadOnlyList<EmojiCategory> Categories => EmojiDatabase.Categories;

    partial void OnSearchTextChanged(string value)
    {
        UpdateFiltered();
    }

    [RelayCommand]
    private void SelectCategory(string categoryName)
    {
        SelectedCategoryName = categoryName;
        SearchText = string.Empty;
        UpdateFiltered();
    }

    private void UpdateFiltered()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            SelectedCategoryName = "Search Results";
            FilteredEmojis = EmojiDatabase.Search(SearchText);
            return;
        }

        foreach (var category in EmojiDatabase.Categories)
        {
            if (category.Name == SelectedCategoryName)
            {
                FilteredEmojis = category.Emojis;
                return;
            }
        }

        SelectedCategoryName = EmojiDatabase.Categories[0].Name;
        FilteredEmojis = EmojiDatabase.Categories[0].Emojis;
    }
}
