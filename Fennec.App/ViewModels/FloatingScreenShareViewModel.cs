using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain.Events;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services;

namespace Fennec.App.ViewModels;

public partial class FloatingScreenShareViewModel : ObservableObject,
    IRecipient<VoiceStateChangedMessage>,
    IRecipient<ScreenShareStartedMessage>,
    IRecipient<ScreenShareStoppedMessage>,
    IRecipient<ScreenShareFrameMessage>,
    IRecipient<ScreenShareCursorMessage>,
    IRecipient<ScreenSharePopOutRequestedMessage>,
    IRecipient<ScreenSharePopOutClosedMessage>
{
    private readonly IMessenger _messenger;
    private readonly IVoiceCallService _voiceCallService;
    private readonly IVoiceChannelNavigator _navigator;
    private readonly IRouter _router;

    private int _activeScreenShareCount;
    private Guid? _focusedScreenShareUserId;
    private Guid? _voiceServerId;
    private readonly Dictionary<Guid, Views.ScreenShareWindow> _popOutWindows = new();

    public FloatingScreenShareViewModel(
        IMessenger messenger,
        IVoiceCallService voiceCallService,
        IVoiceChannelNavigator navigator,
        IRouter router)
    {
        _messenger = messenger;
        _voiceCallService = voiceCallService;
        _navigator = navigator;
        _router = router;

        messenger.Register<VoiceStateChangedMessage>(this);
        messenger.Register<ScreenShareStartedMessage>(this);
        messenger.Register<ScreenShareStoppedMessage>(this);
        messenger.Register<ScreenShareFrameMessage>(this);
        messenger.Register<ScreenShareCursorMessage>(this);
        messenger.Register<ScreenSharePopOutRequestedMessage>(this);
        messenger.Register<ScreenSharePopOutClosedMessage>(this);
    }

    [ObservableProperty]
    private bool _showFloatingScreenShare;

    [ObservableProperty]
    private WriteableBitmap? _floatingScreenShareFrame;

    [ObservableProperty]
    private double _floatingCursorX;

    [ObservableProperty]
    private double _floatingCursorY;

    [ObservableProperty]
    private CursorType _floatingCursorShape;

    [ObservableProperty]
    private bool _floatingCursorVisible = true;

    [ObservableProperty]
    private string? _floatingSharerUsername;

    [ObservableProperty]
    private bool _isScreenSharePoppedOut;

    public void Receive(VoiceStateChangedMessage message)
    {
        _voiceServerId = message.ServerId;
    }

    public void ResetOnCallEnd()
    {
        _activeScreenShareCount = 0;
        _focusedScreenShareUserId = null;
        FloatingScreenShareFrame = null;
        FloatingSharerUsername = null;
        CloseAllPopOutWindows();
        UpdateFloatingScreenShareVisibility();
    }

    public void OnRouteNavigated()
    {
        UpdateFloatingScreenShareVisibility();
    }

    private void UpdateFloatingScreenShareVisibility()
    {
        var isOnVoiceServer = _router.CurrentViewModel is ServerViewModel svm
                              && svm.ServerId == _voiceServerId;
        ShowFloatingScreenShare = _activeScreenShareCount > 0 && !isOnVoiceServer && !IsScreenSharePoppedOut;
    }

    public void Receive(ScreenShareStartedMessage message)
    {
        if (message.ServerId != _voiceServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _activeScreenShareCount++;
            _focusedScreenShareUserId ??= message.UserId;
            if (_focusedScreenShareUserId == message.UserId)
                FloatingSharerUsername = message.Username;
            UpdateFloatingScreenShareVisibility();
        });
    }

    public void Receive(ScreenShareStoppedMessage message)
    {
        if (message.ServerId != _voiceServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _activeScreenShareCount = Math.Max(0, _activeScreenShareCount - 1);

            if (_focusedScreenShareUserId == message.UserId)
            {
                _focusedScreenShareUserId = null;
                FloatingSharerUsername = null;
                FloatingScreenShareFrame = null;
            }

            if (_activeScreenShareCount == 0)
            {
                FloatingScreenShareFrame = null;
                FloatingSharerUsername = null;
            }

            UpdateFloatingScreenShareVisibility();
        });
    }

    public void Receive(ScreenShareFrameMessage message)
    {
        if (_focusedScreenShareUserId != message.UserId) return;
        if (!ShowFloatingScreenShare) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (FloatingScreenShareFrame is null
                    || FloatingScreenShareFrame.PixelSize.Width != message.Width
                    || FloatingScreenShareFrame.PixelSize.Height != message.Height)
                {
                    FloatingScreenShareFrame = new WriteableBitmap(
                        new Avalonia.PixelSize(message.Width, message.Height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Rgba8888,
                        Avalonia.Platform.AlphaFormat.Unpremul);
                }

                using (var frameBuffer = FloatingScreenShareFrame.Lock())
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

                OnPropertyChanged(nameof(FloatingScreenShareFrame));
            }
            catch
            {
                // Ignore render failures
            }
        });
    }

    public void Receive(ScreenShareCursorMessage message)
    {
        if (_focusedScreenShareUserId != message.UserId) return;
        if (!ShowFloatingScreenShare) return;

        Dispatcher.UIThread.Post(() =>
        {
            FloatingCursorX = message.X;
            FloatingCursorY = message.Y;
            FloatingCursorShape = message.Type;
            FloatingCursorVisible = message.IsVisible;
        });
    }

    public void Receive(ScreenSharePopOutRequestedMessage message)
    {
        Dispatcher.UIThread.Post(() => OpenPopOutWindow(message.UserId, message.Username));
    }

    public void Receive(ScreenSharePopOutClosedMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _popOutWindows.Remove(message.UserId);
            if (_popOutWindows.Count == 0)
                IsScreenSharePoppedOut = false;
            UpdateFloatingScreenShareVisibility();
        });
    }

    [RelayCommand]
    private void PopOutFloatingScreenShare()
    {
        if (_focusedScreenShareUserId is null || FloatingSharerUsername is null) return;
        OpenPopOutWindow(_focusedScreenShareUserId.Value, FloatingSharerUsername);
    }

    private void OpenPopOutWindow(Guid userId, string username)
    {
        if (_popOutWindows.ContainsKey(userId))
        {
            _popOutWindows[userId].Activate();
            return;
        }

        if (_voiceServerId is null) return;

        var vm = new ScreenShareWindowViewModel(_messenger, _voiceCallService, userId, username, _voiceServerId.Value);
        var window = new Views.ScreenShareWindow { DataContext = vm };
        _popOutWindows[userId] = window;
        IsScreenSharePoppedOut = true;
        UpdateFloatingScreenShareVisibility();
        window.Show();
    }

    private void CloseAllPopOutWindows()
    {
        foreach (var window in _popOutWindows.Values.ToList())
            window.Close();
        _popOutWindows.Clear();
        IsScreenSharePoppedOut = false;
    }

    [RelayCommand]
    private async Task NavigateToFloatingScreenShareAsync()
    {
        await _navigator.NavigateToVoiceChannelAsync();
    }
}
