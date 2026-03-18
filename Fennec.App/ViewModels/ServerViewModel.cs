using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain;
using Fennec.App.Formatting;
using Fennec.App.Helpers;
using Fennec.App.Messages;
using Fennec.App.Models;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Shortcuts;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class VoiceParticipantItem(Guid userId, string username, string? instanceUrl) : ObservableObject
{
    public Guid UserId { get; } = userId;
    public string Username { get; } = username;
    public string? InstanceUrl { get; } = instanceUrl;
    public string Identity => new FederatedAddress(username, instanceUrl).ToString();

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
    public string Identity => new FederatedAddress(Username, InstanceUrl).ToString();
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
    public string AuthorIdentity => new FederatedAddress(AuthorName, AuthorInstanceUrl).ToString();
    public required string AvatarFallback { get; init; }
    public required string CreatedAt { get; init; }
    public required string LocalTime { get; init; }
    public required string ExactTime { get; init; }
    public required bool ShowAuthor { get; init; }
    public required bool ShowTimeSeparator { get; init; }
    public required string TimeSeparatorText { get; init; }

    private OutgoingMessageState? _sendState;
    public OutgoingMessageState? SendState
    {
        get => _sendState;
        set
        {
            SetProperty(ref _sendState, value);
            OnPropertyChanged(nameof(IsPending));
            OnPropertyChanged(nameof(IsSendFailed));
        }
    }

    public bool IsPending => SendState is PendingState;
    public bool IsSendFailed => SendState is FailedState;

    [ObservableProperty]
    private bool _isSelected;
    public bool IsEmojiOnly => !string.IsNullOrWhiteSpace(Content) && EmojiHelper.IsAllEmoji(Content);
}

public class ScreenShareInfo(Guid userId, string username, string? instanceUrl)
{
    public Guid UserId { get; } = userId;
    public string Username { get; } = username;
    public string? InstanceUrl { get; } = instanceUrl;
}

