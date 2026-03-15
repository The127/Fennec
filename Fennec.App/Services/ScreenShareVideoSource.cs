using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

/// <summary>
/// Bridges screen capture frames to VP8-encoded video samples for WebRTC transmission.
/// Takes RGBA frames from IScreenCaptureService, converts to I420, encodes to VP8.
/// Downscales frames exceeding MaxWidth/MaxHeight to keep encoding performant.
/// </summary>
public class ScreenShareVideoSource : IDisposable
{
    private const int MaxWidth = 1920;
    private const int MaxHeight = 1080;

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

    public ScreenShareVideoSource(ILogger logger, uint frameRate = 30)
    {
        _logger = logger;
        _frameRate = frameRate;
    }

    /// <summary>
    /// Process an incoming RGBA frame: downscale if needed, convert to I420, VP8-encode.
    /// </summary>
    public void OnFrame(byte[] rgbaData, int width, int height)
    {
        try
        {
            // Compute target resolution (fit within MaxWidth x MaxHeight, preserve aspect ratio)
            ComputeScaledSize(width, height, out var targetW, out var targetH);

            // Ensure even dimensions for I420
            targetW &= ~1;
            targetH &= ~1;

            byte[] frameData;
            int frameW, frameH;

            if (targetW < width || targetH < height)
            {
                frameData = DownscaleRgba(rgbaData, width, height, targetW, targetH);
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
                _logger.LogInformation("ScreenShareVideo: Resolution {SrcW}x{SrcH} -> encode at {W}x{H}",
                    width, height, frameW, frameH);
            }

            _encoder ??= new LibVpxEncoder(frameW, frameH);

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

    private static void ComputeScaledSize(int srcW, int srcH, out int dstW, out int dstH)
    {
        if (srcW <= MaxWidth && srcH <= MaxHeight)
        {
            dstW = srcW;
            dstH = srcH;
            return;
        }

        var scaleX = (double)MaxWidth / srcW;
        var scaleY = (double)MaxHeight / srcH;
        var scale = Math.Min(scaleX, scaleY);
        dstW = (int)(srcW * scale);
        dstH = (int)(srcH * scale);
    }

    private static unsafe byte[] DownscaleRgba(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH * 4];
        // Use fixed-point arithmetic for the inner loop (16.16 format)
        var xStep = (srcW << 16) / dstW;
        var yStep = (srcH << 16) / dstH;

        fixed (byte* pSrc = src, pDst = dst)
        {
            var srcYFp = 0;
            for (int y = 0; y < dstH; y++, srcYFp += yStep)
            {
                var srcRow = pSrc + (srcYFp >> 16) * srcW * 4;
                var dstRow = pDst + y * dstW * 4;
                var srcXFp = 0;
                for (int x = 0; x < dstW; x++, srcXFp += xStep)
                {
                    var s = srcRow + (srcXFp >> 16) * 4;
                    var d = dstRow + x * 4;
                    *(int*)d = *(int*)s; // copy 4 bytes at once
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
