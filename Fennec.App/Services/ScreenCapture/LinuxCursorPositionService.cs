using System.Runtime.InteropServices;
using Fennec.App.Messages;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Linux cursor position service using X11 event loop with XFixes for cursor type detection.
/// Uses MotionNotify events instead of polling. Supports window-relative coordinates.
/// </summary>
public class LinuxCursorPositionService : ICursorPositionService
{
    private readonly ILogger<LinuxCursorPositionService> _logger;
    private Thread? _eventThread;
    private volatile bool _cancelled;

    // Cached state updated from event thread
    private CursorType _currentCursorType = CursorType.Arrow;

    public event Action<float, float, CursorType, bool>? OnCursorChanged;

    public LinuxCursorPositionService(ILogger<LinuxCursorPositionService> logger)
    {
        _logger = logger;
    }

    public void Start(CaptureTarget target)
    {
        Stop();
        _cancelled = false;
        _eventThread = new Thread(() => RunEventLoop(target))
        {
            Name = "CursorPositionEventLoop",
            IsBackground = true,
        };
        _eventThread.Start();
    }

    public void Stop()
    {
        _cancelled = true;
        _eventThread?.Join(2000);
        _eventThread = null;
    }

    private void RunEventLoop(CaptureTarget target)
    {
        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            _logger.LogWarning("CursorPosition: Failed to open X11 display");
            return;
        }

