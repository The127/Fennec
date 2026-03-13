using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Models;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Shortcuts;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using NodaTime;
using NodaTime.Text;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class ChannelItem(Guid id, string name, ChannelType channelType, Guid channelGroupId) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public ChannelType ChannelType { get; } = channelType;
    public Guid ChannelGroupId { get; } = channelGroupId;
    public bool IsTextOnly => ChannelType == ChannelType.TextOnly;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class ChannelGroupItem(Guid id, string name, List<ChannelItem> channels) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    private string _name = name;

    public List<ChannelItem> Channels { get; } = channels;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renamingText = name;
}

public class AttachmentItem(string fileName, Uri path)
{
    public string FileName { get; } = fileName;
    public Uri Path { get; } = path;
}

public partial class MessageItem : ObservableObject
{
    public required Guid MessageId { get; init; }
    public required string Content { get; init; }
    public required Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required string AvatarFallback { get; init; }
    public required string CreatedAt { get; init; }
    public required string LocalTime { get; init; }
    public required string ExactTime { get; init; }
    public required bool ShowAuthor { get; init; }
    public required bool ShowTimeSeparator { get; init; }
    public required string TimeSeparatorText { get; init; }

    [ObservableProperty]
    private bool _isSelected;
    public bool IsEmojiOnly => !string.IsNullOrWhiteSpace(Content) && IsAllEmoji(Content);

    private static bool IsAllEmoji(string text)
    {
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        var hasNonWhitespace = false;
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (string.IsNullOrWhiteSpace(element))
                continue;
            hasNonWhitespace = true;
            if (!IsEmoji(element))
                return false;
        }
        return hasNonWhitespace;
    }

    private static bool IsEmoji(string textElement)
    {
        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;
            // Allow variation selectors and zero-width joiner (combining chars in emoji sequences)
            if (value == 0xFE0F || value == 0xFE0E || value == 0x200D)
                continue;
            // Allow skin tone modifiers
            if (value >= 0x1F3FB && value <= 0x1F3FF)
                continue;
            // Allow tag characters (flag sequences)
            if (value >= 0xE0020 && value <= 0xE007F)
                continue;
            // Common emoji ranges
            if (value >= 0x1F600 && value <= 0x1F64F) continue; // Emoticons
            if (value >= 0x1F300 && value <= 0x1F5FF) continue; // Misc symbols & pictographs
            if (value >= 0x1F680 && value <= 0x1F6FF) continue; // Transport & map
            if (value >= 0x1F900 && value <= 0x1F9FF) continue; // Supplemental symbols
            if (value >= 0x1FA00 && value <= 0x1FA6F) continue; // Chess symbols
            if (value >= 0x1FA70 && value <= 0x1FAFF) continue; // Symbols extended-A
            if (value >= 0x2600 && value <= 0x26FF) continue;   // Misc symbols
            if (value >= 0x2700 && value <= 0x27BF) continue;   // Dingbats
            if (value >= 0x231A && value <= 0x23F3) continue;   // Misc technical
            if (value >= 0x2934 && value <= 0x2935) continue;   // Arrows
            if (value >= 0x25AA && value <= 0x25FE) continue;   // Geometric shapes
            if (value >= 0x2B05 && value <= 0x2B55) continue;   // Arrows & shapes
            if (value >= 0x3030 && value <= 0x303D) continue;   // CJK symbols
            if (value == 0x00A9 || value == 0x00AE) continue;   // (C) (R)
            if (value == 0x2122 || value == 0x2139) continue;   // TM, info
            if (value >= 0x23E9 && value <= 0x23FA) continue;   // Media controls
            if (value >= 0x200D && value <= 0x200D) continue;   // ZWJ (already handled)
            if (value >= 0x20E3 && value <= 0x20E3) continue;   // Combining enclosing keycap
            if (value >= 0x0030 && value <= 0x0039) continue;   // Digits (keycap sequences)
            if (value == 0x002A || value == 0x0023) continue;   // * and # (keycap sequences)
            if (value >= 0x1F1E0 && value <= 0x1F1FF) continue; // Regional indicators (flags)
            return false;
        }
        return true;
    }
}

