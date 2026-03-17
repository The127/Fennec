using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Helpers;
using Fennec.App.Messages;
using Fennec.App.Models;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Shortcuts;
using Fennec.Client;
using HubStatus = Fennec.Client.HubConnectionStatus;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using Microsoft.Extensions.Logging;
using Avalonia.Media.Imaging;
using NodaTime;
using NodaTime.Text;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class VoiceParticipantItem(Guid userId, string username, string? instanceUrl) : ObservableObject
{
    public Guid UserId { get; } = userId;
    public string Username { get; } = username;
    public string? InstanceUrl { get; } = instanceUrl;
    public string Identity => instanceUrl is not null ? $"{username}@{instanceUrl}" : username;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isDeafened;

    [ObservableProperty]
    private bool _isSpeaking;

    [ObservableProperty]
    private bool _isScreenSharing;

    [ObservableProperty]
    private string _peerState = "new";
}

public partial class ChannelItem(Guid id, string name, ChannelType channelType, Guid channelGroupId) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public ChannelType ChannelType { get; } = channelType;
    public Guid ChannelGroupId { get; } = channelGroupId;
    public bool IsTextOnly => ChannelType == ChannelType.TextOnly;

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<VoiceParticipantItem> VoiceParticipants { get; } = [];
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

public class MemberItem(string username, string? instanceUrl, bool isOnline)
{
    public string Username { get; } = username;
    public string? InstanceUrl { get; } = instanceUrl;
    public string Identity => InstanceUrl is not null ? $"{Username}@{InstanceUrl}" : Username;
    public bool IsOnline { get; } = isOnline;
}

public class AttachmentItem(string fileName, Uri path)
{
    public string FileName { get; } = fileName;
    public Uri Path { get; } = path;
}

public partial class MessageItem : ObservableObject
{
    [ObservableProperty]
    private Guid _messageId;

    public required string Content { get; init; }
    public required Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorInstanceUrl { get; init; }
    public string AuthorIdentity => AuthorInstanceUrl is not null ? $"{AuthorName}@{AuthorInstanceUrl}" : AuthorName;
    public required string AvatarFallback { get; init; }
    public required string CreatedAt { get; init; }
    public required string LocalTime { get; init; }
    public required string ExactTime { get; init; }
    public required bool ShowAuthor { get; init; }
    public required bool ShowTimeSeparator { get; init; }
    public required string TimeSeparatorText { get; init; }

    [ObservableProperty]
    private bool _isPending;

    [ObservableProperty]
    private bool _isSendFailed;

    [ObservableProperty]
    private bool _isSelected;
    public bool IsEmojiOnly => !string.IsNullOrWhiteSpace(Content) && EmojiHelper.IsAllEmoji(Content);
}

