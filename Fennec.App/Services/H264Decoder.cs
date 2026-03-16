using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public sealed class H264Decoder : IDisposable
{
    private IntPtr _decoder;
    private readonly ILogger _logger;
    private bool _disposed;

    // Pin delegate to prevent GC during native callbacks
    private readonly NativeVideoInterop.FrameCallback _frameCallbackDelegate;

    // Temporary state during Decode call
    private Action<byte[], int, int>? _currentCallback;

    public H264Decoder(ILogger logger)
    {
        _logger = logger;
        _frameCallbackDelegate = OnFrame;

        _decoder = NativeVideoInterop.fennec_decoder_create();
        if (_decoder == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create native H.264 decoder");
    }

    public void Decode(byte[] nalData, Action<byte[], int, int> onFrame)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(H264Decoder));

        _currentCallback = onFrame;

        var pin = GCHandle.Alloc(nalData, GCHandleType.Pinned);
        try
        {
            NativeVideoInterop.fennec_decoder_decode(
                _decoder, pin.AddrOfPinnedObject(), nalData.Length,
                _frameCallbackDelegate, IntPtr.Zero);
        }
        finally
        {
            pin.Free();
            _currentCallback = null;
        }
    }

    private void OnFrame(IntPtr rgbaData, int width, int height, IntPtr userData)
    {
        if (_currentCallback == null || width <= 0 || height <= 0) return;

        var rgba = new byte[width * height * 4];
        Marshal.Copy(rgbaData, rgba, 0, rgba.Length);
        _currentCallback(rgba, width, height);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_decoder != IntPtr.Zero)
        {
            NativeVideoInterop.fennec_decoder_destroy(_decoder);
            _decoder = IntPtr.Zero;
        }
    }
}