public partial class ServerViewModel(IFennecClient client, DialogManager dialogManager, IServerStore serverStore, Guid serverId, string serverName, string instanceUrl) : ObservableObject, IShortcutHandler, ISearchableRoute
{
    [ObservableProperty]
    private string _serverName = serverName;

    [ObservableProperty]
    private ChannelItem? _selectedChannel;

    [ObservableProperty]
    private string _messageText = "";

    public const int MaxMessageLength = 10_000;
    private const int CharCountVisibleThreshold = 9_000;

    public int MessageCharsRemaining => MaxMessageLength - MessageText.Length;
    public bool ShowCharCount => MessageText.Length >= CharCountVisibleThreshold;
    public bool IsOverLimit => MessageText.Length > MaxMessageLength;

    partial void OnMessageTextChanged(string value)
    {
        OnPropertyChanged(nameof(MessageCharsRemaining));
        OnPropertyChanged(nameof(ShowCharCount));
        OnPropertyChanged(nameof(IsOverLimit));
    }

    public Guid ServerId { get; } = serverId;

    public ShortcutContext ShortcutContext => ShortcutContext.Server;

    public bool HandleShortcut(string shortcutId)
    {
        switch (shortcutId)
        {
            case "server.focusMessage":
                ClearMessageSelection();
                MessageInputFocusRequested?.Invoke();
                return true;
            case "server.openEmoji":
                EmojiPickerRequested?.Invoke();
                return true;
            case "server.attachFile":
                AttachFileRequested?.Invoke();
                return true;
            default:
                return false;
        }
    }

    public event Action? MessageInputFocusRequested;
    public event Action? EmojiPickerRequested;
    public event Action? AttachFileRequested;
    private MessageItem? _lastClickedMessage;

    public void SelectMessage(MessageItem message, bool isShift, bool isCtrl)
    {
        if (isShift && _lastClickedMessage is not null)
        {
            // Range select from last clicked to current
            var startIdx = Messages.IndexOf(_lastClickedMessage);
            var endIdx = Messages.IndexOf(message);
            if (startIdx < 0 || endIdx < 0) return;

            if (startIdx > endIdx)
                (startIdx, endIdx) = (endIdx, startIdx);

            if (!isCtrl)
                ClearMessageSelection();

            for (var i = startIdx; i <= endIdx; i++)
                Messages[i].IsSelected = true;
        }
        else if (isCtrl)
        {
            message.IsSelected = !message.IsSelected;
        }
        else
        {
            ClearMessageSelection();
            message.IsSelected = true;
        }

        _lastClickedMessage = message;
    }

    public void ClearMessageSelection()
    {
        foreach (var m in Messages)
            m.IsSelected = false;
    }

    public bool HasSelectedMessages => Messages.Any(m => m.IsSelected);

    public string? GetSelectedMessagesText()
    {
        var selected = Messages.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0) return null;

