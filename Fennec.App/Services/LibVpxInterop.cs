using System.Runtime.InteropServices;

namespace Fennec.App.Services;

/// <summary>
/// Direct P/Invoke wrapper for system libvpx VP8 encoder and decoder.
/// Replaces SIPSorceryMedia.Encoders which has ABI version mismatches on Linux.
/// </summary>
internal static class LibVpxNative
{
    private const string Lib = "libvpx";

    // Constants from vpx headers (for libvpx 1.15.x on linux-x64)
    public const int VPX_ENCODER_ABI_VERSION = 37;
    public const int VPX_DECODER_ABI_VERSION = 12;
    public const int VPX_IMG_FMT_I420 = 258;
    public const int VPX_CODEC_CX_FRAME_PKT = 0;
    public const long VPX_DL_REALTIME = 1;
    public const int VPX_EFLAG_FORCE_KF = 1;

    // Struct sizes (linux-x64, libvpx 1.15.x)
    public const int SIZEOF_CODEC_CTX = 56;
    public const int SIZEOF_ENC_CFG = 504;
    public const int SIZEOF_IMAGE = 136;

    // vpx_codec_enc_cfg_t field offsets
    public const int CFG_G_THREADS = 4;
    public const int CFG_G_W = 12;
    public const int CFG_G_H = 16;
    public const int CFG_G_TIMEBASE_NUM = 28;
    public const int CFG_G_TIMEBASE_DEN = 32;
    public const int CFG_G_ERROR_RESILIENT = 36;
    public const int CFG_G_LAG_IN_FRAMES = 44;
    public const int CFG_RC_END_USAGE = 72;
    public const int CFG_RC_TARGET_BITRATE = 112;
    public const int CFG_KF_MAX_DIST = 168;

    // vpx_image_t field offsets
    public const int IMG_D_W = 24;
    public const int IMG_D_H = 28;
    public const int IMG_PLANES = 48;   // 4x IntPtr
    public const int IMG_STRIDE = 80;   // 4x int
    public const int IMG_DATA = 112;    // IntPtr

    // vpx_codec_cx_pkt_t field offsets
    public const int PKT_KIND = 0;
    public const int PKT_FRAME_BUF = 8;
    public const int PKT_FRAME_SZ = 16;

    [DllImport(Lib)] public static extern IntPtr vpx_codec_vp8_cx();
    [DllImport(Lib)] public static extern IntPtr vpx_codec_vp8_dx();

    [DllImport(Lib)]
    public static extern int vpx_codec_enc_config_default(IntPtr iface, IntPtr cfg, uint usage);

    [DllImport(Lib)]
    public static extern int vpx_codec_enc_init_ver(IntPtr ctx, IntPtr iface, IntPtr cfg, int flags, int ver);

    [DllImport(Lib)]
    public static extern int vpx_codec_encode(IntPtr ctx, IntPtr img, long pts, ulong duration, int flags, long deadline);

    [DllImport(Lib)]
    public static extern IntPtr vpx_codec_get_cx_data(IntPtr ctx, ref IntPtr iter);

    [DllImport(Lib)]
    public static extern int vpx_codec_dec_init_ver(IntPtr ctx, IntPtr iface, IntPtr cfg, int flags, int ver);

    [DllImport(Lib)]
    public static extern int vpx_codec_decode(IntPtr ctx, IntPtr data, uint dataLen, IntPtr userData, long deadline);

    [DllImport(Lib)]
    public static extern IntPtr vpx_codec_get_frame(IntPtr ctx, ref IntPtr iter);

    [DllImport(Lib)]
    public static extern int vpx_codec_destroy(IntPtr ctx);

    [DllImport(Lib)]
    public static extern IntPtr vpx_img_alloc(IntPtr img, int fmt, uint dw, uint dh, uint align);

    [DllImport(Lib)]
    public static extern void vpx_img_free(IntPtr img);

    [DllImport(Lib)]
    public static extern IntPtr vpx_codec_error(IntPtr ctx);

    [DllImport(Lib)]
    public static extern IntPtr vpx_codec_error_detail(IntPtr ctx);
}

public sealed class LibVpxEncoder : IDisposable
{
    private IntPtr _ctx;
    private IntPtr _cfg;
    private IntPtr _img;
    private readonly int _width;
    private readonly int _height;
    private bool _disposed;

