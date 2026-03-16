using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public sealed class H264Encoder : IDisposable
{
    private IntPtr _encoder;
    private int _width;
    private int _height;
    private int _bitrateKbps;
    private int _fps;
    private readonly ILogger _logger;
    private bool _disposed;

    // Pin delegates to prevent GC during native callbacks
    private readonly NativeVideoInterop.NalCallback _nalCallbackDelegate;

    // Temporary state during Encode call
    private Action<byte[], long, bool>? _currentCallback;

    public string EncoderName { get; }

    public H264Encoder(ILogger logger, int width, int height, int bitrateKbps = 1500, int fps = 30)
    {
        _logger = logger;
        _width = width & ~1;
        _height = height & ~1;
        _bitrateKbps = bitrateKbps;
        _fps = fps;

        _nalCallbackDelegate = OnNalUnit;

        _encoder = NativeVideoInterop.fennec_encoder_create(_width, _height, _bitrateKbps, _fps);
        if (_encoder == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create native H.264 encoder");

        EncoderName = Marshal.PtrToStringUTF8(NativeVideoInterop.fennec_encoder_get_name(_encoder)) ?? "unknown";

        _logger.LogInformation("H264Encoder: Created {W}x{H}, bitrate={Bitrate}Kbps, fps={Fps}, encoder={Encoder}",
            _width, _height, _bitrateKbps, _fps, EncoderName);
    }

    public void Encode(byte[] rgbaData, int width, int height, long pts, bool forceKeyframe,
        Action<byte[], long, bool> onNalUnit)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(H264Encoder));

        width &= ~1;
        height &= ~1;

        if (width != _width || height != _height)
        {
            var result = NativeVideoInterop.fennec_encoder_update_size(_encoder, width, height);
            if (result != 0)
            {
                _logger.LogWarning("H264Encoder: Failed to update size to {W}x{H}", width, height);
                return;
            }
            _width = width;
            _height = height;
            _logger.LogInformation("H264Encoder: Resized to {W}x{H}", width, height);
        }

        _currentCallback = onNalUnit;

        var pin = GCHandle.Alloc(rgbaData, GCHandleType.Pinned);
        try
        {
            var status = NativeVideoInterop.fennec_encoder_encode_rgba(
                _encoder, pin.AddrOfPinnedObject(), width, height,
                pts, forceKeyframe ? 1 : 0, _nalCallbackDelegate, IntPtr.Zero);

            if (status != 0)
                _logger.LogWarning("H264Encoder: Encode returned {Status}", status);
        }
        finally
        {
            pin.Free();
            _currentCallback = null;
        }
    }

    private void OnNalUnit(IntPtr nalData, int nalSize, long pts, int isKeyframe, IntPtr userData)
    {
        if (_currentCallback == null || nalSize <= 0) return;

        var nal = new byte[nalSize];
        Marshal.Copy(nalData, nal, 0, nalSize);
        _currentCallback(nal, pts, isKeyframe != 0);
    }

    public void UpdateBitrate(int kbps)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(H264Encoder));
        var result = NativeVideoInterop.fennec_encoder_update_bitrate(_encoder, kbps);
        if (result != 0)
        {
            _logger.LogWarning("H264Encoder: Failed to update bitrate to {Kbps}", kbps);
            return;
        }
        _bitrateKbps = kbps;
        _logger.LogInformation("H264Encoder: Bitrate updated to {Kbps}Kbps", kbps);
    }

    public void UpdateFps(int fps)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(H264Encoder));
        var result = NativeVideoInterop.fennec_encoder_update_fps(_encoder, fps);
        if (result != 0)
        {
            _logger.LogWarning("H264Encoder: Failed to update FPS to {Fps}", fps);
            return;
        }
        _fps = fps;
        _logger.LogInformation("H264Encoder: FPS updated to {Fps}", fps);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_encoder != IntPtr.Zero)
        {
            NativeVideoInterop.fennec_encoder_destroy(_encoder);
            _encoder = IntPtr.Zero;
        }
    }
}
