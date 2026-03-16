using System.Runtime.InteropServices;

namespace Fennec.App.Services;

internal static class NativeVideoInterop
{
    private const string Lib = "libfennec_video";

    // --- Callbacks ---

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NalCallback(IntPtr nalData, int nalSize, long pts, int isKeyframe, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FrameCallback(IntPtr rgbaData, int width, int height, IntPtr userData);

    // --- Standalone encoder ---

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fennec_encoder_create(int width, int height, int bitrateKbps, int fps);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_encoder_encode_rgba(IntPtr enc, IntPtr rgba, int w, int h,
        long pts, int forceKf, NalCallback cb, IntPtr ud);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_encoder_update_size(IntPtr enc, int w, int h);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_encoder_update_bitrate(IntPtr enc, int bitrateKbps);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_encoder_update_fps(IntPtr enc, int fps);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fennec_encoder_destroy(IntPtr enc);

    // --- Fused capture+encode (macOS) ---

    [StructLayout(LayoutKind.Sequential)]
    public struct FennecCaptureTarget
    {
        public IntPtr Id;
        public IntPtr Name;
        public int Width;
        public int Height;
        public int IsWindow;
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_capture_list_targets(out IntPtr targets);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fennec_capture_free_targets(IntPtr targets, int count);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fennec_capture_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string targetId,
        int maxW, int maxH, int bitrateKbps, int fps,
        NalCallback nalCb, FrameCallback previewCb, IntPtr userData);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_capture_start(IntPtr cap);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_capture_stop(IntPtr cap);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_capture_update_bitrate(IntPtr cap, int bitrateKbps);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_capture_update_fps(IntPtr cap, int fps);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fennec_capture_destroy(IntPtr cap);

    // --- Decoder ---

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fennec_decoder_create();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int fennec_decoder_decode(IntPtr dec, IntPtr nal, int size,
        FrameCallback cb, IntPtr ud);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fennec_decoder_destroy(IntPtr dec);
}