public partial class ServerViewModel : ObservableObject, IShortcutHandler, ISearchableRoute,
    IRecipient<ChannelMessageReceivedMessage>,
    IRecipient<VoiceParticipantJoinedMessage>,
    IRecipient<VoiceParticipantLeftMessage>,
    IRecipient<VoiceStateChangedMessage>,
    IRecipient<HubConnectionStateChangedMessage>,
    IRecipient<UserOnlineMessage>,
    IRecipient<UserOfflineMessage>,
    IRecipient<VoiceMuteStateChangedMessage>,
    IRecipient<VoiceDeafenStateChangedMessage>,
    IRecipient<VoiceSpeakingChangedMessage>,
    IRecipient<VoiceMuteToggledMessage>,
    IRecipient<VoiceDeafenToggledMessage>,
    IRecipient<ScreenShareStartedMessage>,
    IRecipient<ScreenShareStoppedMessage>,
    IRecipient<ScreenShareFrameMessage>,
    IRecipient<ScreenShareCursorMessage>,
    IRecipient<ScreenSharePopOutRequestedMessage>,
    IRecipient<VoicePeerStateChangedMessage>,
    IRecipient<ScreenSharePopOutClosedMessage>,
    IRecipient<ControlWatchScreenShareMessage>,
    IRecipient<ControlUnwatchScreenShareMessage>
{
    private readonly IFennecClient client;
    private readonly DialogManager dialogManager;
    private readonly IServerStore serverStore;
    private readonly IMessageHubService _messageHubService;
    private readonly IVoiceCallService _voiceCallService;
    private readonly IMessenger _messenger;
    private readonly ILogger<ServerViewModel> _logger;
    private readonly ToastManager _toastManager;
    private readonly ISettingsStore _settingsStore;
    private readonly string instanceUrl;
    private readonly string _currentUsername;
    private readonly Guid _currentUserId;

    public ServerViewModel(IFennecClient client, DialogManager dialogManager, IServerStore serverStore, IMessageHubService messageHubService, IVoiceCallService voiceCallService, IMessenger messenger, ToastManager toastManager, ILogger<ServerViewModel> logger, ISettingsStore settingsStore, Guid serverId, string serverName, string instanceUrl, Guid currentUserId, string currentUsername)
    {
        this.client = client;
        this.dialogManager = dialogManager;
        this.serverStore = serverStore;
        _messageHubService = messageHubService;
        _voiceCallService = voiceCallService;
        _messenger = messenger;
        _toastManager = toastManager;
        _logger = logger;
        _settingsStore = settingsStore;
        ServerId = serverId;
        _serverName = serverName;
        this.instanceUrl = instanceUrl;
        _currentUserId = currentUserId;
        _currentUsername = currentUsername;

        messenger.Register<ChannelMessageReceivedMessage>(this);
        messenger.Register<VoiceParticipantJoinedMessage>(this);
        messenger.Register<VoiceParticipantLeftMessage>(this);
        messenger.Register<VoiceStateChangedMessage>(this);
        messenger.Register<HubConnectionStateChangedMessage>(this);
        messenger.Register<UserOnlineMessage>(this);
        messenger.Register<UserOfflineMessage>(this);
        messenger.Register<VoiceMuteStateChangedMessage>(this);
        messenger.Register<VoiceDeafenStateChangedMessage>(this);
        messenger.Register<VoiceSpeakingChangedMessage>(this);
        messenger.Register<VoiceMuteToggledMessage>(this);
        messenger.Register<VoiceDeafenToggledMessage>(this);
        messenger.Register<ScreenShareStartedMessage>(this);
        messenger.Register<ScreenShareStoppedMessage>(this);
        messenger.Register<ScreenShareFrameMessage>(this);
        messenger.Register<ScreenShareCursorMessage>(this);
        messenger.Register<ScreenSharePopOutRequestedMessage>(this);
        messenger.Register<VoicePeerStateChangedMessage>(this);
        messenger.Register<ScreenSharePopOutClosedMessage>(this);
        messenger.Register<ControlWatchScreenShareMessage>(this);
        messenger.Register<ControlUnwatchScreenShareMessage>(this);

        // Initialize hub status from current state (message may have been sent before registration)
        (HubStatusText, HubStatusColor) = messageHubService.CurrentStatus switch
        {
            HubStatus.Connected => ("Connected", "#4CAF50"),
            HubStatus.Connecting => ("Connecting...", "#FFC107"),
            HubStatus.Reconnecting => ("Reconnecting...", "#FFC107"),
            HubStatus.Disconnected => ("Disconnected", "#F44336"),
            _ => ("Unknown", "#808080"),
        };

        // Restore voice state if already in a call on this server
        if (voiceCallService.IsConnected && voiceCallService.CurrentServerId == serverId)
        {
            IsInVoiceChannel = true;
            CurrentVoiceChannelId = voiceCallService.CurrentChannelId;
            IsMuted = voiceCallService.IsMuted;
            IsDeafened = voiceCallService.IsDeafened;
            IsScreenSharing = voiceCallService.IsScreenSharing;

            foreach (var sharer in voiceCallService.ActiveScreenSharers)
            {
                ActiveScreenShares.Add(new ScreenShareInfo(sharer.UserId, sharer.Username, sharer.InstanceUrl));
            }
        }
    }
    [ObservableProperty]
    private string _serverName;

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

    public Guid ServerId { get; }

    public List<string> ServerMembers { get; private set; } = [];

    public ObservableCollection<MemberItem> OnlineMembers { get; } = [];
    public ObservableCollection<MemberItem> OfflineMembers { get; } = [];

    [ObservableProperty]
    private int _onlineMemberCount;

    [ObservableProperty]
    private int _offlineMemberCount;

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

        try
        {
            await _messageHubService.SubscribeToChannelAsync(ServerId, channel.Id);
            SubscribedChannelName = channel.Name;
        }
        catch (Exception ex)
        {
            SubscribedChannelName = null;
            _logger.LogWarning(ex, "Failed to subscribe to channel");
        }

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

        var now = SystemClock.Instance.GetCurrentInstant();
        var nowString = InstantPattern.ExtendedIso.Format(now);

        var optimistic = BuildMessageItem(
            messageId: Guid.NewGuid(),
            content: content,
            authorId: Guid.Empty,
            authorName: _currentUsername,
            authorInstanceUrl: null,
            createdAt: nowString);
        optimistic.IsPending = true;

        Messages.Add(optimistic);
        _allMessages.Add(optimistic);

        try
        {
            var response = await client.Server.SendMessageAsync(instanceUrl, ServerId, SelectedChannel.Id, new SendMessageRequestDto
            {
                Content = content,
            });

            // SignalR may have already delivered the real message
            var alreadyDelivered = Messages.FirstOrDefault(m => m.MessageId == response.MessageId);
            if (alreadyDelivered is not null)
            {
                Messages.Remove(optimistic);
                _allMessages.Remove(optimistic);
            }
            else
            {
                optimistic.MessageId = response.MessageId;
                optimistic.IsPending = false;
            }
        }
        catch
        {
            optimistic.IsPending = false;
            optimistic.IsSendFailed = true;
            MessageText = content;
        }
    }

    private MessageItem BuildMessageItem(Guid messageId, string content, Guid authorId, string authorName, string? authorInstanceUrl, string createdAt)
    {
        var lastMessage = Messages.LastOrDefault();
        var parsed = InstantPattern.ExtendedIso.Parse(createdAt);
        var timestamp = parsed.Success ? parsed.Value : (Instant?)null;
        var zone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        var msgDate = timestamp?.InZone(zone).Date;

        var lastAuthorId = lastMessage?.AuthorId;
        Instant? lastTimestamp = null;
        if (lastMessage is not null)
        {
            var lastParsed = InstantPattern.ExtendedIso.Parse(lastMessage.CreatedAt);
            if (lastParsed.Success) lastTimestamp = lastParsed.Value;
        }
        var lastDate = lastTimestamp?.InZone(zone).Date;

        var isNewDay = lastDate is not null && msgDate is not null && msgDate != lastDate;
        var hasTimeGap = lastTimestamp is not null && timestamp is not null
            && (timestamp.Value - lastTimestamp.Value).TotalMinutes >= 5;
        var showAuthor = authorId != lastAuthorId || hasTimeGap || isNewDay;

        return new MessageItem
        {
            MessageId = messageId,
            Content = content,
            AuthorId = authorId,
            AuthorName = authorName,
            AuthorInstanceUrl = authorInstanceUrl,
            AvatarFallback = authorName.Length > 0 ? authorName[..1].ToUpper() : "?",
            CreatedAt = createdAt,
            LocalTime = FormatLocalTime(createdAt),
            ExactTime = FormatExactTime(createdAt),
            ShowAuthor = showAuthor,
            ShowTimeSeparator = isNewDay,
            TimeSeparatorText = isNewDay ? FormatTimeSeparator(createdAt) : "",
        };
    }

    public void Receive(ChannelMessageReceivedMessage message)
    {
        if (message.ServerId != ServerId || SelectedChannel is null || message.ChannelId != SelectedChannel.Id)
            return;

        var dto = message.Message;

        Dispatcher.UIThread.Post(() =>
        {
            // Dedup: skip if this message was already added (e.g. optimistic send)
            if (Messages.Any(m => m.MessageId == dto.MessageId))
                return;

            var item = BuildMessageItem(dto.MessageId, dto.Content, dto.AuthorId, dto.AuthorName, dto.AuthorInstanceUrl, dto.CreatedAt);

            Messages.Add(item);
            _allMessages.Add(item);
        });
    }

    // --- Hub connection status ---

    [ObservableProperty]
    private string _hubStatusText = "Disconnected";

    [ObservableProperty]
    private string _hubStatusColor = "#808080";

    [ObservableProperty]
    private string? _subscribedChannelName;

    public void Receive(HubConnectionStateChangedMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            (HubStatusText, HubStatusColor) = message.Status switch
            {
                HubStatus.Connected => ("Connected", "#4CAF50"),
                HubStatus.Connecting => ("Connecting...", "#FFC107"),
                HubStatus.Reconnecting => ("Reconnecting...", "#FFC107"),
                HubStatus.Disconnected => ("Disconnected", "#F44336"),
                _ => ("Unknown", "#808080"),
            };
        });
    }

    // --- Presence ---

    private readonly Dictionary<Guid, (string Username, string? InstanceUrl)> _onlineUsers = new();
    private List<ListServerMembersResponseItemDto> _allMembers = [];

    public void Receive(UserOnlineMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _onlineUsers[message.UserId] = (message.Username, message.InstanceUrl);
            RebuildMemberLists();
        });
    }

    public void Receive(UserOfflineMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _onlineUsers.Remove(message.UserId);
            RebuildMemberLists();
        });
    }

    private void RebuildMemberLists()
    {
        OnlineMembers.Clear();
        OfflineMembers.Clear();

        var onlineKeys = new HashSet<string>(_onlineUsers.Values.Select(p => MemberKey(p.Username, p.InstanceUrl)));

        foreach (var member in _allMembers)
        {
            var key = MemberKey(member.Name, member.InstanceUrl);
            if (onlineKeys.Contains(key))
                OnlineMembers.Add(new MemberItem(member.Name, member.InstanceUrl, true));
            else
                OfflineMembers.Add(new MemberItem(member.Name, member.InstanceUrl, false));
        }

        OnlineMemberCount = OnlineMembers.Count;
        OfflineMemberCount = OfflineMembers.Count;
    }

    private static string MemberKey(string name, string? instanceUrl) =>
        instanceUrl is not null ? $"{name}@{instanceUrl}" : name;

    // --- User Profile Actions ---

    [RelayCommand]
    private void AddFriend()
    {
        _toastManager.CreateToast("Not Implemented")
            .WithContent("Adding friends is not yet available.")
            .WithDelay(3)
            .ShowWarning();
    }

    [RelayCommand]
    private void SendDirectMessage()
    {
        _toastManager.CreateToast("Not Implemented")
            .WithContent("Direct messages are not yet available.")
            .WithDelay(3)
            .ShowWarning();
    }

    // --- Voice ---

    [ObservableProperty]
    private bool _isInVoiceChannel;

    [ObservableProperty]
    private Guid? _currentVoiceChannelId;

    [ObservableProperty]
    private string? _currentVoiceChannelName;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isDeafened;

    [RelayCommand]
    private async Task JoinVoiceChannel(ChannelItem channel)
    {
        if (channel.IsTextOnly) return;

        // If already in this channel, do nothing
        if (IsInVoiceChannel && CurrentVoiceChannelId == channel.Id) return;

        try
        {
            await _voiceCallService.JoinAsync(ServerId, channel.Id, instanceUrl, _currentUserId, _currentUsername);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to join voice channel");
        }
    }

    [RelayCommand]
    private async Task LeaveVoiceChannel()
    {
        try
        {
            await _voiceCallService.LeaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to leave voice channel");
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _voiceCallService.SetMuted(IsMuted);
        UpdateLocalParticipantMuteState();
    }

    [RelayCommand]
    private void ToggleDeafen()
    {
        IsDeafened = !IsDeafened;
        _voiceCallService.SetDeafened(IsDeafened);
        if (IsDeafened)
            IsMuted = true;
        UpdateLocalParticipantMuteState();
    }

    public void Receive(VoiceParticipantJoinedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            if (channel is null) return;

            // Avoid duplicates
            if (channel.VoiceParticipants.Any(p => p.UserId == message.UserId))
                return;

            channel.VoiceParticipants.Add(new VoiceParticipantItem(message.UserId, message.Username, message.InstanceUrl)
            {
                IsMuted = message.IsMuted,
                IsDeafened = message.IsDeafened,
                IsScreenSharing = message.IsScreenSharing,
            });
        });
    }

    public void Receive(VoiceParticipantLeftMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            if (channel is null) return;

            var participant = channel.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                channel.VoiceParticipants.Remove(participant);
        });
    }

    public void Receive(VoiceStateChangedMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsInVoiceChannel = message.IsConnected;
            CurrentVoiceChannelId = message.ChannelId;

            if (message.IsConnected && message.ChannelId is not null)
            {
                var channel = FindChannel(message.ChannelId.Value);
                CurrentVoiceChannelName = channel?.Name;
            }
            else
            {
                CurrentVoiceChannelName = null;
                IsMuted = false;
                IsDeafened = false;

                // Reset screen share state
                ActiveScreenShares.Clear();
                WatchedScreenShares.Clear();
                HasWatchedShares = false;
                FocusedScreenShareUserId = null;
                IsScreenSharing = false;
                IsScreenShareMaximized = false;
                ScreenShareFrame = null;
                _poppedOutUserIds.Clear();

                // Reset all speaking indicators
                foreach (var group in ChannelGroups)
                    foreach (var ch in group.Channels)
                        foreach (var p in ch.VoiceParticipants)
                            p.IsSpeaking = false;
            }
        });
    }

    private ChannelItem? FindChannel(Guid channelId)
    {
        foreach (var group in ChannelGroups)
        {
            var channel = group.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is not null) return channel;
        }
        return null;
    }

    private void UpdateLocalParticipantMuteState()
    {
        if (CurrentVoiceChannelId is null) return;
        var channel = FindChannel(CurrentVoiceChannelId.Value);
        var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == _currentUserId);
        if (participant is not null)
        {
            participant.IsMuted = IsMuted;
            participant.IsDeafened = IsDeafened;
        }
    }

    public void Receive(VoiceMuteStateChangedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsMuted = message.IsMuted;
        });
    }

    public void Receive(VoiceDeafenStateChangedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsDeafened = message.IsDeafened;
        });
    }

    public void Receive(VoiceSpeakingChangedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsSpeaking = message.IsSpeaking;
        });
    }

    public void Receive(VoicePeerStateChangedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.PeerState = message.State;
        });
    }

    public void Receive(VoiceMuteToggledMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsMuted = message.IsMuted;
            if (message.IsMuted && IsDeafened)
            {
                // Muting while deafened is fine, state is already consistent
            }
            UpdateLocalParticipantMuteState();
        });
    }

    public void Receive(VoiceDeafenToggledMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsDeafened = message.IsDeafened;
            if (message.IsDeafened)
                IsMuted = true;
            UpdateLocalParticipantMuteState();
        });
    }

    // --- Screen Share ---

    public class ScreenShareInfo(Guid userId, string username, string? instanceUrl)
    {
        public Guid UserId { get; } = userId;
        public string Username { get; } = username;
        public string? InstanceUrl { get; } = instanceUrl;
    }

    public ObservableCollection<ScreenShareInfo> ActiveScreenShares { get; } = [];
    public ObservableCollection<ScreenShareInfo> WatchedScreenShares { get; } = [];

    [ObservableProperty]
    private bool _hasWatchedShares;

    private readonly HashSet<Guid> _poppedOutUserIds = [];

    [ObservableProperty]
    private Guid? _focusedScreenShareUserId;

    [ObservableProperty]
    private bool _showOwnScreenShare;

    [ObservableProperty]
    private bool _showDebugOverlay;

    public ScreenShareMetrics? DebugMetrics => FocusedScreenShareUserId is { } uid ? _voiceCallService.GetMetrics(uid) : null;

    private int _queueDepth;

    partial void OnFocusedScreenShareUserIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(DebugMetrics));
    }

    [ObservableProperty]
    private bool _isScreenSharing;

    [ObservableProperty]
    private bool _showTileView;

    [ObservableProperty]
    private WriteableBitmap? _screenShareFrame;

    [ObservableProperty]
    private double _cursorX;

    [ObservableProperty]
    private double _cursorY;

    [ObservableProperty]
    private CursorType _cursorShape;

    [ObservableProperty]
    private bool _cursorVisible = true;

    public List<string> ShareResolutionOptions { get; } = ["720p", "1080p", "1440p", "Native"];
    public List<int> ShareFrameRateOptions { get; } = [15, 30, 60];

    [ObservableProperty]
    private string _shareResolution = "1080p";

    [ObservableProperty]
    private int _shareBitrateKbps = 1500;

    [ObservableProperty]
    private int _shareFrameRate = 30;

    [RelayCommand]
    private async Task StartScreenShare()
    {
        try
        {
            if (_voiceCallService.IsNativePickerAvailable)
            {
                // macOS 14+: use native picker with saved settings
                var settings = await _settingsStore.LoadAsync();
                ShareResolution = settings.ScreenShareResolution;
                ShareBitrateKbps = settings.ScreenShareBitrateKbps;
                ShareFrameRate = settings.ScreenShareFrameRate;
                await _voiceCallService.StartScreenShareWithPickerAsync(
                    ShareResolution, ShareBitrateKbps, ShareFrameRate);
            }
            else
            {
                // Fallback: custom picker dialog
                var targets = await _voiceCallService.GetScreenShareTargetsAsync();
                if (targets.Count == 0)
                {
                    _logger.LogWarning("No screen share targets available");
                    return;
                }

                var settings = await _settingsStore.LoadAsync();
                var vm = new ScreenSharePickerViewModel(dialogManager, _settingsStore, settings, targets);
                dialogManager.CreateDialog(vm)
                    .Dismissible()
                    .WithSuccessCallback<ScreenSharePickerViewModel>(async ctx =>
                    {
                        if (ctx.Result is not null)
                        {
                            ShareResolution = ctx.Result.Resolution;
                            ShareBitrateKbps = ctx.Result.BitrateKbps;
                            ShareFrameRate = ctx.Result.FrameRate;
                            await _voiceCallService.StartScreenShareAsync(
                                ctx.Result.Target, ctx.Result.Resolution, ctx.Result.BitrateKbps, ctx.Result.FrameRate);
                        }
                    })
                    .Show();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start screen share");
        }
    }

    [RelayCommand]
    private async Task UpdateScreenShareSettings()
    {
        try
        {
            await _voiceCallService.UpdateScreenShareSettingsAsync(ShareResolution, ShareBitrateKbps, ShareFrameRate);

            // Persist as new defaults
            var settings = await _settingsStore.LoadAsync();
            settings.ScreenShareResolution = ShareResolution;
            settings.ScreenShareBitrateKbps = ShareBitrateKbps;
            settings.ScreenShareFrameRate = ShareFrameRate;
            await _settingsStore.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update screen share settings");
        }
    }

    [RelayCommand]
    private async Task StopScreenShare()
    {
        try
        {
            await _voiceCallService.StopScreenShareAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop screen share");
        }
    }

    [ObservableProperty]
    private bool _isScreenShareMaximized;

    [RelayCommand]
    private void ToggleTileView()
    {
        ShowTileView = !ShowTileView;
    }

    [RelayCommand]
    private void ToggleScreenShareMaximize()
    {
        IsScreenShareMaximized = !IsScreenShareMaximized;
    }

    [RelayCommand]
    private void ExitScreenShareMaximize()
    {
        IsScreenShareMaximized = false;
    }

    [RelayCommand]
    private void PopOutScreenShare()
    {
        if (FocusedScreenShareUserId is null) return;
        var share = ActiveScreenShares.FirstOrDefault(s => s.UserId == FocusedScreenShareUserId);
        if (share is null) return;
        _messenger.Send(new ScreenSharePopOutRequestedMessage(share.UserId, share.Username));
    }

    [RelayCommand]
    private void ToggleShowOwnScreenShare()
    {
        ShowOwnScreenShare = !ShowOwnScreenShare;

        if (ShowOwnScreenShare)
        {
            // If own share is active and nothing focused, focus it
            if (FocusedScreenShareUserId is null && WatchedScreenShares.Any(s => s.UserId == _currentUserId))
                FocusedScreenShareUserId = _currentUserId;
        }
        else
        {
            // If own share is focused, move to next and clear stale frame
            if (FocusedScreenShareUserId == _currentUserId)
            {
                FocusedScreenShareUserId = FindNextFocusableShare(null);
                if (FocusedScreenShareUserId is null)
                    ScreenShareFrame = null;
            }
        }
    }

    [RelayCommand]
    private void WatchScreenShare(Guid userId)
    {
        if (WatchedScreenShares.Any(s => s.UserId == userId))
            return;

        var share = ActiveScreenShares.FirstOrDefault(s => s.UserId == userId);
        if (share is null) return;

        WatchedScreenShares.Add(share);
        HasWatchedShares = true;
        FocusedScreenShareUserId = userId;

        // Signal the sharer that we're watching
        _ = _voiceCallService.WatchScreenShareAsync(userId);
    }

    [RelayCommand]
    private void UnwatchScreenShare(Guid userId)
    {
        var share = WatchedScreenShares.FirstOrDefault(s => s.UserId == userId);
        if (share is null) return;

        // Signal the sharer that we stopped watching
        _ = _voiceCallService.UnwatchScreenShareAsync(userId);

        WatchedScreenShares.Remove(share);
        HasWatchedShares = WatchedScreenShares.Count > 0;

        if (FocusedScreenShareUserId == userId)
        {
            FocusedScreenShareUserId = FindNextFocusableShare(null);
            if (FocusedScreenShareUserId is null)
                ScreenShareFrame = null;
        }

        if (WatchedScreenShares.Count == 0)
            IsScreenShareMaximized = false;
    }

    public void Receive(ControlWatchScreenShareMessage message)
    {
        Dispatcher.UIThread.Post(() => WatchScreenShare(message.UserId));
    }

    public void Receive(ControlUnwatchScreenShareMessage message)
    {
        Dispatcher.UIThread.Post(() => UnwatchScreenShare(message.UserId));
    }

    [RelayCommand]
    private void FocusScreenShare(Guid userId)
    {
        FocusedScreenShareUserId = userId;
    }

    private Guid? FindNextFocusableShare(Guid? excludeUserId)
    {
        return WatchedScreenShares
            .Where(s => s.UserId != excludeUserId)
            .Where(s => !_poppedOutUserIds.Contains(s.UserId))
            .Where(s => s.UserId != _currentUserId || ShowOwnScreenShare)
            .FirstOrDefault()?.UserId;
    }

    public void Receive(ScreenSharePopOutRequestedMessage message)
    {
        _poppedOutUserIds.Add(message.UserId);

        if (FocusedScreenShareUserId == message.UserId)
        {
            FocusedScreenShareUserId = FindNextFocusableShare(message.UserId);
            ScreenShareFrame = null;
        }
    }

    public void Receive(ScreenSharePopOutClosedMessage message)
    {
        _poppedOutUserIds.Remove(message.UserId);

        // Re-focus if nothing is focused and this share is still watched
        if (FocusedScreenShareUserId is null && WatchedScreenShares.Any(s => s.UserId == message.UserId))
            FocusedScreenShareUserId = message.UserId;
    }

    public void Receive(ScreenShareStartedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (ActiveScreenShares.Any(s => s.UserId == message.UserId))
                return;

            ActiveScreenShares.Add(new ScreenShareInfo(message.UserId, message.Username, message.InstanceUrl));

            if (message.UserId == _currentUserId)
                IsScreenSharing = true;

            // Update participant indicator
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsScreenSharing = true;
        });
    }

    public void Receive(ScreenShareStoppedMessage message)
    {
        if (message.ServerId != ServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var share = ActiveScreenShares.FirstOrDefault(s => s.UserId == message.UserId);
            if (share != null)
                ActiveScreenShares.Remove(share);

            // Clean up watched + popped-out state for stopped share
            var watched = WatchedScreenShares.FirstOrDefault(s => s.UserId == message.UserId);
            if (watched != null)
            {
                WatchedScreenShares.Remove(watched);
                HasWatchedShares = WatchedScreenShares.Count > 0;
            }
            _poppedOutUserIds.Remove(message.UserId);

            if (FocusedScreenShareUserId == message.UserId)
                FocusedScreenShareUserId = FindNextFocusableShare(null);

            if (message.UserId == _currentUserId)
                IsScreenSharing = false;

            if (WatchedScreenShares.Count == 0)
            {
                ScreenShareFrame = null;
                IsScreenShareMaximized = false;
            }

            // Update participant indicator
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsScreenSharing = false;
        });
    }

    public void Receive(ScreenShareFrameMessage message)
    {
        // Only render frames from the focused share
        if (FocusedScreenShareUserId != message.UserId)
            return;

        // Don't render popped-out or own stream (when toggled off)
        if (_poppedOutUserIds.Contains(message.UserId))
            return;
        if (message.UserId == _currentUserId && !ShowOwnScreenShare)
            return;

        var metrics = DebugMetrics;
        Interlocked.Increment(ref _queueDepth);
        metrics?.QueueDepth.Add(Interlocked.CompareExchange(ref _queueDepth, 0, 0));

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Decrement(ref _queueDepth);

            try
            {
                var sw = Stopwatch.StartNew();

                if (ScreenShareFrame is null
                    || ScreenShareFrame.PixelSize.Width != message.Width
                    || ScreenShareFrame.PixelSize.Height != message.Height)
                {
                    ScreenShareFrame = new WriteableBitmap(
                        new Avalonia.PixelSize(message.Width, message.Height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Rgba8888,
                        Avalonia.Platform.AlphaFormat.Unpremul);
                }

                using (var frameBuffer = ScreenShareFrame.Lock())
                {
                    var srcStride = message.Width * 4;
                    var dstStride = frameBuffer.RowBytes;

                    if (srcStride == dstStride)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            message.RgbaData, 0, frameBuffer.Address, message.RgbaData.Length);
                    }
                    else
                    {
                        // Copy row-by-row to handle stride padding
                        for (int y = 0; y < message.Height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(
                                message.RgbaData, y * srcStride,
                                frameBuffer.Address + y * dstStride, srcStride);
                        }
                    }
                }

                sw.Stop();
                metrics?.BitmapCopyTimeMs.Add(sw.Elapsed.TotalMilliseconds);
                metrics?.FrameLagMs.Add(Stopwatch.GetElapsedTime(message.Timestamp).TotalMilliseconds);

                // Force re-render — writing pixels doesn't change the property reference
                OnPropertyChanged(nameof(ScreenShareFrame));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to render screen share frame");
            }
        });
    }

    public void Receive(ScreenShareCursorMessage message)
    {
        if (FocusedScreenShareUserId != message.UserId)
            return;

        if (_poppedOutUserIds.Contains(message.UserId))
            return;
        if (message.UserId == _currentUserId && !ShowOwnScreenShare)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            CursorX = message.X;
            CursorY = message.Y;
            CursorShape = message.Type;
            CursorVisible = message.IsVisible;
        });
    }

    // --- End Voice ---

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
                    AuthorInstanceUrl = msg.AuthorInstanceUrl,
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
        try
        {
            var membersResponse = await client.Server.ListMembersAsync(instanceUrl, ServerId);
            _allMembers = membersResponse.Members;
            ServerMembers = membersResponse.Members.Select(m => m.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server members");
        }

        // Subscribe to server-level events (voice presence)
        try
        {
            await _messageHubService.SubscribeToServerAsync(ServerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe to server group");
        }

        // Fetch initial presence
        try
        {
            var presence = await _messageHubService.GetServerPresenceAsync(ServerId);
            _onlineUsers.Clear();
            foreach (var entry in presence)
                _onlineUsers[entry.UserId] = (entry.Username, entry.InstanceUrl);
            RebuildMemberLists();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server presence");
            // Show all members as offline
            RebuildMemberLists();
        }

        var groups = await serverStore.GetChannelGroupsAsync(instanceUrl, client, ServerId);
        if (groups.Any())
        {
            ChannelGroups.Clear();
            foreach (var group in groups)
            {
                var channels = await serverStore.GetChannelsAsync(instanceUrl, client, ServerId, group.ChannelGroupId);
                var channelItems = channels
                    .Select(c => new ChannelItem(c.ChannelId, c.Name, c.ChannelType, c.ChannelGroupId))
                    .ToList();
                ChannelGroups.Add(new ChannelGroupItem(group.ChannelGroupId, group.Name, channelItems));
            }

            // Populate existing voice participants
            try
            {
                var voiceState = await _messageHubService.GetServerVoiceStateAsync(ServerId, instanceUrl);
                var screenSharerIds = _voiceCallService.ActiveScreenSharers
                    .Select(s => s.UserId).ToHashSet();
                foreach (var (channelId, participants) in voiceState)
                {
                    var channel = FindChannel(channelId);
                    if (channel is null) continue;
                    foreach (var p in participants)
                    {
                        var item = new VoiceParticipantItem(p.UserId, p.Username, p.InstanceUrl);
                        if (screenSharerIds.Contains(p.UserId))
                            item.IsScreenSharing = true;
                        channel.VoiceParticipants.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch voice state");
            }

            // Restore voice channel name now that channels are loaded
            if (IsInVoiceChannel && CurrentVoiceChannelId is not null && CurrentVoiceChannelName is null)
            {
                var voiceChannel = FindChannel(CurrentVoiceChannelId.Value);
                CurrentVoiceChannelName = voiceChannel?.Name;
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
    }
}
