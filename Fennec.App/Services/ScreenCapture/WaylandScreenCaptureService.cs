using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// IScreenCaptureService for Wayland: portal negotiation via Python3/dbus-python,
/// frame capture via GStreamer pipewiresrc → appsink (piped as binary to .NET).
/// </summary>
public class WaylandScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<WaylandScreenCaptureService> _logger;
    private readonly ILogger<WaylandPortalClient> _portalLogger;
    private readonly ILogger<WaylandPipeWireCapture> _captureLogger;

    private WaylandPortalClient? _portal;
    private WaylandPipeWireCapture? _capture;

    public bool IsCapturing { get; private set; }

    public WaylandScreenCaptureService(
        ILogger<WaylandScreenCaptureService> logger,
        ILogger<WaylandPortalClient> portalLogger,
        ILogger<WaylandPipeWireCapture> captureLogger)
    {
        _logger = logger;
        _portalLogger = portalLogger;
        _captureLogger = captureLogger;
    }

    public Task<List<CaptureTarget>> GetAvailableTargetsAsync() =>
        Task.FromResult(new List<CaptureTarget>
        {
            new CaptureTarget(CaptureTargetKind.Screen, "wayland:portal", "Screen (system picker)"),
        });

    public async Task StartAsync(CaptureTarget target, Action<byte[], int, int> onFrame)
    {
        if (IsCapturing) return;

        _portal = new WaylandPortalClient(_portalLogger);
        _capture = new WaylandPipeWireCapture(_captureLogger);

        try
        {
            var nodeId = await _portal.StartSessionAsync();
            await _capture.StartAsync(nodeId, onFrame, _portal.Process!);
            IsCapturing = true;
            _logger.LogInformation("Wayland screen capture started (node {NodeId})", nodeId);
        }
        catch
        {
            _capture.Dispose();
            await _portal.DisposeAsync();
            _capture = null;
            _portal = null;
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsCapturing) return;
        IsCapturing = false;

        if (_capture != null) { await _capture.StopAsync(); _capture.Dispose(); _capture = null; }
        if (_portal != null) { await _portal.DisposeAsync(); _portal = null; }

        _logger.LogInformation("Wayland screen capture stopped");
    }
}
