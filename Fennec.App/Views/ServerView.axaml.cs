using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Fennec.App.Models;
using Fennec.App.ViewModels;
using ShadUI;

namespace Fennec.App.Views;

public partial class ServerView : UserControl
{
    private readonly ShortcodeAutocompleteViewModel _autocompleteVm = new();

    public ServerView()
    {
        InitializeComponent();

        // Emoji picker setup
        EmojiPicker.DataContext = new EmojiPickerViewModel();
        EmojiPicker.EmojiSelected += InsertEmoji;

        // Autocomplete setup
        AutocompleteView.DataContext = _autocompleteVm;
        AutocompleteView.EntrySelected += entry => ApplyAutocompleteSuggestion(entry);

        MessageTextBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty || args.Property == TextBox.CaretIndexProperty)
                UpdateAutocomplete();
            if (args.Property == TextBox.TextProperty || args.Property == Visual.BoundsProperty)
                AdjustTextBoxHeight();
        };

        DataContextChanged += (_, _) =>
        {
            if (DataContext is ServerViewModel vm)
                vm.Messages.CollectionChanged += OnMessagesChanged;
        };
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => MessageScrollViewer.ScrollToEnd(), DispatcherPriority.Loaded);
    }

    private void AdjustTextBoxHeight()
    {
        var text = MessageTextBox.Text ?? "";
        if (string.IsNullOrEmpty(text))
            text = " ";

        var availableWidth = MessageTextBox.Bounds.Width
                             - MessageTextBox.Padding.Left
                             - MessageTextBox.Padding.Right;
        if (availableWidth <= 0)
            return;

        var typeface = new Typeface(
            MessageTextBox.FontFamily,
            MessageTextBox.FontStyle,
            MessageTextBox.FontWeight);

        var textLayout = new TextLayout(
            text,
            typeface,
            MessageTextBox.FontSize,
            foreground: null,
            maxWidth: availableWidth,
            textWrapping: TextWrapping.Wrap);

        var padding = MessageTextBox.Padding.Top + MessageTextBox.Padding.Bottom + 20;
        var desiredHeight = textLayout.Height + padding;

        const double minHeight = 36.0;
        const double maxHeight = 200.0;

        var newHeight = Math.Clamp(desiredHeight, minHeight, maxHeight);
        ControlAssist.SetHeight(MessageTextBox, newHeight);
    }

    private void InsertEmoji(string emoji)
    {
        if (DataContext is not ServerViewModel vm) return;
        var caretIndex = MessageTextBox.CaretIndex;
        vm.MessageText = vm.MessageText.Insert(caretIndex, emoji);
        MessageTextBox.CaretIndex = caretIndex + emoji.Length;
    }

    private void UpdateAutocomplete()
    {
        var text = MessageTextBox.Text ?? "";
        var caret = MessageTextBox.CaretIndex;
        _autocompleteVm.Update(text, caret);
        AutocompletePopup.IsOpen = _autocompleteVm.IsVisible;
    }

    private void ApplyAutocompleteSuggestion(EmojiEntry entry)
    {
        if (DataContext is not ServerViewModel vm) return;

        var text = vm.MessageText;
        var colonStart = _autocompleteVm.ColonStartIndex;
        var caret = MessageTextBox.CaretIndex;

        // Replace :partial with the emoji unicode
        var before = text[..colonStart];
        var after = caret < text.Length ? text[caret..] : "";
        vm.MessageText = before + entry.Unicode + after;
        MessageTextBox.CaretIndex = before.Length + entry.Unicode.Length;

        _autocompleteVm.Hide();
        AutocompletePopup.IsOpen = false;
        MessageTextBox.Focus();
    }

    private void MessageBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_autocompleteVm.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Up:
                    _autocompleteVm.MoveUp();
                    e.Handled = true;
                    return;
                case Key.Down:
                    _autocompleteVm.MoveDown();
                    e.Handled = true;
                    return;
                case Key.Tab:
                case Key.Enter:
                    var entry = _autocompleteVm.Confirm();
                    if (entry is not null)
                    {
                        ApplyAutocompleteSuggestion(entry);
                        AutocompletePopup.IsOpen = false;
                    }
                    e.Handled = true;
                    return;
                case Key.Escape:
                    _autocompleteVm.Hide();
                    AutocompletePopup.IsOpen = false;
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Shift)
        {
            var caretIndex = MessageTextBox.CaretIndex;
            var text = MessageTextBox.Text ?? "";
            MessageTextBox.Text = text.Insert(caretIndex, "\n");
            MessageTextBox.CaretIndex = caretIndex + 1;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && DataContext is ServerViewModel vm)
        {
            vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RenameChannelGroup_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ServerViewModel vm) return;
        if (sender is not TextBox { DataContext: ChannelGroupItem group }) return;

        if (e.Key == Key.Enter)
        {
            vm.ConfirmRenameChannelGroupCommand.Execute(group);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelRenameChannelGroupCommand.Execute(group);
            e.Handled = true;
        }
    }
}
