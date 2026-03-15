using System.Runtime.InteropServices;
using Fennec.App.Messages;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Linux cursor position service using X11 XQueryPointer.
/// Polls at ~60Hz and normalizes coordinates relative to screen bounds.
/// </summary>
public class LinuxCursorPositionService : ICursorPositionService
{
    private readonly ILogger<LinuxCursorPositionService> _logger;
    private Timer? _timer;
    private int _screenWidth;
    private int _screenHeight;
    private float _lastX = -1;
    private float _lastY = -1;

    public event Action<float, float, CursorType>? OnCursorChanged;

    public LinuxCursorPositionService(ILogger<LinuxCursorPositionService> logger)
    {
        _logger = logger;
    }

    public void Start(CaptureTarget target)
    {
        Stop();

        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            _logger.LogWarning("CursorPosition: Failed to open X11 display");
            return;
        }

        var screen = XDefaultScreen(display);
        _screenWidth = XDisplayWidth(display, screen);
        _screenHeight = XDisplayHeight(display, screen);
        XCloseDisplay(display);

        // Poll at ~60Hz (16ms interval)
        _timer = new Timer(PollCursor, null, 0, 16);
        _logger.LogInformation("CursorPosition: Started polling at 60Hz, screen {W}x{H}", _screenWidth, _screenHeight);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _lastX = -1;
        _lastY = -1;
    }

    private void PollCursor(object? state)
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return;

            try
            {
                var screen = XDefaultScreen(display);
                var root = XRootWindow(display, screen);

                XQueryPointer(display, root,
                    out _, out _,
                    out var rootX, out var rootY,
                    out _, out _,
                    out _);

                var normalizedX = _screenWidth > 0 ? (float)rootX / _screenWidth : 0f;
                var normalizedY = _screenHeight > 0 ? (float)rootY / _screenHeight : 0f;

                normalizedX = Math.Clamp(normalizedX, 0f, 1f);
                normalizedY = Math.Clamp(normalizedY, 0f, 1f);

                // Deduplicate — only fire if position changed
                if (Math.Abs(normalizedX - _lastX) > 0.0001f || Math.Abs(normalizedY - _lastY) > 0.0001f)
                {
                    _lastX = normalizedX;
                    _lastY = normalizedY;
                    // For now, always report Arrow — X11 cursor shape detection
                    // via XFixesGetCursorImage is complex and deferred.
                    OnCursorChanged?.Invoke(normalizedX, normalizedY, CursorType.Arrow);
                }
            }
            finally
            {
                XCloseDisplay(display);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CursorPosition: Poll error");
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XDefaultScreen(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern ulong XRootWindow(IntPtr display, int screen);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayWidth(IntPtr display, int screen);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayHeight(IntPtr display, int screen);

    [DllImport("libX11.so.6")]
    private static extern bool XQueryPointer(IntPtr display, ulong window,
        out ulong rootReturn, out ulong childReturn,
        out int rootXReturn, out int rootYReturn,
        out int winXReturn, out int winYReturn,
        out uint maskReturn);
}
