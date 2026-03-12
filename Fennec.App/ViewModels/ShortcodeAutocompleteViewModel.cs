using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Models;

namespace Fennec.App.ViewModels;

public partial class ShortcodeAutocompleteViewModel : ObservableObject
{
    private const int MaxSuggestions = 8;
    private const int MinQueryLength = 2;

    public ObservableCollection<EmojiEntry> Suggestions { get; } = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// The index of the ':' that started the current shortcode query.
    /// </summary>
    public int ColonStartIndex { get; private set; }

    public EmojiEntry? SelectedSuggestion =>
        IsVisible && SelectedIndex >= 0 && SelectedIndex < Suggestions.Count
            ? Suggestions[SelectedIndex]
            : null;

    public void Update(string text, int caretIndex)
    {
        // Find the last unmatched ':' before the caret
        var colonIndex = -1;
        for (var i = caretIndex - 1; i >= 0; i--)
        {
            if (text[i] == ':')
            {
                // Check this isn't part of an already-completed shortcode (look for a prior ':')
                colonIndex = i;
                break;
            }

            // Stop on whitespace or non-shortcode characters
            if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
                break;
        }

        if (colonIndex < 0)
        {
            Hide();
            return;
        }

        var query = text[(colonIndex + 1)..caretIndex];

        if (query.Length < MinQueryLength)
        {
            Hide();
            return;
        }

        ColonStartIndex = colonIndex;
        var results = EmojiDatabase.Search(query).Take(MaxSuggestions).ToList();

        if (results.Count == 0)
        {
            Hide();
            return;
        }

        Suggestions.Clear();
        foreach (var entry in results)
            Suggestions.Add(entry);

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

    public EmojiEntry? Confirm()
    {
        var entry = SelectedSuggestion;
        Hide();
        return entry;
    }

    public void Hide()
    {
        IsVisible = false;
        Suggestions.Clear();
        SelectedIndex = 0;
    }
}
