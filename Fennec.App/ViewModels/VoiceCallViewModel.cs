using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain.Events;
using Fennec.App.Messages;
using Fennec.App.Services;
using Microsoft.Extensions.Logging;

namespace Fennec.App.ViewModels;

public partial class VoiceCallViewModel : ObservableObject,
    IRecipient<VoiceStateChangedMessage>,
    IRecipient<VoiceMuteToggledMessage>,
    IRecipient<VoiceDeafenToggledMessage>
{
    private readonly Guid _serverId;
    private readonly string _instanceUrl;
    private readonly Guid _currentUserId;
    private readonly string _currentUsername;
    private readonly IVoiceCallService _voiceCallService;
    private readonly ILogger<VoiceCallViewModel> _logger;
    private readonly VoiceParticipantsViewModel _voiceParticipants;

    public VoiceCallViewModel(
        Guid serverId,
        string instanceUrl,
        Guid currentUserId,
        string currentUsername,
        IVoiceCallService voiceCallService,
        IMessenger messenger,
        ILogger<VoiceCallViewModel> logger,
        VoiceParticipantsViewModel voiceParticipants)
    {
        _serverId = serverId;
        _instanceUrl = instanceUrl;
        _currentUserId = currentUserId;
        _currentUsername = currentUsername;
        _voiceCallService = voiceCallService;
        _logger = logger;
        _voiceParticipants = voiceParticipants;

        messenger.Register<VoiceStateChangedMessage>(this);
        messenger.Register<VoiceMuteToggledMessage>(this);
        messenger.Register<VoiceDeafenToggledMessage>(this);

        // Restore voice state if already in a call on this server
        if (voiceCallService.IsConnected && voiceCallService.CurrentServerId == serverId)
        {
            IsInVoiceChannel = true;
            CurrentVoiceChannelId = voiceCallService.CurrentChannelId;
            IsMuted = voiceCallService.IsMuted;
            IsDeafened = voiceCallService.IsDeafened;
        }
    }

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

        if (IsInVoiceChannel && CurrentVoiceChannelId == channel.Id) return;

        try
        {
            await _voiceCallService.JoinAsync(_serverId, channel.Id, _instanceUrl, _currentUserId, _currentUsername);
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
        _voiceParticipants.UpdateLocalParticipantMuteState(IsMuted, IsDeafened, _currentUserId, CurrentVoiceChannelId);
    }

    [RelayCommand]
    private void ToggleDeafen()
    {
        IsDeafened = !IsDeafened;
        _voiceCallService.SetDeafened(IsDeafened);
        if (IsDeafened)
            IsMuted = true;
        _voiceParticipants.UpdateLocalParticipantMuteState(IsMuted, IsDeafened, _currentUserId, CurrentVoiceChannelId);
    }

    public void Receive(VoiceStateChangedMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsInVoiceChannel = message.IsConnected;
            CurrentVoiceChannelId = message.ChannelId;

            if (message.IsConnected && message.ChannelId is not null)
            {
                var channel = _voiceParticipants.FindChannel(message.ChannelId.Value);
                CurrentVoiceChannelName = channel?.Name;
            }
            else
            {
                CurrentVoiceChannelName = null;
                IsMuted = false;
                IsDeafened = false;
                _voiceParticipants.ResetSpeakingIndicators();
            }
        });
    }

    public void Receive(VoiceMuteToggledMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsMuted = message.IsMuted;
            _voiceParticipants.UpdateLocalParticipantMuteState(IsMuted, IsDeafened, _currentUserId, CurrentVoiceChannelId);
        });
    }

    public void Receive(VoiceDeafenToggledMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsDeafened = message.IsDeafened;
            if (message.IsDeafened)
                IsMuted = true;
            _voiceParticipants.UpdateLocalParticipantMuteState(IsMuted, IsDeafened, _currentUserId, CurrentVoiceChannelId);
        });
    }

    /// <summary>Called by ServerViewModel.LoadAsync after channels are loaded.</summary>
    public void RestoreVoiceChannelName()
    {
        if (IsInVoiceChannel && CurrentVoiceChannelId is not null && CurrentVoiceChannelName is null)
        {
            var voiceChannel = _voiceParticipants.FindChannel(CurrentVoiceChannelId.Value);
            CurrentVoiceChannelName = voiceChannel?.Name;
        }
    }
}
