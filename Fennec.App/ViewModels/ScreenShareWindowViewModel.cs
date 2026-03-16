using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;

namespace Fennec.App.ViewModels;

public partial class ScreenShareWindowViewModel : ObservableObject,
    IRecipient<ScreenShareFrameMessage>,
    IRecipient<ScreenShareCursorMessage>,
    IRecipient<ScreenShareStoppedMessage>,
    IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IVoiceCallService _voiceCallService;
    private readonly Guid _voiceServerId;
    private int _queueDepth;

    public Guid UserId { get; }
    public string Username { get; }

    [ObservableProperty]
    private WriteableBitmap? _screenShareFrame;

    [ObservableProperty]
    private double _cursorX;

    [ObservableProperty]
    private double _cursorY;

    [ObservableProperty]
    private CursorType _cursorShape;

    [ObservableProperty]
    private bool _shouldClose;

    [ObservableProperty]
    private bool _showDebugOverlay;

    public ScreenShareMetrics? DebugMetrics => _voiceCallService.GetMetrics(UserId);

    public ScreenShareWindowViewModel(IMessenger messenger, IVoiceCallService voiceCallService, Guid userId, string username, Guid voiceServerId)
    {
        _messenger = messenger;
        _voiceCallService = voiceCallService;
        UserId = userId;
        Username = username;
        _voiceServerId = voiceServerId;

        messenger.Register<ScreenShareFrameMessage>(this);
        messenger.Register<ScreenShareCursorMessage>(this);
        messenger.Register<ScreenShareStoppedMessage>(this);
    }

    public void Receive(ScreenShareFrameMessage message)
    {
        if (message.UserId != UserId) return;

        var metrics = DebugMetrics;
        Interlocked.Increment(ref _queueDepth);
        metrics?.QueueDepth.Add(Interlocked.CompareExchange(ref _queueDepth, 0, 0));

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Decrement(ref _queueDepth);
            try
            {
                // Measure frame lag
                if (metrics != null && message.Timestamp != 0)
                    metrics.FrameLagMs.Add(Stopwatch.GetElapsedTime(message.Timestamp).TotalMilliseconds);

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

                var copySw = Stopwatch.StartNew();
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
                copySw.Stop();
                metrics?.BitmapCopyTimeMs.Add(copySw.Elapsed.TotalMilliseconds);

                OnPropertyChanged(nameof(ScreenShareFrame));
            }
            catch
            {
                // Ignore render failures
            }
        });
    }

    public void Receive(ScreenShareCursorMessage message)
    {
        if (message.UserId != UserId) return;

        Dispatcher.UIThread.Post(() =>
        {
            CursorX = message.X;
            CursorY = message.Y;
            CursorShape = message.Type;
        });
    }

    public void Receive(ScreenShareStoppedMessage message)
    {
        if (message.UserId != UserId) return;

        Dispatcher.UIThread.Post(() => ShouldClose = true);
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
    }
}
