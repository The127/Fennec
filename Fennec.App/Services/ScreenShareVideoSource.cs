using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

/// <summary>
/// Bridges screen capture frames to H.264-encoded video samples for WebRTC transmission.
/// Two modes:
/// - Standalone (Linux): Takes RGBA frames, encodes to H.264 NAL units via native encoder
/// - Fused (macOS): NAL units arrive directly from native capture+encode, passed through
/// </summary>
public class ScreenShareVideoSource : IDisposable
{
    private int _maxWidth;
    private int _maxHeight;
    private int _bitrateKbps;

    private readonly ILogger _logger;
    private uint _frameRate;
    private H264Encoder? _encoder;
    private long _pts;
    private volatile bool _forceNextKeyFrame;

    /// <summary>
    /// Fires when an H.264 NAL unit is ready to send via WebRTC.
    /// Parameters: (durationRtpUnits, nalData, isKeyframe)
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
    /// Process an incoming RGBA frame (Linux standalone mode): downscale if needed, H.264-encode.
    /// </summary>
    public void OnFrame(byte[] rgbaData, int width, int height)
    {
        try
        {
            ComputeScaledSize(width, height, _maxWidth, _maxHeight, out var targetW, out var targetH);
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

            _encoder ??= new H264Encoder(_logger, frameW, frameH, _bitrateKbps, (int)_frameRate);

            var forceKeyFrame = _pts == 0 || _forceNextKeyFrame;
            _forceNextKeyFrame = false;
            var durationRtpUnits = (uint)(90000 / _frameRate);

            // Collect all NAL units for this frame into an Annex B access unit
            using var accessUnit = new MemoryStream();
            _encoder.Encode(frameData, frameW, frameH, _pts, forceKeyFrame, (nal, pts, isKf) =>
            {
                // Annex B start code prefix
                accessUnit.Write([0x00, 0x00, 0x00, 0x01]);
                accessUnit.Write(nal);
            });

            if (accessUnit.Length > 4)
            {
                OnVideoSourceEncodedSample?.Invoke(durationRtpUnits, accessUnit.ToArray());
            }

            _pts += 90000 / _frameRate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ScreenShareVideo: Encode error");
        }
    }

    /// <summary>
    /// Handle a NAL unit from the fused macOS capture (zero-copy path).
    /// Wraps in Annex B format for SendH264Frame.
    /// </summary>
    public void OnNalUnit(byte[] nalData, long pts, bool isKeyframe)
    {
        var durationRtpUnits = (uint)(90000 / _frameRate);
        // Wrap single NAL in Annex B format
        var accessUnit = new byte[4 + nalData.Length];
        accessUnit[0] = 0x00;
        accessUnit[1] = 0x00;
        accessUnit[2] = 0x00;
        accessUnit[3] = 0x01;
        Buffer.BlockCopy(nalData, 0, accessUnit, 4, nalData.Length);
        OnVideoSourceEncodedSample?.Invoke(durationRtpUnits, accessUnit);
    }

    /// <summary>
    /// Forces the next encoded frame to be a keyframe (IDR).
    /// Call after SDP answer is received so new peers can decode immediately.
    /// </summary>
    public void RequestKeyFrame()
    {
        _forceNextKeyFrame = true;
    }

    /// <summary>
    /// Maps a resolution preset string to max width/height dimensions.
    /// </summary>
    public static (int Width, int Height) ResolutionPresetToDimensions(string preset) => preset switch
    {
        "720p" => (1280, 720),
        "1080p" => (1920, 1080),
        "1440p" => (2560, 1440),
        "Native" => (0, 0),
        _ => (1920, 1080),
    };

    internal static void ComputeScaledSize(int srcW, int srcH, int maxWidth, int maxHeight, out int dstW, out int dstH)
    {
        if (maxWidth <= 0 || maxHeight <= 0 || (srcW <= maxWidth && srcH <= maxHeight))
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
    /// Bilinear downscale of an RGBA buffer.
    /// </summary>
    internal static unsafe byte[] BilinearDownscaleRgba(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH * 4];
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

    public void UpdateBitrate(int kbps)
    {
        _bitrateKbps = kbps;
        _encoder?.UpdateBitrate(kbps);
    }

    public void UpdateFps(int fps)
    {
        _frameRate = (uint)fps;
        _encoder?.UpdateFps(fps);
    }

    public void UpdateResolution(string preset)
    {
        var (w, h) = ResolutionPresetToDimensions(preset);
        _maxWidth = w;
        _maxHeight = h;
        // Resolution changes take effect on the next frame (encoder auto-resizes)
    }

    public void Dispose()
    {
        _encoder?.Dispose();
        _encoder = null;
    }
}
