namespace Fennec.App.Services;

public class MetricSeries
{
    private readonly double[] _buffer;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    public MetricSeries(int capacity = 60)
    {
        _buffer = new double[capacity];
    }

    public void Add(double value)
    {
        lock (_lock)
        {
            _buffer[_head] = value;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public double Latest
    {
        get
        {
            lock (_lock)
            {
                if (_count == 0) return 0;
                return _buffer[(_head - 1 + _buffer.Length) % _buffer.Length];
            }
        }
    }

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    /// <summary>Index 0 = oldest sample.</summary>
    public double this[int i]
    {
        get
        {
            lock (_lock)
            {
                if (i < 0 || i >= _count) return 0;
                var start = (_head - _count + _buffer.Length) % _buffer.Length;
                return _buffer[(start + i) % _buffer.Length];
            }
        }
    }

    public double Max
    {
        get
        {
            lock (_lock)
            {
                if (_count == 0) return 0;
                double max = double.MinValue;
                var start = (_head - _count + _buffer.Length) % _buffer.Length;
                for (int i = 0; i < _count; i++)
                    max = Math.Max(max, _buffer[(start + i) % _buffer.Length]);
                return max;
            }
        }
    }
}

public class ScreenShareMetrics
{
    // Sender
    public MetricSeries CaptureFps { get; } = new();
    public MetricSeries EncodeTimeMs { get; } = new();
    public MetricSeries EncodedSizeKb { get; } = new();
    public MetricSeries SentFps { get; } = new();
    public int CaptureWidth { get; set; }
    public int CaptureHeight { get; set; }
    public long FramesEncoded { get; set; }
    public long FramesSent { get; set; }
    public long FramesDropped { get; set; }
    public string? EncoderName { get; set; }
    public int ViewerCount { get; set; }

    // Receiver
    public MetricSeries TransportFps { get; } = new();
    public MetricSeries ReceiveFps { get; } = new();
    public MetricSeries DecodeTimeMs { get; } = new();
    public MetricSeries DownscaleTimeMs { get; } = new();
    public long FramesReceived { get; set; }
    public long FramesDecoded { get; set; }

    // UI (both)
    public MetricSeries RenderFps { get; } = new();
    public MetricSeries BitmapCopyTimeMs { get; } = new();
    public MetricSeries QueueDepth { get; } = new();
    public MetricSeries FrameLagMs { get; } = new();

    public bool IsSender { get; set; }
}