public partial class ServerViewModel : ObservableObject, IShortcutHandler, ISearchableRoute,
    IRecipient<ChannelMessageReceivedMessage>,
    IRecipient<HubConnectionStateChangedMessage>
{
    private readonly IFennecClient client;
    private readonly DialogManager dialogManager;
    private readonly IServerStore serverStore;
    private readonly IMessageHubService _messageHubService;
    private readonly IMessenger _messenger;
    private readonly ILogger<ServerViewModel> _logger;
    private readonly string instanceUrl;
    private readonly Guid _currentUserId;

    public ServerViewModel(
        IFennecClient client,
        DialogManager dialogManager,
        IServerStore serverStore,
        IMessageHubService messageHubService,
        IVoiceCallService voiceCallService,
        IMessenger messenger,
        ToastManager toastManager,
        ILogger<ServerViewModel> logger,
        ISettingsStore settingsStore,
        ILoggerFactory loggerFactory,
        Guid serverId,
        string serverName,
        string instanceUrl,
        Guid currentUserId,
        string currentUsername)
    {
        this.client = client;
        this.dialogManager = dialogManager;
        this.serverStore = serverStore;
        _messageHubService = messageHubService;
        _messenger = messenger;
        _logger = logger;
        ServerId = serverId;
        _serverName = serverName;
        this.instanceUrl = instanceUrl;
        _currentUserId = currentUserId;

        messenger.Register<ChannelMessageReceivedMessage>(this);
        messenger.Register<HubConnectionStateChangedMessage>(this);

        // Initialize hub status from current state
        HubStatus = messageHubService.CurrentStatus;

        // Instantiate sub-VMs in dependency order
        VoiceParticipants = new VoiceParticipantsViewModel(serverId, messenger, ChannelGroups);
        VoiceCall = new VoiceCallViewModel(serverId, instanceUrl, currentUserId, currentUsername,
            voiceCallService, messenger, loggerFactory.CreateLogger<VoiceCallViewModel>(), VoiceParticipants);
        ScreenShareBroadcast = new ScreenShareBroadcastViewModel(serverId, currentUserId,
            voiceCallService, settingsStore, dialogManager, messenger,
            loggerFactory.CreateLogger<ScreenShareBroadcastViewModel>());
        ScreenShareWatcher = new ScreenShareWatcherViewModel(serverId, currentUserId,
            voiceCallService, messenger, loggerFactory.CreateLogger<ScreenShareWatcherViewModel>(),
            VoiceParticipants.FindChannel);
        Presence = new ServerPresenceViewModel(serverId, messenger, toastManager);
    }

    // --- Sub-ViewModels ---

    public VoiceParticipantsViewModel VoiceParticipants { get; }
    public VoiceCallViewModel VoiceCall { get; }
    public ScreenShareBroadcastViewModel ScreenShareBroadcast { get; }
    public ScreenShareWatcherViewModel ScreenShareWatcher { get; }
    public ServerPresenceViewModel Presence { get; }

    // Forwarded for code-behind autocomplete
    public List<string> ServerMembers => Presence.ServerMembers;

    [ObservableProperty]
    private string _serverName;

    [ObservableProperty]
    private ChannelItem? _selectedChannel;

    [ObservableProperty]
    private string _messageText = "";

    public int MessageCharsRemaining => MessageLengthPolicy.CharsRemaining(MessageText);
    public bool ShowCharCount => MessageLengthPolicy.ShouldShowCounter(MessageText);
    public bool IsOverLimit => MessageLengthPolicy.IsOverLimit(MessageText);

    partial void OnMessageTextChanged(string value)
    {
        OnPropertyChanged(nameof(MessageCharsRemaining));
        OnPropertyChanged(nameof(ShowCharCount));
        OnPropertyChanged(nameof(IsOverLimit));
    }

    public Guid ServerId { get; }

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

        var content = ShortcodeReplacer.Replace(MessageText.Trim());
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
        optimistic.SendState = new PendingState();

        Messages.Add(optimistic);
        _allMessages.Add(optimistic);

        try
        {
            var response = await client.Server.SendMessageAsync(instanceUrl, ServerId, SelectedChannel.Id, new SendMessageRequestDto
            {
                Content = content,
            });

            var alreadyDelivered = Messages.FirstOrDefault(m => m.MessageId == response.MessageId);
            if (alreadyDelivered is not null)
            {
                Messages.Remove(optimistic);
                _allMessages.Remove(optimistic);
            }
            else
            {
                optimistic.MessageId = response.MessageId;
                optimistic.SendState = new DeliveredState();
            }
        }
        catch
        {
            optimistic.SendState = new FailedState("Send failed");
            MessageText = content;
        }
    }

    private readonly string _currentUsername;

    private MessageItem BuildMessageItem(Guid messageId, string content, Guid authorId, string authorName, string? authorInstanceUrl, string createdAt)
    {
        var message = Message.Create(messageId, authorId, authorInstanceUrl, content, createdAt);

        var lastMessage = Messages.LastOrDefault();
        var lastAuthorId = lastMessage?.AuthorId;
        Instant? lastTimestamp = null;
        if (lastMessage is not null)
        {
            var lastParsed = InstantPattern.ExtendedIso.Parse(lastMessage.CreatedAt);
            if (lastParsed.Success) lastTimestamp = lastParsed.Value;
        }

        var zone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        var showAuthor = MessageGrouper.ShouldShowAuthor(lastTimestamp, lastAuthorId, message.Timestamp, message.AuthorId, zone);
        var showTimeSeparator = MessageGrouper.ShouldShowTimeSeparator(lastTimestamp, message.Timestamp, zone);

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
            ShowTimeSeparator = showTimeSeparator,
            TimeSeparatorText = showTimeSeparator ? FormatTimeSeparator(createdAt) : "",
        };
    }

    public void Receive(ChannelMessageReceivedMessage message)
    {
        if (message.ServerId != ServerId || SelectedChannel is null || message.ChannelId != SelectedChannel.Id)
            return;

        var dto = message.Message;

        Dispatcher.UIThread.Post(() =>
        {
            if (Messages.Any(m => m.MessageId == dto.MessageId))
                return;

            var item = BuildMessageItem(dto.MessageId, dto.Content, dto.AuthorId, dto.AuthorName, dto.AuthorInstanceUrl, dto.CreatedAt);

            Messages.Add(item);
            _allMessages.Add(item);
        });
    }

    // --- Hub connection status ---

    [ObservableProperty]
    private HubConnectionStatus _hubStatus = HubConnectionStatus.Disconnected;

    [ObservableProperty]
    private string? _subscribedChannelName;

    public void Receive(HubConnectionStateChangedMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HubStatus = message.Status;
        });
    }

    // --- Messages ---

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
            foreach (var msg in response.Messages)
            {
                var message = Message.Create(msg.MessageId, msg.AuthorId, msg.AuthorInstanceUrl, msg.Content, msg.CreatedAt);

                var showAuthor = MessageGrouper.ShouldShowAuthor(lastTimestamp, lastAuthorId, message.Timestamp, message.AuthorId, zone);
                var showTimeSeparator = MessageGrouper.ShouldShowTimeSeparator(lastTimestamp, message.Timestamp, zone);
                lastAuthorId = message.AuthorId;
                lastTimestamp = message.Timestamp;

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
                    ShowTimeSeparator = showTimeSeparator,
                    TimeSeparatorText = showTimeSeparator ? FormatTimeSeparator(msg.CreatedAt) : "",
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
            Presence.SetMembers(membersResponse.Members);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server members");
        }

        try
        {
            await _messageHubService.SubscribeToServerAsync(ServerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe to server group");
        }

        try
        {
            var presence = await _messageHubService.GetServerPresenceAsync(ServerId);
            Presence.SetPresence(presence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server presence");
            Presence.SetPresence([]);
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

            try
            {
                var voiceState = await _messageHubService.GetServerVoiceStateAsync(ServerId, instanceUrl);
                var screenSharerIds = ScreenShareWatcher.ActiveScreenShares.Select(s => s.UserId).ToHashSet();
                foreach (var (channelId, participants) in voiceState)
                {
                    var channel = VoiceParticipants.FindChannel(channelId);
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

            VoiceCall.RestoreVoiceChannelName();

            if (SelectedChannel is null)
            {
                var firstChannel = ChannelGroups.FirstOrDefault()?.Channels.FirstOrDefault();
                if (firstChannel is not null)
                    await SelectChannel(firstChannel);
            }
        }
    }
}
