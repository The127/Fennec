using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Linux screen capture using X11 XGetImage.
/// Enumerates screens + visible windows for the picker dialog.
/// </summary>
public class LinuxScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<LinuxScreenCaptureService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public bool IsCapturing { get; private set; }

    public LinuxScreenCaptureService(ILogger<LinuxScreenCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<List<CaptureTarget>> GetAvailableTargetsAsync()
    {
        var targets = new List<CaptureTarget>();

        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                _logger.LogWarning("ScreenCapture: Failed to open X11 display for enumeration");
                return Task.FromResult(targets);
            }

            try
            {
                var screen = XDefaultScreen(display);
                var root = XRootWindow(display, screen);
                var screenWidth = XDisplayWidth(display, screen);
                var screenHeight = XDisplayHeight(display, screen);

                // Add full screen option
                targets.Add(new CaptureTarget(CaptureTargetKind.Screen, "x11:root",
                    $"Entire Screen ({screenWidth}x{screenHeight})"));

                // Enumerate top-level windows
                EnumerateWindows(display, root, targets);
            }
            finally
            {
                XCloseDisplay(display);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ScreenCapture: Failed to enumerate capture targets");
        }

        return Task.FromResult(targets);
    }

    private void EnumerateWindows(IntPtr display, ulong root, List<CaptureTarget> targets)
    {
        if (XQueryTree(display, root, out _, out _, out var children, out var nChildren) == 0)
            return;

        if (children == IntPtr.Zero || nChildren == 0)
            return;

        try
        {
            var windowIds = new ulong[nChildren];
            for (int i = 0; i < nChildren; i++)
                windowIds[i] = (ulong)Marshal.ReadInt64(children + i * 8);

            foreach (var windowId in windowIds)
            {
                // Check if window is viewable
                if (XGetWindowAttributes(display, windowId, out var attrs) == 0)
                    continue;

                // map_state: 0=IsUnmapped, 1=IsUnviewable, 2=IsViewable
                if (attrs.map_state != 2)
                    continue;

                // Skip tiny windows (toolbars, panels, etc.)
                if (attrs.width < 100 || attrs.height < 100)
                    continue;

                // Get window name
                var name = GetWindowName(display, windowId);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                targets.Add(new CaptureTarget(CaptureTargetKind.Window,
                    $"x11:0x{windowId:x}",
                    $"{name} ({attrs.width}x{attrs.height})"));
            }
        }
        finally
        {
            XFree(children);
        }
    }

    private static string? GetWindowName(IntPtr display, ulong window)
    {
        // Try _NET_WM_NAME first (UTF-8)
        var utf8String = XInternAtom(display, "UTF8_STRING", false);
        var netWmName = XInternAtom(display, "_NET_WM_NAME", false);

        if (XGetWindowProperty(display, window, netWmName, 0, 1024, false, utf8String,
                out _, out _, out var nItems, out _, out var prop) == 0 && prop != IntPtr.Zero && nItems > 0)
        {
            var name = Marshal.PtrToStringUTF8(prop);
            XFree(prop);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        // Fall back to XFetchName (Latin-1)
        if (XFetchName(display, window, out var namePtr) != 0 && namePtr != IntPtr.Zero)
        {
            var name = Marshal.PtrToStringAnsi(namePtr);
            XFree(namePtr);
            return name;
        }

        return null;
    }

    public async Task StartAsync(CaptureTarget target, Action<byte[], int, int> onFrame)
    {
        if (IsCapturing)
            return;

        _cts = new CancellationTokenSource();
        IsCapturing = true;

        if (target.Kind == CaptureTargetKind.Window && target.Id.StartsWith("x11:0x"))
        {
            var windowId = Convert.ToUInt64(target.Id["x11:".Length..], 16);
            _captureTask = CaptureWindowAsync(windowId, onFrame, _cts.Token);
        }
        else
        {
            _captureTask = CaptureX11Async(onFrame, _cts.Token);
        }

        _logger.LogInformation("ScreenCapture: Started capturing {Target}", target.Name);
    }

    public async Task StopAsync()
    {
        if (!IsCapturing)
            return;

        _cts?.Cancel();
        if (_captureTask != null)
        {
            try { await _captureTask; }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _cts = null;
        _captureTask = null;
        IsCapturing = false;

        _logger.LogInformation("ScreenCapture: Stopped");
    }

    private Task CaptureX11Async(Action<byte[], int, int> onFrame, CancellationToken ct)
    {
        return Task.Factory.StartNew(() =>
        {
            try
            {
                var display = XOpenDisplay(null);
                if (display == IntPtr.Zero)
                {
                    _logger.LogError("ScreenCapture: Failed to open X11 display");
                    return;
                }

                try
                {
                    var screen = XDefaultScreen(display);
                    var root = XRootWindow(display, screen);
                    var width = XDisplayWidth(display, screen);
                    var height = XDisplayHeight(display, screen);

                    _logger.LogInformation("ScreenCapture: X11 display {Width}x{Height}", width, height);

                    RunCaptureLoop(display, root, () => (0, 0, width, height), onFrame, ct);
                }
                finally
                {
                    XCloseDisplay(display);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScreenCapture: X11 capture error");
                IsCapturing = false;
            }
        }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private Task CaptureWindowAsync(ulong windowId, Action<byte[], int, int> onFrame, CancellationToken ct)
    {
        return Task.Factory.StartNew(() =>
        {
            try
            {
                var display = XOpenDisplay(null);
                if (display == IntPtr.Zero)
                {
                    _logger.LogError("ScreenCapture: Failed to open X11 display");
                    return;
                }

                try
                {
                    var oldHandler = XSetErrorHandler(NoOpErrorHandler);
                    var screen = XDefaultScreen(display);
                    var root = XRootWindow(display, screen);
                    var screenW = XDisplayWidth(display, screen);
                    var screenH = XDisplayHeight(display, screen);

                    RunCaptureLoop(display, root, () =>
                    {
                        if (XGetWindowAttributes(display, windowId, out var attrs) == 0)
                        {
                            _logger.LogWarning("ScreenCapture: Window 0x{Id:x} no longer exists", windowId);
                            return (-1, -1, -1, -1); // signal to stop
                        }

                        if (attrs.map_state != 2)
                            return (0, 0, 0, 0); // signal to skip frame

                        XTranslateCoordinates(display, windowId, root, 0, 0, out var rootX, out var rootY, out _);
                        var x = Math.Max(0, rootX);
                        var y = Math.Max(0, rootY);
                        var w = Math.Min(attrs.width, screenW - x);
                        var h = Math.Min(attrs.height, screenH - y);
                        return (x, y, w, h);
                    }, onFrame, ct);

                    XSetErrorHandler(oldHandler);
                }
                finally
                {
                    XCloseDisplay(display);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScreenCapture: Window capture error");
                IsCapturing = false;
            }
        }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private static int NoOpErrorHandler(IntPtr display, IntPtr errorEvent) => 0;

    /// <summary>
    /// Shared capture loop with adaptive FPS and frame-drop support.
    /// getRegion returns (x, y, w, h). (-1,-1,-1,-1) = stop, (0,0,0,0) = skip frame.
    /// </summary>
    private void RunCaptureLoop(IntPtr display, ulong drawable,
        Func<(int x, int y, int w, int h)> getRegion,
        Action<byte[], int, int> onFrame, CancellationToken ct)
    {
        const int minFps = 5;
        const int maxFps = 30;
        var encoding = 0; // 0 = idle, 1 = busy

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            var (rx, ry, rw, rh) = getRegion();
            if (rx == -1) break;       // window gone
            if (rw <= 0 || rh <= 0)    // hidden / skip
            {
                Thread.Sleep(100);
                continue;
            }

            // Skip this frame if the encoder is still busy with the previous one
            if (Interlocked.CompareExchange(ref encoding, 1, 0) != 0)
            {
                Thread.Sleep(10);
                continue;
            }

            var rgbaData = CaptureDrawable(display, drawable, rx, ry, rw, rh);
            if (rgbaData == null)
            {
                Interlocked.Exchange(ref encoding, 0);
                Thread.Sleep(10);
                continue;
            }

            var capturedW = rw;
            var capturedH = rh;

            // Fire encode on threadpool so capture thread isn't blocked by VP8
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                try
                {
                    onFrame(rgbaData, capturedW, capturedH);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "ScreenCapture: onFrame error");
                }
                finally
                {
                    Interlocked.Exchange(ref encoding, 0);
                }
            }, null);

            sw.Stop();

            // Adaptive frame interval: slow down if capture itself is heavy
            var captureMs = sw.Elapsed.TotalMilliseconds;
            var targetFps = captureMs > 50 ? minFps : Math.Max(minFps, Math.Min(maxFps, (int)(1000.0 / (captureMs * 2))));
            var frameInterval = 1000.0 / targetFps;
            var remaining = (int)(frameInterval - captureMs);
            if (remaining > 0)
                Thread.Sleep(remaining);
        }
    }

    /// <summary>
    /// Capture a region and convert BGRA→RGBA directly from native XImage memory.
    /// Returns null on failure. Does NOT allocate an intermediate full-size managed copy.
    /// </summary>
    private static unsafe byte[]? CaptureDrawable(IntPtr display, ulong drawable, int x, int y, int width, int height)
    {
        var image = XGetImage(display, drawable, x, y, (uint)width, (uint)height, ~0UL, 2 /* ZPixmap */);
        if (image == IntPtr.Zero)
            return null;

        try
        {
            var ximage = Marshal.PtrToStructure<XImage>(image);
            if (ximage.bits_per_pixel != 32)
                return null;

            var rgbaData = new byte[width * height * 4];
            var src = (byte*)ximage.data;
            var bpl = ximage.bytes_per_line;

            fixed (byte* dst = rgbaData)
            {
                for (int row = 0; row < height; row++)
                {
                    var srcRow = src + row * bpl;
                    var dstRow = dst + row * width * 4;
                    for (int col = 0; col < width; col++)
                    {
                        var s = srcRow + col * 4;
                        var d = dstRow + col * 4;
                        d[0] = s[2]; // R
                        d[1] = s[1]; // G
                        d[2] = s[0]; // B
                        d[3] = 255;  // A
                    }
                }
            }

            return rgbaData;
        }
        finally
        {
            XDestroyImage(image);
        }
    }

    // X11 P/Invoke declarations
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
    private static extern IntPtr XGetImage(IntPtr display, ulong drawable, int x, int y, uint width, uint height, ulong planeMask, int format);

    [DllImport("libX11.so.6")]
    private static extern int XDestroyImage(IntPtr image);

    [DllImport("libX11.so.6")]
    private static extern int XQueryTree(IntPtr display, ulong window, out ulong rootReturn,
        out ulong parentReturn, out IntPtr childrenReturn, out uint nChildrenReturn);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowAttributes(IntPtr display, ulong window, out XWindowAttributes attributes);

    [DllImport("libX11.so.6")]
    private static extern int XFetchName(IntPtr display, ulong window, out IntPtr windowName);

    [DllImport("libX11.so.6")]
    private static extern ulong XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowProperty(IntPtr display, ulong window, ulong property,
        long longOffset, long longLength, bool delete, ulong reqType,
        out ulong actualTypeReturn, out int actualFormatReturn, out ulong nItemsReturn,
        out ulong bytesAfterReturn, out IntPtr propReturn);

    [DllImport("libX11.so.6")]
    private static extern int XTranslateCoordinates(IntPtr display, ulong srcWindow, ulong dstWindow,
        int srcX, int srcY, out int dstX, out int dstY, out ulong childReturn);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XErrorHandlerDelegate(IntPtr display, IntPtr errorEvent);

    [DllImport("libX11.so.6")]
    private static extern XErrorHandlerDelegate XSetErrorHandler(XErrorHandlerDelegate handler);

    [StructLayout(LayoutKind.Sequential)]
    private struct XImage
    {
        public int width;
        public int height;
        public int xoffset;
        public int format;
        public IntPtr data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XWindowAttributes
    {
        public int x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public ulong root;
        public int @class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public int save_under;
        public ulong colormap;
        public int map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public int override_redirect;
        public IntPtr screen;
    }
}
