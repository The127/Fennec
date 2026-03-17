using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class ScreenShareBroadcastViewModel : ObservableObject,
    IRecipient<ScreenShareStartedMessage>,
    IRecipient<ScreenShareStoppedMessage>
{
    private readonly Guid _serverId;
    private readonly Guid _currentUserId;
    private readonly IVoiceCallService _voiceCallService;
    private readonly ISettingsStore _settingsStore;
    private readonly DialogManager _dialogManager;
    private readonly ILogger<ScreenShareBroadcastViewModel> _logger;

    public ScreenShareBroadcastViewModel(
        Guid serverId,
        Guid currentUserId,
        IVoiceCallService voiceCallService,
        ISettingsStore settingsStore,
        DialogManager dialogManager,
        IMessenger messenger,
        ILogger<ScreenShareBroadcastViewModel> logger)
    {
        _serverId = serverId;
        _currentUserId = currentUserId;
        _voiceCallService = voiceCallService;
        _settingsStore = settingsStore;
        _dialogManager = dialogManager;
        _logger = logger;

        messenger.Register<ScreenShareStartedMessage>(this);
        messenger.Register<ScreenShareStoppedMessage>(this);

        // Restore screen share state if already sharing
        if (voiceCallService.IsConnected && voiceCallService.CurrentServerId == serverId)
            IsScreenSharing = voiceCallService.IsScreenSharing;
    }

    [ObservableProperty]
    private bool _isScreenSharing;

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
                var settings = await _settingsStore.LoadAsync();
                ShareResolution = settings.ScreenShareResolution;
                ShareBitrateKbps = settings.ScreenShareBitrateKbps;
                ShareFrameRate = settings.ScreenShareFrameRate;
                await _voiceCallService.StartScreenShareWithPickerAsync(
                    ShareResolution, ShareBitrateKbps, ShareFrameRate);
            }
            else
            {
                var targets = await _voiceCallService.GetScreenShareTargetsAsync();
                if (targets.Count == 0)
                {
                    _logger.LogWarning("No screen share targets available");
                    return;
                }

                var settings = await _settingsStore.LoadAsync();
                var vm = new ScreenSharePickerViewModel(_dialogManager, _settingsStore, settings, targets);
                _dialogManager.CreateDialog(vm)
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

    [RelayCommand]
    private async Task UpdateScreenShareSettings()
    {
        try
        {
            await _voiceCallService.UpdateScreenShareSettingsAsync(ShareResolution, ShareBitrateKbps, ShareFrameRate);

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

    public void Receive(ScreenShareStartedMessage message)
    {
        if (message.ServerId != _serverId) return;
        if (message.UserId != _currentUserId) return;

        Dispatcher.UIThread.Post(() => IsScreenSharing = true);
    }

    public void Receive(ScreenShareStoppedMessage message)
    {
        if (message.ServerId != _serverId) return;
        if (message.UserId != _currentUserId) return;

        Dispatcher.UIThread.Post(() => IsScreenSharing = false);
    }
}
