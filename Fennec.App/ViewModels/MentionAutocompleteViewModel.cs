using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.ViewModels;

public partial class MentionAutocompleteViewModel : ObservableObject
{
    private const int MaxSuggestions = 8;
    private const int MinQueryLength = 1;

    public ObservableCollection<string> Suggestions { get; } = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// The index of the '@' that started the current mention query.
    /// </summary>
    public int AtStartIndex { get; private set; }

    public string? SelectedSuggestion =>
        IsVisible && SelectedIndex >= 0 && SelectedIndex < Suggestions.Count
            ? Suggestions[SelectedIndex]
            : null;

    public void Update(string text, int caretIndex, List<string> members)
    {
        // Find the last unmatched '@' before the caret
        var atIndex = -1;
        for (var i = caretIndex - 1; i >= 0; i--)
        {
            if (text[i] == '@')
            {
                atIndex = i;
                break;
            }

            // Stop on whitespace or non-word characters
            if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
                break;
        }

        if (atIndex < 0)
        {
            Hide();
            return;
        }

        var query = text[(atIndex + 1)..caretIndex];

        if (query.Length < MinQueryLength)
        {
            Hide();
            return;
        }

        AtStartIndex = atIndex;
        var results = members
            .Where(m => m.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .ToList();

        if (results.Count == 0)
        {
            Hide();
            return;
        }

        Suggestions.Clear();
        foreach (var name in results)
            Suggestions.Add(name);

        SelectedIndex = 0;
        IsVisible = true;
    }

    public void MoveUp()
    {
        if (Suggestions.Count == 0) return;
        SelectedIndex = (SelectedIndex - 1 + Suggestions.Count) % Suggestions.Count;
    }

    public void MoveDown()
    {
        if (Suggestions.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Suggestions.Count;
    }

    public string? Confirm()
    {
        var name = SelectedSuggestion;
        Hide();
        return name;
    }

    public void Hide()
    {
        IsVisible = false;
        Suggestions.Clear();
        SelectedIndex = 0;
    }
}
