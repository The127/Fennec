using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain.Events;
using Fennec.App.Messages;
using Fennec.App.Services;
using Microsoft.Extensions.Logging;

namespace Fennec.App.ViewModels;

public partial class ScreenShareWatcherViewModel : ObservableObject,
    IRecipient<ScreenShareStartedMessage>,
    IRecipient<ScreenShareStoppedMessage>,
    IRecipient<ScreenShareFrameMessage>,
    IRecipient<ScreenShareCursorMessage>,
    IRecipient<ScreenSharePopOutRequestedMessage>,
    IRecipient<ScreenSharePopOutClosedMessage>,
    IRecipient<ControlWatchScreenShareMessage>,
    IRecipient<ControlUnwatchScreenShareMessage>,
    IRecipient<VoiceStateChangedMessage>
{
    private readonly Guid _serverId;
    private readonly Guid _currentUserId;
    private readonly IVoiceCallService _voiceCallService;
    private readonly IMessenger _messenger;
    private readonly ILogger<ScreenShareWatcherViewModel> _logger;
    private readonly Func<Guid, ChannelItem?> _findChannel;

    private readonly HashSet<Guid> _poppedOutUserIds = [];
    private int _queueDepth;

    public ScreenShareWatcherViewModel(
        Guid serverId,
        Guid currentUserId,
        IVoiceCallService voiceCallService,
        IMessenger messenger,
        ILogger<ScreenShareWatcherViewModel> logger,
        Func<Guid, ChannelItem?> findChannel)
    {
        _serverId = serverId;
        _currentUserId = currentUserId;
        _voiceCallService = voiceCallService;
        _messenger = messenger;
        _logger = logger;
        _findChannel = findChannel;

        messenger.Register<ScreenShareStartedMessage>(this);
        messenger.Register<ScreenShareStoppedMessage>(this);
        messenger.Register<ScreenShareFrameMessage>(this);
        messenger.Register<ScreenShareCursorMessage>(this);
        messenger.Register<ScreenSharePopOutRequestedMessage>(this);
        messenger.Register<ScreenSharePopOutClosedMessage>(this);
        messenger.Register<ControlWatchScreenShareMessage>(this);
        messenger.Register<ControlUnwatchScreenShareMessage>(this);
        messenger.Register<VoiceStateChangedMessage>(this);

        // Restore active screen sharers if already in a call
        if (voiceCallService.IsConnected && voiceCallService.CurrentServerId == serverId)
        {
            foreach (var sharer in voiceCallService.ActiveScreenSharers)
                ActiveScreenShares.Add(new ScreenShareInfo(sharer.UserId, sharer.Username, sharer.InstanceUrl));
        }
    }

    public ObservableCollection<ScreenShareInfo> ActiveScreenShares { get; } = [];
    public ObservableCollection<ScreenShareInfo> WatchedScreenShares { get; } = [];

    [ObservableProperty]
    private bool _hasWatchedShares;

    [ObservableProperty]
    private Guid? _focusedScreenShareUserId;

    [ObservableProperty]
    private bool _showOwnScreenShare;

    [ObservableProperty]
    private bool _showDebugOverlay;

    public ScreenShareMetrics? DebugMetrics => FocusedScreenShareUserId is { } uid ? _voiceCallService.GetMetrics(uid) : null;

    partial void OnFocusedScreenShareUserIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(DebugMetrics));
    }

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

    [ObservableProperty]
    private bool _isScreenShareMaximized;

    private Guid? FindNextFocusableShare(Guid? excludeUserId)
    {
        return WatchedScreenShares
            .Where(s => s.UserId != excludeUserId)
            .Where(s => !_poppedOutUserIds.Contains(s.UserId))
            .Where(s => s.UserId != _currentUserId || ShowOwnScreenShare)
            .FirstOrDefault()?.UserId;
    }

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
            if (FocusedScreenShareUserId is null && WatchedScreenShares.Any(s => s.UserId == _currentUserId))
                FocusedScreenShareUserId = _currentUserId;
        }
        else
        {
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

        if (share is null)
        {
            var voiceSharer = _voiceCallService.ActiveScreenSharers.FirstOrDefault(s => s.UserId == userId);
            if (voiceSharer is not null)
            {
                share = new ScreenShareInfo(voiceSharer.UserId, voiceSharer.Username, voiceSharer.InstanceUrl);
                ActiveScreenShares.Add(share);
            }
        }

        if (share is null) return;

        WatchedScreenShares.Add(share);
        HasWatchedShares = true;
        FocusedScreenShareUserId = userId;

        _ = _voiceCallService.WatchScreenShareAsync(userId);
    }

    [RelayCommand]
    private void UnwatchScreenShare(Guid userId)
    {
        var share = WatchedScreenShares.FirstOrDefault(s => s.UserId == userId);
        if (share is null) return;

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

    [RelayCommand]
    private void FocusScreenShare(Guid userId)
    {
        FocusedScreenShareUserId = userId;
    }

    public void Receive(ScreenShareStartedMessage message)
    {
        if (message.ServerId != _serverId) return;
        if (message.UserId == _currentUserId) return; // handled by ScreenShareBroadcastViewModel

        Dispatcher.UIThread.Post(() =>
        {
            if (ActiveScreenShares.Any(s => s.UserId == message.UserId))
                return;

            ActiveScreenShares.Add(new ScreenShareInfo(message.UserId, message.Username, message.InstanceUrl));

            var channel = _findChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsScreenSharing = true;
        });
    }

    public void Receive(ScreenShareStoppedMessage message)
    {
        if (message.ServerId != _serverId) return;
        if (message.UserId == _currentUserId) return; // handled by ScreenShareBroadcastViewModel

        Dispatcher.UIThread.Post(() =>
        {
            var share = ActiveScreenShares.FirstOrDefault(s => s.UserId == message.UserId);
            if (share != null)
                ActiveScreenShares.Remove(share);

            var watched = WatchedScreenShares.FirstOrDefault(s => s.UserId == message.UserId);
            if (watched != null)
            {
                WatchedScreenShares.Remove(watched);
                HasWatchedShares = WatchedScreenShares.Count > 0;
            }
            _poppedOutUserIds.Remove(message.UserId);

            if (FocusedScreenShareUserId == message.UserId)
                FocusedScreenShareUserId = FindNextFocusableShare(null);

            if (WatchedScreenShares.Count == 0)
            {
                ScreenShareFrame = null;
                IsScreenShareMaximized = false;
            }

            var channel = _findChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsScreenSharing = false;
        });
    }

    public void Receive(ScreenShareFrameMessage message)
    {
        if (FocusedScreenShareUserId != message.UserId)
            return;

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

        if (FocusedScreenShareUserId is null && WatchedScreenShares.Any(s => s.UserId == message.UserId))
            FocusedScreenShareUserId = message.UserId;
    }

    public void Receive(ControlWatchScreenShareMessage message)
    {
        Dispatcher.UIThread.Post(() => WatchScreenShare(message.UserId));
    }

    public void Receive(ControlUnwatchScreenShareMessage message)
    {
        Dispatcher.UIThread.Post(() => UnwatchScreenShare(message.UserId));
    }

    public void Receive(VoiceStateChangedMessage message)
    {
        if (message.IsConnected) return;

        Dispatcher.UIThread.Post(() =>
        {
            ActiveScreenShares.Clear();
            WatchedScreenShares.Clear();
            HasWatchedShares = false;
            FocusedScreenShareUserId = null;
            IsScreenShareMaximized = false;
            ScreenShareFrame = null;
            _poppedOutUserIds.Clear();
        });
    }
}
