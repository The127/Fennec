using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Reads binary RGBA frames from the WaylandPortalClient helper process stdout.
/// Frame protocol: [uint32 width LE][uint32 height LE][width*height*4 RGBA bytes]
/// </summary>
public class WaylandPipeWireCapture : IDisposable
{
    private readonly ILogger<WaylandPipeWireCapture> _logger;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public WaylandPipeWireCapture(ILogger<WaylandPipeWireCapture> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(uint nodeId, Action<byte[], int, int> onFrame, Process helperProcess)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var stdout = helperProcess.StandardOutput.BaseStream;

        _readTask = Task.Factory.StartNew(async () =>
        {
            var header = new byte[8];
            _logger.LogInformation("PipeWire: reading frames for node {NodeId}", nodeId);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Read 8-byte header
                    if (!await ReadExactAsync(stdout, header, 8, ct))
                        break;

                    var width  = (int)BitConverter.ToUInt32(header, 0);
                    var height = (int)BitConverter.ToUInt32(header, 4);

                    if (width <= 0 || height <= 0 || width > 7680 || height > 4320)
                    {
                        _logger.LogWarning("PipeWire: implausible frame size {W}x{H}, skipping", width, height);
                        break;
                    }

                    var frameBytes = width * height * 4;
                    var rgba = new byte[frameBytes];
                    if (!await ReadExactAsync(stdout, rgba, frameBytes, ct))
                        break;

                    try { onFrame(rgba, width, height); }
                    catch (Exception ex) { _logger.LogDebug(ex, "PipeWire: onFrame error"); }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogError(ex, "PipeWire: frame reader error"); }
        }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        return Task.CompletedTask;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buf, int count, CancellationToken ct)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buf, offset, count - offset, ct);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_readTask != null)
        {
            try { await _readTask; } catch { }
        }
        _cts?.Dispose();
        _cts = null;
        _readTask = null;
    }

    public void Dispose() => _cts?.Cancel();
}