    public LibVpxEncoder(int width, int height, int bitrateKbps = 1500)
    {
        _width = width;
        _height = height;

        _cfg = Marshal.AllocHGlobal(LibVpxNative.SIZEOF_ENC_CFG);
        var iface = LibVpxNative.vpx_codec_vp8_cx();

        var res = LibVpxNative.vpx_codec_enc_config_default(iface, _cfg, 0);
        if (res != 0) throw new InvalidOperationException($"vpx_codec_enc_config_default failed: {res}");

        // Configure encoder
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_W, width);
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_H, height);
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_THREADS, Math.Min(Environment.ProcessorCount, 8));
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_TIMEBASE_NUM, 1);
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_TIMEBASE_DEN, 90000); // RTP timebase
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_ERROR_RESILIENT, 1);
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_G_LAG_IN_FRAMES, 0);
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_RC_END_USAGE, 1); // VPX_CBR
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_RC_TARGET_BITRATE, bitrateKbps);
        Marshal.WriteInt32(_cfg + LibVpxNative.CFG_KF_MAX_DIST, 150); // keyframe every 5s at 30fps

        _ctx = Marshal.AllocHGlobal(LibVpxNative.SIZEOF_CODEC_CTX);
        // Zero-init the context
        unsafe { new Span<byte>((void*)_ctx, LibVpxNative.SIZEOF_CODEC_CTX).Clear(); }

        res = LibVpxNative.vpx_codec_enc_init_ver(_ctx, iface, _cfg, 0, LibVpxNative.VPX_ENCODER_ABI_VERSION);
        if (res != 0)
        {
            var errMsg = Marshal.PtrToStringAnsi(LibVpxNative.vpx_codec_error(_ctx)) ?? "unknown";
            throw new InvalidOperationException($"vpx_codec_enc_init failed ({res}): {errMsg}");
        }

        _img = LibVpxNative.vpx_img_alloc(IntPtr.Zero, LibVpxNative.VPX_IMG_FMT_I420, (uint)width, (uint)height, 1);
        if (_img == IntPtr.Zero) throw new InvalidOperationException("vpx_img_alloc failed");
    }

    public byte[]? Encode(byte[] i420Data, long pts, bool forceKeyFrame)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LibVpxEncoder));

        // Copy I420 data into the vpx_image planes
        CopyI420ToImage(i420Data);

        var flags = forceKeyFrame ? LibVpxNative.VPX_EFLAG_FORCE_KF : 0;
        var res = LibVpxNative.vpx_codec_encode(_ctx, _img, pts, 3000, flags, LibVpxNative.VPX_DL_REALTIME);
        if (res != 0) return null;

        // Get encoded packet
        var iter = IntPtr.Zero;
        var pkt = LibVpxNative.vpx_codec_get_cx_data(_ctx, ref iter);
        while (pkt != IntPtr.Zero)
        {
            var kind = Marshal.ReadInt32(pkt + LibVpxNative.PKT_KIND);
            if (kind == LibVpxNative.VPX_CODEC_CX_FRAME_PKT)
            {
                var buf = Marshal.ReadIntPtr(pkt + LibVpxNative.PKT_FRAME_BUF);
                var sz = (int)Marshal.ReadInt64(pkt + LibVpxNative.PKT_FRAME_SZ);
                var result = new byte[sz];
                Marshal.Copy(buf, result, 0, sz);
                return result;
            }
            pkt = LibVpxNative.vpx_codec_get_cx_data(_ctx, ref iter);
        }

        return null;
    }

    private void CopyI420ToImage(byte[] i420Data)
    {
        var yPlane = Marshal.ReadIntPtr(_img + LibVpxNative.IMG_PLANES);
        var uPlane = Marshal.ReadIntPtr(_img + LibVpxNative.IMG_PLANES + IntPtr.Size);
        var vPlane = Marshal.ReadIntPtr(_img + LibVpxNative.IMG_PLANES + IntPtr.Size * 2);

        var yStride = Marshal.ReadInt32(_img + LibVpxNative.IMG_STRIDE);
        var uStride = Marshal.ReadInt32(_img + LibVpxNative.IMG_STRIDE + 4);

        var ySize = _width * _height;
        var uvWidth = _width / 2;
        var uvHeight = _height / 2;

        // If strides match the width (align=1), copy directly
        if (yStride == _width && uStride == uvWidth)
        {
            Marshal.Copy(i420Data, 0, yPlane, ySize);
            Marshal.Copy(i420Data, ySize, uPlane, uvWidth * uvHeight);
            Marshal.Copy(i420Data, ySize + uvWidth * uvHeight, vPlane, uvWidth * uvHeight);
        }
        else
        {
            // Row-by-row copy respecting strides
            for (int y = 0; y < _height; y++)
                Marshal.Copy(i420Data, y * _width, yPlane + y * yStride, _width);
            for (int y = 0; y < uvHeight; y++)
            {
                Marshal.Copy(i420Data, ySize + y * uvWidth, uPlane + y * uStride, uvWidth);
                Marshal.Copy(i420Data, ySize + uvWidth * uvHeight + y * uvWidth, vPlane + y * uStride, uvWidth);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_img != IntPtr.Zero) { LibVpxNative.vpx_img_free(_img); _img = IntPtr.Zero; }
        if (_ctx != IntPtr.Zero) { LibVpxNative.vpx_codec_destroy(_ctx); Marshal.FreeHGlobal(_ctx); _ctx = IntPtr.Zero; }
        if (_cfg != IntPtr.Zero) { Marshal.FreeHGlobal(_cfg); _cfg = IntPtr.Zero; }
    }
}

