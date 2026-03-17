using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    private readonly MentionAutocompleteViewModel _mentionVm = new();

    public ServerView()
    {
        InitializeComponent();

        // Emoji picker setup
        EmojiPicker.DataContext = new EmojiPickerViewModel();
        EmojiPicker.EmojiSelected += InsertEmoji;
        EmojiButton.Flyout!.Opened += (_, _) => EmojiPicker.FocusSearch();

        // Autocomplete setup
        AutocompleteView.DataContext = _autocompleteVm;
        AutocompleteView.EntrySelected += entry => ApplyAutocompleteSuggestion(entry);

        // Mention autocomplete setup
        MentionAutocompleteView.DataContext = _mentionVm;
        MentionAutocompleteView.EntrySelected += name => ApplyMentionSuggestion(name);

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
            {
                vm.Messages.CollectionChanged += OnMessagesChanged;
                vm.MessageInputFocusRequested += () =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        MessageTextBox.Focus();
                        MessageScrollViewer.ScrollToEnd();
                    }, DispatcherPriority.Loaded);
                vm.EmojiPickerRequested += OpenEmojiPicker;
                vm.AttachFileRequested += () => _ = OpenFilePickerAsync();
            }
        };

        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        MessageTextBox.AddHandler(KeyDownEvent, MessageBox_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => MessageTextBox.Focus(), DispatcherPriority.Loaded);
    }

    private async void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ServerViewModel vm) return;

        if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.None)
        {
            vm.ScreenShareWatcher.ShowDebugOverlay = !vm.ScreenShareWatcher.ShowDebugOverlay;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && vm.ScreenShareWatcher.IsScreenShareMaximized)
        {
            vm.ScreenShareWatcher.ExitScreenShareMaximizeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control && vm.HasSelectedMessages)
        {
            e.Handled = true;
            var text = vm.GetSelectedMessagesText();
            if (text is null) return;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(text);
        }
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

    private void AttachFileButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = OpenFilePickerAsync();
    }

    private async Task OpenFilePickerAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach files",
            AllowMultiple = true,
        });

        if (files.Count > 0 && DataContext is ServerViewModel vm)
        {
            vm.AddAttachments(files);
        }
    }

    private void OpenEmojiPicker()
    {
        EmojiButton.Flyout!.ShowAt(EmojiButton);
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

        var vm = DataContext as ServerViewModel;
        _mentionVm.Update(text, caret, vm?.ServerMembers ?? []);
        MentionAutocompletePopup.IsOpen = _mentionVm.IsVisible;
    }

    private void ApplyMentionSuggestion(string name)
    {
        if (DataContext is not ServerViewModel vm) return;

        var text = vm.MessageText;
        var atStart = _mentionVm.AtStartIndex;
        var caret = MessageTextBox.CaretIndex;

        var before = text[..atStart];
        var after = caret < text.Length ? text[caret..] : "";
        var inserted = $"@{name} ";
        vm.MessageText = before + inserted + after;
        MessageTextBox.CaretIndex = before.Length + inserted.Length;

        _mentionVm.Hide();
        MentionAutocompletePopup.IsOpen = false;
        MessageTextBox.Focus();
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

    private void OnMessagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: MessageItem message }) return;
        if (DataContext is not ServerViewModel vm) return;

        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        vm.SelectMessage(message, shift, ctrl);
    }

    private async void OnCopyMessageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string content }) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(content);
    }

    private void MessageBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_mentionVm.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Up:
                    _mentionVm.MoveUp();
                    e.Handled = true;
                    return;
                case Key.Down:
                    _mentionVm.MoveDown();
                    e.Handled = true;
                    return;
                case Key.Tab:
                case Key.Enter:
                    var name = _mentionVm.Confirm();
                    if (name is not null)
                    {
                        ApplyMentionSuggestion(name);
                        MentionAutocompletePopup.IsOpen = false;
                    }
                    e.Handled = true;
                    return;
                case Key.Escape:
                    _mentionVm.Hide();
                    MentionAutocompletePopup.IsOpen = false;
                    e.Handled = true;
                    return;
            }
        }

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

        if (DataContext is not ServerViewModel vm) return;

        // Escape: clear message selection, scroll to bottom
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && vm.HasSelectedMessages)
        {
            e.Handled = true;
            vm.ClearMessageSelection();
            MessageScrollViewer.ScrollToEnd();
            return;
        }

        // Shift+Enter: let AcceptsReturn insert the newline (don't handle)
        // Plain Enter: send the message
        if (e.Key == Key.Enter && e.KeyModifiers != KeyModifiers.Shift)
        {
            vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnChannelDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { DataContext: ChannelItem channel }) return;
        if (channel.IsTextOnly) return;
        if (DataContext is ServerViewModel vm)
            vm.VoiceCall.JoinVoiceChannelCommand.Execute(channel);
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
