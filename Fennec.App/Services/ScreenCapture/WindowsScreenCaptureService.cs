using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

public class WindowsScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<WindowsScreenCaptureService> _logger;
    private IntPtr _capture;
    private bool _isCapturing;

    // Pin delegates to prevent GC during native callbacks
    private NativeVideoInterop.NalCallback? _nalCallback;
    private NativeVideoInterop.FrameCallback? _previewCallback;

    public bool IsCapturing => _isCapturing;

    // Events for the fused capture mode — delivers H.264 NAL units directly
    public event Action<byte[], long, bool>? OnNalUnit;
    // Preview callback for local display
    private Action<byte[], int, int>? _onFrame;

    public WindowsScreenCaptureService(ILogger<WindowsScreenCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<List<CaptureTarget>> GetAvailableTargetsAsync()
    {
        var targets = new List<CaptureTarget>();

        IntPtr nativeTargets;
        int count = NativeVideoInterop.fennec_capture_list_targets(out nativeTargets);

        if (count > 0 && nativeTargets != IntPtr.Zero)
        {
            int structSize = Marshal.SizeOf<NativeVideoInterop.FennecCaptureTarget>();
            for (int i = 0; i < count; i++)
            {
                var ptr = nativeTargets + i * structSize;
                var native = Marshal.PtrToStructure<NativeVideoInterop.FennecCaptureTarget>(ptr);

                var id = Marshal.PtrToStringUTF8(native.Id) ?? "";
                var name = Marshal.PtrToStringUTF8(native.Name) ?? "Unknown";
                var kind = native.IsWindow != 0 ? CaptureTargetKind.Window : CaptureTargetKind.Screen;

                targets.Add(new CaptureTarget(kind, id, name, native.Width, native.Height));
            }

            NativeVideoInterop.fennec_capture_free_targets(nativeTargets, count);
        }

        _logger.LogInformation("WindowsScreenCapture: Found {Count} capture targets", targets.Count);
        return Task.FromResult(targets);
    }

    public Task StartAsync(CaptureTarget target, Action<byte[], int, int> onFrame)
    {
        if (_isCapturing)
            return Task.CompletedTask;

        _onFrame = onFrame;

        _nalCallback = OnNativeNalUnit;
        _previewCallback = OnNativePreviewFrame;

        // Create with default settings — ScreenShareVideoSource will configure these
        _capture = NativeVideoInterop.fennec_capture_create(
            target.Id, 1920, 1080, 1500, 30,
            _nalCallback, _previewCallback, IntPtr.Zero);

        if (_capture == IntPtr.Zero)
        {
            _logger.LogError("WindowsScreenCapture: Failed to create native capture");
            return Task.CompletedTask;
        }

        var status = NativeVideoInterop.fennec_capture_start(_capture);
        if (status != 0)
        {
            _logger.LogError("WindowsScreenCapture: Failed to start capture, status={Status}", status);
            NativeVideoInterop.fennec_capture_destroy(_capture);
            _capture = IntPtr.Zero;
            return Task.CompletedTask;
        }

        _isCapturing = true;
        _logger.LogInformation("WindowsScreenCapture: Started capturing target {Id}", target.Id);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_isCapturing)
            return Task.CompletedTask;

        if (_capture != IntPtr.Zero)
        {
            NativeVideoInterop.fennec_capture_stop(_capture);
            NativeVideoInterop.fennec_capture_destroy(_capture);
            _capture = IntPtr.Zero;
        }

        _isCapturing = false;
        _onFrame = null;
        _nalCallback = null;
        _previewCallback = null;

        _logger.LogInformation("WindowsScreenCapture: Stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a fused capture+encode session with specific video parameters.
    /// Used by ScreenShareVideoSource for zero-copy H.264 output.
    /// </summary>
    public Task StartFusedAsync(CaptureTarget target, int maxW, int maxH, int bitrateKbps, int fps,
        Action<byte[], long, bool> onNal, Action<byte[], int, int> onPreview)
    {
        if (_isCapturing)
            return Task.CompletedTask;

        _onFrame = onPreview;
        OnNalUnit = null;
        OnNalUnit += onNal;

        _nalCallback = OnNativeNalUnit;
        _previewCallback = OnNativePreviewFrame;

        _capture = NativeVideoInterop.fennec_capture_create(
            target.Id, maxW, maxH, bitrateKbps, fps,
            _nalCallback, _previewCallback, IntPtr.Zero);

        if (_capture == IntPtr.Zero)
        {
            _logger.LogError("WindowsScreenCapture: Failed to create fused capture");
            return Task.CompletedTask;
        }

        var status = NativeVideoInterop.fennec_capture_start(_capture);
        if (status != 0)
        {
            _logger.LogError("WindowsScreenCapture: Failed to start fused capture, status={Status}", status);
            NativeVideoInterop.fennec_capture_destroy(_capture);
            _capture = IntPtr.Zero;
            return Task.CompletedTask;
        }

        _isCapturing = true;
        _logger.LogInformation("WindowsScreenCapture: Started fused capture+encode {W}x{H} {Bitrate}Kbps {Fps}fps",
            maxW, maxH, bitrateKbps, fps);
        return Task.CompletedTask;
    }

    private void OnNativeNalUnit(IntPtr nalData, int nalSize, long pts, int isKeyframe, IntPtr userData)
    {
        if (nalSize <= 0) return;
        var nal = new byte[nalSize];
        Marshal.Copy(nalData, nal, 0, nalSize);
        OnNalUnit?.Invoke(nal, pts, isKeyframe != 0);
    }

    private void OnNativePreviewFrame(IntPtr rgbaData, int width, int height, IntPtr userData)
    {
        if (width <= 0 || height <= 0) return;
        var rgba = new byte[width * height * 4];
        Marshal.Copy(rgbaData, rgba, 0, rgba.Length);
        _onFrame?.Invoke(rgba, width, height);
    }
}