        try
        {
            var screen = XDefaultScreen(display);
            var root = XRootWindow(display, screen);
            var screenWidth = XDisplayWidth(display, screen);
            var screenHeight = XDisplayHeight(display, screen);

            // Determine if we're tracking a window
            var isWindowTarget = target.Kind == CaptureTargetKind.Window && target.Id.StartsWith("x11:0x");
            ulong targetWindow = 0;
            int winX = 0, winY = 0, winW = screenWidth, winH = screenHeight;

            if (isWindowTarget)
            {
                targetWindow = Convert.ToUInt64(target.Id["x11:".Length..], 16);

                // Get initial window position
                if (XGetWindowAttributes(display, targetWindow, out var attrs) != 0)
                {
                    XTranslateCoordinates(display, targetWindow, root, 0, 0, out winX, out winY, out _);
                    winW = attrs.width;
                    winH = attrs.height;
                }

                // Subscribe to window move/resize events
                XSelectInput(display, targetWindow, StructureNotifyMask);
            }

            // Subscribe to pointer motion on root window
            XSelectInput(display, root, PointerMotionMask);

            // Try to initialize XFixes for cursor type tracking
            var xfixesAvailable = false;
            var xfixesEventBase = 0;
            if (XFixesQueryExtension(display, out xfixesEventBase, out _) != 0)
            {
                XFixesSelectCursorInput(display, root, XFixesDisplayCursorNotifyMask);
                xfixesAvailable = true;
                _logger.LogDebug("CursorPosition: XFixes available, cursor type tracking enabled");
            }
            else
            {
                _logger.LogDebug("CursorPosition: XFixes not available, cursor type will always be Arrow");
            }

            _logger.LogInformation("CursorPosition: Event loop started, screen {W}x{H}, window target: {IsWindow}",
                screenWidth, screenHeight, isWindowTarget);

            float lastX = -1, lastY = -1;
            var lastVisible = false;
            var lastType = CursorType.Arrow;

            while (!_cancelled)
            {
                // Use XPending + small sleep to allow cancellation checks
                if (XPending(display) == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                XNextEvent(display, out var ev);

                switch (ev.type)
                {
                    case MotionNotify:
                    {
                        var rootCursorX = ev.xmotion.x_root;
                        var rootCursorY = ev.xmotion.y_root;

                        float normalizedX, normalizedY;
                        bool isVisible;

                        if (isWindowTarget)
                        {
                            // Window-relative coordinates
                            var relX = rootCursorX - winX;
                            var relY = rootCursorY - winY;
                            isVisible = relX >= 0 && relY >= 0 && relX < winW && relY < winH;
                            normalizedX = winW > 0 ? Math.Clamp((float)relX / winW, 0f, 1f) : 0f;
                            normalizedY = winH > 0 ? Math.Clamp((float)relY / winH, 0f, 1f) : 0f;
                        }
                        else
                        {
                            // Screen-relative coordinates
                            normalizedX = screenWidth > 0 ? Math.Clamp((float)rootCursorX / screenWidth, 0f, 1f) : 0f;
                            normalizedY = screenHeight > 0 ? Math.Clamp((float)rootCursorY / screenHeight, 0f, 1f) : 0f;
                            isVisible = true;
                        }

                        var cursorType = _currentCursorType;

                        if (Math.Abs(normalizedX - lastX) > 0.0001f
                            || Math.Abs(normalizedY - lastY) > 0.0001f
                            || isVisible != lastVisible
                            || cursorType != lastType)
                        {
                            lastX = normalizedX;
                            lastY = normalizedY;
                            lastVisible = isVisible;
                            lastType = cursorType;
                            OnCursorChanged?.Invoke(normalizedX, normalizedY, cursorType, isVisible);
                        }
                        break;
                    }

                    case ConfigureNotify when isWindowTarget:
                    {
                        // Window moved or resized — update cached bounds
                        if ((ulong)ev.xconfigure.window == targetWindow)
                        {
                            XTranslateCoordinates(display, targetWindow, root, 0, 0, out winX, out winY, out _);
                            winW = ev.xconfigure.width;
                            winH = ev.xconfigure.height;
                        }
                        break;
                    }

                    default:
                    {
                        // Check for XFixes cursor notify event
                        if (xfixesAvailable && ev.type == xfixesEventBase + XFixesCursorNotify)
                        {
                            var cursorImage = XFixesGetCursorImage(display);
                            if (cursorImage != IntPtr.Zero)
                            {
                                try
                                {
                                    var image = Marshal.PtrToStructure<XFixesCursorImageStruct>(cursorImage);
                                    var atomName = XGetAtomName(display, image.atom);
                                    if (atomName != IntPtr.Zero)
                                    {
                                        var name = Marshal.PtrToStringUTF8(atomName) ?? "";
                                        XFree(atomName);
                                        _currentCursorType = MapCursorName(name);
                                    }
                                }
                                finally
                                {
                                    XFree(cursorImage);
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (!_cancelled)
                _logger.LogError(ex, "CursorPosition: Event loop error");
        }
        finally
        {
            XCloseDisplay(display);
            _logger.LogInformation("CursorPosition: Event loop stopped");
        }
    }

    private static readonly Dictionary<string, CursorType> CursorNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Arrow
        ["left_ptr"] = CursorType.Arrow,
        ["default"] = CursorType.Arrow,
        ["arrow"] = CursorType.Arrow,
        // Hand
        ["hand2"] = CursorType.Hand,
        ["pointer"] = CursorType.Hand,
        ["pointing_hand"] = CursorType.Hand,
        // Text
        ["xterm"] = CursorType.Text,
        ["text"] = CursorType.Text,
        ["ibeam"] = CursorType.Text,
        // Crosshair
        ["crosshair"] = CursorType.Crosshair,
        ["cross"] = CursorType.Crosshair,
        // ResizeNS
        ["sb_v_double_arrow"] = CursorType.ResizeNS,
        ["ns-resize"] = CursorType.ResizeNS,
        ["size_ver"] = CursorType.ResizeNS,
        ["top_side"] = CursorType.ResizeNS,
        ["bottom_side"] = CursorType.ResizeNS,
        // ResizeEW
        ["sb_h_double_arrow"] = CursorType.ResizeEW,
        ["ew-resize"] = CursorType.ResizeEW,
        ["size_hor"] = CursorType.ResizeEW,
        ["left_side"] = CursorType.ResizeEW,
        ["right_side"] = CursorType.ResizeEW,
        // ResizeNESW
        ["size_bdiag"] = CursorType.ResizeNESW,
        ["nesw-resize"] = CursorType.ResizeNESW,
        ["bottom_left_corner"] = CursorType.ResizeNESW,
        ["top_right_corner"] = CursorType.ResizeNESW,
        // ResizeNWSE
        ["size_fdiag"] = CursorType.ResizeNWSE,
        ["nwse-resize"] = CursorType.ResizeNWSE,
        ["bottom_right_corner"] = CursorType.ResizeNWSE,
        ["top_left_corner"] = CursorType.ResizeNWSE,
        // Move
        ["fleur"] = CursorType.Move,
        ["move"] = CursorType.Move,
        ["grab"] = CursorType.Move,
        ["grabbing"] = CursorType.Move,
        ["all-scroll"] = CursorType.Move,
        // NotAllowed
        ["not-allowed"] = CursorType.NotAllowed,
        ["crossed_circle"] = CursorType.NotAllowed,
        ["X_cursor"] = CursorType.NotAllowed,
        // Wait
        ["watch"] = CursorType.Wait,
        ["wait"] = CursorType.Wait,
        ["left_ptr_watch"] = CursorType.Wait,
        // Help
        ["question_arrow"] = CursorType.Help,
        ["help"] = CursorType.Help,
        ["whats_this"] = CursorType.Help,
    };

    internal static CursorType MapCursorName(string name)
    {
        return CursorNameMap.TryGetValue(name, out var type) ? type : CursorType.Arrow;
    }

    // X11 event types
    private const int MotionNotify = 6;
    private const int ConfigureNotify = 22;

    // X11 event masks
    private const long PointerMotionMask = 1L << 6;
    private const long StructureNotifyMask = 1L << 17;

    // XFixes constants
    private const int XFixesCursorNotify = 1;
    private const long XFixesDisplayCursorNotifyMask = 1L << 0;

    // X11 event union — we only need the fields we access
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(0)] public XMotionEvent xmotion;
        [FieldOffset(0)] public XConfigureEvent xconfigure;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XMotionEvent
    {
        public int type;
        public ulong serial;
        public int send_event;
        public IntPtr display;
        public ulong window;
        public ulong root;
        public ulong subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public byte is_hint;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XConfigureEvent
    {
        public int type;
        public ulong serial;
        public int send_event;
        public IntPtr display;
        public ulong @event;
        public ulong window;
        public int x, y;
        public int width, height;
        public int border_width;
        public ulong above;
        public int override_redirect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XFixesCursorImageStruct
    {
        public short x, y;
        public ushort width, height;
        public ushort xhot, yhot;
        public uint cursor_serial;
        public IntPtr pixels;
        public ulong atom;
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
    private static extern int XSelectInput(IntPtr display, ulong window, long eventMask);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(IntPtr display, out XEvent eventReturn);

    [DllImport("libX11.so.6")]
    private static extern int XPending(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowAttributes(IntPtr display, ulong window, out XWindowAttributes attributes);

    [DllImport("libX11.so.6")]
    private static extern int XTranslateCoordinates(IntPtr display, ulong srcWindow, ulong dstWindow,
        int srcX, int srcY, out int dstX, out int dstY, out ulong childReturn);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XGetAtomName(IntPtr display, ulong atom);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);

    [DllImport("libXfixes.so.3")]
    private static extern int XFixesQueryExtension(IntPtr display, out int eventBase, out int errorBase);

    [DllImport("libXfixes.so.3")]
    private static extern void XFixesSelectCursorInput(IntPtr display, ulong window, long eventMask);

    [DllImport("libXfixes.so.3")]
    private static extern IntPtr XFixesGetCursorImage(IntPtr display);
}