public sealed class LibVpxDecoder : IDisposable
{
    private IntPtr _ctx;
    private bool _disposed;

    public LibVpxDecoder()
    {
        _ctx = Marshal.AllocHGlobal(LibVpxNative.SIZEOF_CODEC_CTX);
        unsafe { new Span<byte>((void*)_ctx, LibVpxNative.SIZEOF_CODEC_CTX).Clear(); }

        var res = LibVpxNative.vpx_codec_dec_init_ver(
            _ctx, LibVpxNative.vpx_codec_vp8_dx(), IntPtr.Zero, 0, LibVpxNative.VPX_DECODER_ABI_VERSION);
        if (res != 0)
        {
            var errMsg = Marshal.PtrToStringAnsi(LibVpxNative.vpx_codec_error(_ctx)) ?? "unknown";
            throw new InvalidOperationException($"vpx_codec_dec_init failed ({res}): {errMsg}");
        }
    }

    /// <summary>
    /// Decode a VP8 frame. Returns (rgbaData, width, height) or null if no frame produced.
    /// </summary>
    public (byte[] Rgba, int Width, int Height)? Decode(byte[] vp8Data)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LibVpxDecoder));

        var pin = GCHandle.Alloc(vp8Data, GCHandleType.Pinned);
        try
        {
            var res = LibVpxNative.vpx_codec_decode(_ctx, pin.AddrOfPinnedObject(), (uint)vp8Data.Length, IntPtr.Zero, 0);
            if (res != 0) return null;
        }
        finally { pin.Free(); }

        var iter = IntPtr.Zero;
        var img = LibVpxNative.vpx_codec_get_frame(_ctx, ref iter);
        if (img == IntPtr.Zero) return null;

        var width = Marshal.ReadInt32(img + LibVpxNative.IMG_D_W);
        var height = Marshal.ReadInt32(img + LibVpxNative.IMG_D_H);

        var yPlane = Marshal.ReadIntPtr(img + LibVpxNative.IMG_PLANES);
        var uPlane = Marshal.ReadIntPtr(img + LibVpxNative.IMG_PLANES + IntPtr.Size);
        var vPlane = Marshal.ReadIntPtr(img + LibVpxNative.IMG_PLANES + IntPtr.Size * 2);
        var yStride = Marshal.ReadInt32(img + LibVpxNative.IMG_STRIDE);
        var uStride = Marshal.ReadInt32(img + LibVpxNative.IMG_STRIDE + 4);
        var vStride = Marshal.ReadInt32(img + LibVpxNative.IMG_STRIDE + 8);

        var rgba = I420ToRgba(yPlane, uPlane, vPlane, yStride, uStride, vStride, width, height);
        return (rgba, width, height);
    }

    private static byte[] I420ToRgba(IntPtr yP, IntPtr uP, IntPtr vP,
        int yStride, int uStride, int vStride, int w, int h)
    {
        var rgba = new byte[w * h * 4];

        for (int row = 0; row < h; row++)
        {
            for (int col = 0; col < w; col++)
            {
                var y = Marshal.ReadByte(yP + row * yStride + col) - 16;
                var u = Marshal.ReadByte(uP + (row / 2) * uStride + (col / 2)) - 128;
                var v = Marshal.ReadByte(vP + (row / 2) * vStride + (col / 2)) - 128;

                // BT.601 YUV to RGB
                var r = Math.Clamp((298 * y + 409 * v + 128) >> 8, 0, 255);
                var g = Math.Clamp((298 * y - 100 * u - 208 * v + 128) >> 8, 0, 255);
                var b = Math.Clamp((298 * y + 516 * u + 128) >> 8, 0, 255);

                var idx = (row * w + col) * 4;
                rgba[idx] = (byte)r;
                rgba[idx + 1] = (byte)g;
                rgba[idx + 2] = (byte)b;
                rgba[idx + 3] = 255;
            }
        }

        return rgba;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ctx != IntPtr.Zero) { LibVpxNative.vpx_codec_destroy(_ctx); Marshal.FreeHGlobal(_ctx); _ctx = IntPtr.Zero; }
    }
}
