using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;

namespace Fennec.App.ViewModels;

public partial class VoiceBarViewModel : ObservableObject,
    IRecipient<VoiceStateChangedMessage>,
    IRecipient<VoiceMuteToggledMessage>,
    IRecipient<VoiceDeafenToggledMessage>
{
    private readonly IVoiceCallService _voiceCallService;
    private readonly IMessenger _messenger;
    private readonly IVoiceChannelNavigator _navigator;
    private readonly ObservableCollection<SidebarServer> _servers;
    private readonly FloatingScreenShareViewModel _floatingScreenShare;

    public Guid? VoiceServerId { get; private set; }

    public VoiceBarViewModel(
        IVoiceCallService voiceCallService,
        IMessenger messenger,
        IVoiceChannelNavigator navigator,
        ObservableCollection<SidebarServer> servers,
        FloatingScreenShareViewModel floatingScreenShare)
    {
        _voiceCallService = voiceCallService;
        _messenger = messenger;
        _navigator = navigator;
        _servers = servers;
        _floatingScreenShare = floatingScreenShare;

        messenger.Register<VoiceStateChangedMessage>(this);
        messenger.Register<VoiceMuteToggledMessage>(this);
        messenger.Register<VoiceDeafenToggledMessage>(this);

        IsInVoiceCall = voiceCallService.IsConnected;
        IsVoiceMuted = voiceCallService.IsMuted;
        IsVoiceDeafened = voiceCallService.IsDeafened;
    }

    [ObservableProperty]
    private bool _isInVoiceCall;

    [ObservableProperty]
    private string? _voiceChannelName;

    [ObservableProperty]
    private string? _voiceServerName;

    [ObservableProperty]
    private bool _isVoiceMuted;

    [ObservableProperty]
    private bool _isVoiceDeafened;

    public void Receive(VoiceStateChangedMessage message)
    {
        IsInVoiceCall = message.IsConnected;
        VoiceServerId = message.ServerId;

        if (message.IsConnected && message.ServerId is not null)
        {
            var server = _servers.FirstOrDefault(s => s.Id == message.ServerId);
            VoiceServerName = server?.Name;
            VoiceChannelName = null;
        }
        else
        {
            VoiceServerName = null;
            VoiceChannelName = null;
            IsVoiceMuted = false;
            IsVoiceDeafened = false;

            _floatingScreenShare.ResetOnCallEnd();
        }
    }

    public void Receive(VoiceMuteToggledMessage message)
    {
        IsVoiceMuted = message.IsMuted;
    }

    public void Receive(VoiceDeafenToggledMessage message)
    {
        IsVoiceDeafened = message.IsDeafened;
        if (message.IsDeafened)
            IsVoiceMuted = true;
    }

    [RelayCommand]
    private void ToggleVoiceMute()
    {
        if (!_voiceCallService.IsConnected) return;
        var newMuted = !_voiceCallService.IsMuted;
        _voiceCallService.SetMuted(newMuted);
        _messenger.Send(new VoiceMuteToggledMessage(newMuted));
    }

    [RelayCommand]
    private void ToggleVoiceDeafen()
    {
        if (!_voiceCallService.IsConnected) return;
        var newDeafened = !_voiceCallService.IsDeafened;
        _voiceCallService.SetDeafened(newDeafened);
        _messenger.Send(new VoiceDeafenToggledMessage(newDeafened));
    }

    [RelayCommand]
    private async Task LeaveVoiceCallAsync()
    {
        await _voiceCallService.LeaveAsync();
    }

    [RelayCommand]
    private async Task NavigateToVoiceChannelAsync()
    {
        await _navigator.NavigateToVoiceChannelAsync();
    }
}
