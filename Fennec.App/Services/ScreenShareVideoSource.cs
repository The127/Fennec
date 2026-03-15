using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

/// <summary>
/// Bridges screen capture frames to VP8-encoded video samples for WebRTC transmission.
/// Takes RGBA frames from IScreenCaptureService, converts to I420, encodes to VP8.
/// Downscales frames exceeding MaxWidth/MaxHeight to keep encoding performant.
/// </summary>
public class ScreenShareVideoSource : IDisposable
{
    private readonly int _maxWidth;
    private readonly int _maxHeight;
    private readonly int _bitrateKbps;

    private readonly ILogger _logger;
    private readonly uint _frameRate;
    private LibVpxEncoder? _encoder;
    private int _encWidth;
    private int _encHeight;
    private long _pts;

    /// <summary>
    /// Fires when a VP8-encoded video sample is ready to send via WebRTC.
    /// </summary>
    public event Action<uint, byte[]>? OnVideoSourceEncodedSample;

    public ScreenShareVideoSource(ILogger logger, int maxWidth = 1920, int maxHeight = 1080, int bitrateKbps = 1500, uint frameRate = 30)
    {
        _logger = logger;
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        _bitrateKbps = bitrateKbps;
        _frameRate = frameRate;
    }

    /// <summary>
    /// Process an incoming RGBA frame: downscale if needed, convert to I420, VP8-encode.
    /// </summary>
    public void OnFrame(byte[] rgbaData, int width, int height)
    {
        try
        {
            // Compute target resolution (fit within max dimensions, preserve aspect ratio)
            ComputeScaledSize(width, height, _maxWidth, _maxHeight, out var targetW, out var targetH);

            // Ensure even dimensions for I420
            targetW &= ~1;
            targetH &= ~1;

            byte[] frameData;
            int frameW, frameH;

            if (targetW < width || targetH < height)
            {
                frameData = BilinearDownscaleRgba(rgbaData, width, height, targetW, targetH);
                frameW = targetW;
                frameH = targetH;
            }
            else
            {
                frameData = rgbaData;
                frameW = width;
                frameH = height;
            }

            if (_encWidth != frameW || _encHeight != frameH)
            {
                _encoder?.Dispose();
                _encoder = null;
                _encWidth = frameW;
                _encHeight = frameH;
                _logger.LogInformation("ScreenShareVideo: Resolution {SrcW}x{SrcH} -> encode at {W}x{H}, bitrate={Bitrate}Kbps, fps={Fps}",
                    width, height, frameW, frameH, _bitrateKbps, _frameRate);
            }

            _encoder ??= new LibVpxEncoder(frameW, frameH, _bitrateKbps);

            var i420 = RgbaToI420(frameData, frameW, frameH);

            var forceKeyFrame = _pts == 0;
            var encoded = _encoder.Encode(i420, _pts, forceKeyFrame);
            _pts += 90000 / _frameRate;

            if (encoded != null && encoded.Length > 0)
            {
                var durationRtpUnits = (uint)(90000 / _frameRate);
                OnVideoSourceEncodedSample?.Invoke(durationRtpUnits, encoded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ScreenShareVideo: Encode error");
        }
    }

    /// <summary>
    /// Maps a resolution preset string to max width/height dimensions.
    /// </summary>
    public static (int Width, int Height) ResolutionPresetToDimensions(string preset) => preset switch
    {
        "720p" => (1280, 720),
        "1080p" => (1920, 1080),
        "1440p" => (2560, 1440),
        "Native" => (int.MaxValue, int.MaxValue),
        _ => (1920, 1080),
    };

    internal static void ComputeScaledSize(int srcW, int srcH, int maxWidth, int maxHeight, out int dstW, out int dstH)
    {
        if (srcW <= maxWidth && srcH <= maxHeight)
        {
            dstW = srcW;
            dstH = srcH;
            return;
        }

        var scaleX = (double)maxWidth / srcW;
        var scaleY = (double)maxHeight / srcH;
        var scale = Math.Min(scaleX, scaleY);
        dstW = (int)(srcW * scale);
        dstH = (int)(srcH * scale);
    }

    /// <summary>
    /// Bilinear downscale of an RGBA buffer. Averages a 2x2 block of source pixels per destination pixel.
    /// </summary>
    internal static unsafe byte[] BilinearDownscaleRgba(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH * 4];
        // Fixed-point 16.16 arithmetic for the inner loop
        var xStep = (srcW << 16) / dstW;
        var yStep = (srcH << 16) / dstH;

        fixed (byte* pSrc = src, pDst = dst)
        {
            var srcYFp = 0;
            for (int y = 0; y < dstH; y++, srcYFp += yStep)
            {
                var sy = srcYFp >> 16;
                var sy1 = Math.Min(sy + 1, srcH - 1);
                var srcRow0 = pSrc + sy * srcW * 4;
                var srcRow1 = pSrc + sy1 * srcW * 4;
                var dstRow = pDst + y * dstW * 4;
                var srcXFp = 0;
                for (int x = 0; x < dstW; x++, srcXFp += xStep)
                {
                    var sx = srcXFp >> 16;
                    var sx1 = Math.Min(sx + 1, srcW - 1);

                    var p00 = srcRow0 + sx * 4;
                    var p10 = srcRow0 + sx1 * 4;
                    var p01 = srcRow1 + sx * 4;
                    var p11 = srcRow1 + sx1 * 4;

                    var d = dstRow + x * 4;
                    d[0] = (byte)((p00[0] + p10[0] + p01[0] + p11[0] + 2) >> 2);
                    d[1] = (byte)((p00[1] + p10[1] + p01[1] + p11[1] + 2) >> 2);
                    d[2] = (byte)((p00[2] + p10[2] + p01[2] + p11[2] + 2) >> 2);
                    d[3] = (byte)((p00[3] + p10[3] + p01[3] + p11[3] + 2) >> 2);
                }
            }
        }

        return dst;
    }

    private static byte[] RgbaToI420(byte[] rgba, int width, int height)
    {
        var ySize = width * height;
        var uvSize = (width / 2) * (height / 2);
        var i420 = new byte[ySize + uvSize * 2];

        var yPlane = i420.AsSpan(0, ySize);
        var uPlane = i420.AsSpan(ySize, uvSize);
        var vPlane = i420.AsSpan(ySize + uvSize, uvSize);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var rgbaOffset = (y * width + x) * 4;
                var r = rgba[rgbaOffset];
                var g = rgba[rgbaOffset + 1];
                var b = rgba[rgbaOffset + 2];

                var yVal = (byte)Math.Clamp(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16, 0, 255);
                yPlane[y * width + x] = yVal;

                if (y % 2 == 0 && x % 2 == 0)
                {
                    var uvIndex = (y / 2) * (width / 2) + (x / 2);
                    uPlane[uvIndex] = (byte)Math.Clamp(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128, 0, 255);
                    vPlane[uvIndex] = (byte)Math.Clamp(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128, 0, 255);
                }
            }
        }

        return i420;
    }

    public void Dispose()
    {
        _encoder?.Dispose();
        _encoder = null;
    }
}