        return string.Join(Environment.NewLine, selected.Select(m => m.Content));
    }

    public ObservableCollection<ChannelGroupItem> ChannelGroups { get; } = [];

    public ObservableCollection<MessageItem> Messages { get; } = [];

    public ObservableCollection<AttachmentItem> Attachments { get; } = [];

    public void AddAttachments(IReadOnlyList<Avalonia.Platform.Storage.IStorageFile> files)
    {
        foreach (var file in files)
            Attachments.Add(new AttachmentItem(file.Name, file.Path));
    }

    [RelayCommand]
    private void RemoveAttachment(AttachmentItem item)
    {
        Attachments.Remove(item);
    }

    [RelayCommand]
    private async Task SelectChannel(ChannelItem channel)
    {
        if (SelectedChannel is not null)
            SelectedChannel.IsSelected = false;

        SelectedChannel = channel;
        channel.IsSelected = true;
        await LoadMessagesAsync();
        MessageInputFocusRequested?.Invoke();
    }

    [RelayCommand]
    private async Task CreateChannelGroup()
    {
        try
        {
            await client.Server.CreateChannelGroupAsync(instanceUrl, ServerId, new CreateChannelGroupRequestDto
            {
                Name = "New Group",
            });

            await LoadAsync();

            // Start renaming the newly created group
            var newGroup = ChannelGroups.LastOrDefault();
            if (newGroup is not null)
            {
                newGroup.RenamingText = "";
                newGroup.IsRenaming = true;
            }
        }
        catch
        {
            // Failed to create channel group.
        }
    }

    [RelayCommand]
    private void StartRenameChannelGroup(ChannelGroupItem group)
    {
        group.RenamingText = group.Name;
        group.IsRenaming = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameChannelGroup(ChannelGroupItem group)
    {
        if (string.IsNullOrWhiteSpace(group.RenamingText))
        {
            group.IsRenaming = false;
            return;
        }

        var newName = group.RenamingText.Trim();
        if (newName == group.Name)
        {
            group.IsRenaming = false;
            return;
        }

        try
        {
            await client.Server.RenameChannelGroupAsync(instanceUrl, ServerId, group.Id, new RenameChannelGroupRequestDto
            {
                Name = newName,
            });

            group.Name = newName;
        }
        catch
        {
            // Failed to rename.
        }
        finally
        {
            group.IsRenaming = false;
        }
    }

    [RelayCommand]
    private void CancelRenameChannelGroup(ChannelGroupItem group)
    {
        group.IsRenaming = false;
    }

    [RelayCommand]
    private void ShowAddChannelDialog(ChannelGroupItem group)
    {
        var vm = new CreateChannelDialogViewModel(dialogManager);
        dialogManager.CreateDialog(vm)
            .Dismissible()
            .WithSuccessCallback<CreateChannelDialogViewModel>(async ctx =>
            {
                try
                {
                    await client.Server.CreateChannelAsync(instanceUrl, ServerId, group.Id, new CreateChannelRequestDto
                    {
                        Name = ctx.ChannelName.Trim(),
                        ChannelType = ctx.ChannelType,
                    });

                    await LoadAsync();
                }
                catch
                {
                    // Failed to create channel.
                }
            })
            .Show();
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (SelectedChannel is null || string.IsNullOrWhiteSpace(MessageText) || IsOverLimit)
            return;

        var content = ReplaceShortcodes(MessageText.Trim());
        MessageText = "";

        try
        {
            await client.Server.SendMessageAsync(instanceUrl, ServerId, SelectedChannel.Id, new SendMessageRequestDto
            {
                Content = content,
            });

            await LoadMessagesAsync();
        }
        catch
        {
            // Message send failed — restore text so user can retry.
            MessageText = content;
        }
    }

    private async Task LoadMessagesAsync()
    {
        if (SelectedChannel is null)
            return;

        try
        {
            var response = await client.Server.ListMessagesAsync(instanceUrl, ServerId, SelectedChannel.Id);

            Messages.Clear();

            var zone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
            Guid? lastAuthorId = null;
            Instant? lastTimestamp = null;
            LocalDate? lastDate = null;
            foreach (var msg in response.Messages)
            {
                var parsed = InstantPattern.ExtendedIso.Parse(msg.CreatedAt);
                var timestamp = parsed.Success ? parsed.Value : (Instant?)null;
                var msgDate = timestamp?.InZone(zone).Date;

                var isNewDay = lastDate is not null && msgDate is not null && msgDate != lastDate;
                var hasTimeGap = lastTimestamp is not null && timestamp is not null
                    && (timestamp.Value - lastTimestamp.Value).TotalMinutes >= 5;

                var showAuthor = msg.AuthorId != lastAuthorId || hasTimeGap || isNewDay;
                lastAuthorId = msg.AuthorId;
                lastTimestamp = timestamp;
                lastDate = msgDate;

                Messages.Add(new MessageItem
                {
                    MessageId = msg.MessageId,
                    Content = msg.Content,
                    AuthorId = msg.AuthorId,
                    AuthorName = msg.AuthorName,
                    AvatarFallback = msg.AuthorName.Length > 0 ? msg.AuthorName[..1].ToUpper() : "?",
                    CreatedAt = msg.CreatedAt,
                    LocalTime = FormatLocalTime(msg.CreatedAt),
                    ExactTime = FormatExactTime(msg.CreatedAt),
                    ShowAuthor = showAuthor,
                    ShowTimeSeparator = isNewDay,
                    TimeSeparatorText = isNewDay ? FormatTimeSeparator(msg.CreatedAt) : "",
                });
            }

            _allMessages = Messages.ToList();
        }
        catch
        {
            // Failed to load messages.
        }
    }

    private static string FormatLocalTime(string instantString)
    {
        var result = InstantPattern.ExtendedIso.Parse(instantString);
        if (!result.Success) return "";

        var local = result.Value.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
        var now = SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());

        if (local.Date == now.Date)
            return local.ToString("HH:mm", null);

        if (local.Year == now.Year)
            return local.ToString("MMM dd, HH:mm", null);

        return local.ToString("yyyy MMM dd, HH:mm", null);
    }

    private static string FormatExactTime(string instantString)
    {
        var result = InstantPattern.ExtendedIso.Parse(instantString);
        if (!result.Success) return "";

        var local = result.Value.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
        return local.ToString("dddd, MMMM d, yyyy 'at' HH:mm:ss", null);
    }

    private static string FormatTimeSeparator(string instantString)
    {
        var result = InstantPattern.ExtendedIso.Parse(instantString);
        if (!result.Success) return "";

        var local = result.Value.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
        var now = SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());

        if (local.Date == now.Date)
            return "Today";

        if (local.Date == now.Date.PlusDays(-1))
            return "Yesterday";

        return local.ToString("MMMM d, yyyy", null);
    }

    internal static string ReplaceShortcodes(string text)
    {
        return Regex.Replace(text, @":([a-z0-9_]+):", match =>
            EmojiDatabase.ByShortcode.TryGetValue(match.Groups[1].Value, out var entry)
                ? entry.Unicode
                : match.Value);
    }

    public string SearchWatermark => "Search messages...";

    private List<MessageItem> _allMessages = [];

    public void ApplySearch(string query)
    {
        Messages.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var msg in _allMessages)
                Messages.Add(msg);
        }
        else
        {
            foreach (var msg in _allMessages)
            {
                if (msg.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || msg.AuthorName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    Messages.Add(msg);
                }
            }
        }
    }

    public void ClearSearch()
    {
        Messages.Clear();
        foreach (var msg in _allMessages)
            Messages.Add(msg);
    }

    public async Task LoadAsync()
    {
        // Offline-first: Load from store
        var storedGroups = await serverStore.GetChannelGroupsAsync(ServerId);
        if (storedGroups.Any())
        {
            ChannelGroups.Clear();
            foreach (var group in storedGroups)
            {
                var storedChannels = await serverStore.GetChannelsAsync(ServerId, group.ChannelGroupId);
                var channels = storedChannels
                    .Select(c => new ChannelItem(c.ChannelId, c.Name, c.ChannelType, c.ChannelGroupId))
                    .ToList();
                ChannelGroups.Add(new ChannelGroupItem(group.ChannelGroupId, group.Name, channels));
            }

            if (SelectedChannel is null)
            {
                var firstChannel = ChannelGroups.FirstOrDefault()?.Channels.FirstOrDefault();
                if (firstChannel is not null)
                {
                    await SelectChannel(firstChannel);
                }
            }
        }

        try
        {
            var groupsResponse = await client.Server.ListChannelGroupsAsync(instanceUrl, ServerId);
            await serverStore.SetChannelGroupsAsync(ServerId, groupsResponse.ChannelGroups);

            ChannelGroups.Clear();

            foreach (var group in groupsResponse.ChannelGroups)
            {
                var channelsResponse = await client.Server.ListChannelsAsync(instanceUrl, ServerId, group.ChannelGroupId);
                await serverStore.SetChannelsAsync(ServerId, group.ChannelGroupId, channelsResponse.Channels);

                var channels = channelsResponse.Channels
                    .Select(c => new ChannelItem(c.ChannelId, c.Name, c.ChannelType, c.ChannelGroupId))
                    .ToList();

                ChannelGroups.Add(new ChannelGroupItem(group.ChannelGroupId, group.Name, channels));
            }

            if (SelectedChannel is null)
            {
                var firstChannel = ChannelGroups.FirstOrDefault()?.Channels.FirstOrDefault();
                if (firstChannel is not null)
                {
                    await SelectChannel(firstChannel);
                }
            }
        }
        catch
        {
            // Server unreachable — channels stay with what's in store.
        }
    }
}
