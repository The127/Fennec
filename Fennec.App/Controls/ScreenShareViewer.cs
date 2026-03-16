using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Fennec.App.Messages;
using Fennec.App.Services;

namespace Fennec.App.Controls;

/// <summary>
/// Custom control that renders a screen share frame (WriteableBitmap) with a cursor overlay.
/// </summary>
public class ScreenShareViewer : Control
{
    public static readonly StyledProperty<WriteableBitmap?> SourceProperty =
        AvaloniaProperty.Register<ScreenShareViewer, WriteableBitmap?>(nameof(Source));

    public static readonly StyledProperty<double> CursorXProperty =
        AvaloniaProperty.Register<ScreenShareViewer, double>(nameof(CursorX));

    public static readonly StyledProperty<double> CursorYProperty =
        AvaloniaProperty.Register<ScreenShareViewer, double>(nameof(CursorY));

    public static readonly StyledProperty<CursorType> CursorShapeProperty =
        AvaloniaProperty.Register<ScreenShareViewer, CursorType>(nameof(CursorShape));

    public static readonly StyledProperty<bool> ShowDebugOverlayProperty =
        AvaloniaProperty.Register<ScreenShareViewer, bool>(nameof(ShowDebugOverlay));

    public static readonly StyledProperty<ScreenShareMetrics?> DebugMetricsProperty =
        AvaloniaProperty.Register<ScreenShareViewer, ScreenShareMetrics?>(nameof(DebugMetrics));

    private DispatcherTimer? _renderTimer;

    // Render FPS tracking
    private int _renderFrameCount;
    private long _renderFpsTimestamp = Stopwatch.GetTimestamp();

    private static readonly Typeface MonoTypeface = new("Cascadia Mono, Consolas, Menlo, monospace");

    public WriteableBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double CursorX
    {
        get => GetValue(CursorXProperty);
        set => SetValue(CursorXProperty, value);
    }

    public double CursorY
    {
        get => GetValue(CursorYProperty);
        set => SetValue(CursorYProperty, value);
    }

    public CursorType CursorShape
    {
        get => GetValue(CursorShapeProperty);
        set => SetValue(CursorShapeProperty, value);
    }

    public bool ShowDebugOverlay
    {
        get => GetValue(ShowDebugOverlayProperty);
        set => SetValue(ShowDebugOverlayProperty, value);
    }

    public ScreenShareMetrics? DebugMetrics
    {
        get => GetValue(DebugMetricsProperty);
        set => SetValue(DebugMetricsProperty, value);
    }

    static ScreenShareViewer()
    {
        AffectsRender<ScreenShareViewer>(SourceProperty, CursorXProperty, CursorYProperty, CursorShapeProperty);
        SourceProperty.Changed.AddClassHandler<ScreenShareViewer>((viewer, _) => viewer.OnSourceChanged());
    }

    private void OnSourceChanged()
    {
        if (Source is not null)
        {
            if (_renderTimer is null)
            {
                _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _renderTimer.Tick += (_, _) => InvalidateVisual();
                _renderTimer.Start();
            }
        }
        else
        {
            _renderTimer?.Stop();
            _renderTimer = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var source = Source;
        if (source is null)
            return;

        var bounds = Bounds;
        var imageWidth = source.PixelSize.Width;
        var imageHeight = source.PixelSize.Height;

        if (imageWidth == 0 || imageHeight == 0)
            return;

        // Calculate aspect-fit rectangle
        var scaleX = bounds.Width / imageWidth;
        var scaleY = bounds.Height / imageHeight;
        var scale = Math.Min(scaleX, scaleY);
        var renderWidth = imageWidth * scale;
        var renderHeight = imageHeight * scale;
        var offsetX = (bounds.Width - renderWidth) / 2;
        var offsetY = (bounds.Height - renderHeight) / 2;

        var destRect = new Rect(offsetX, offsetY, renderWidth, renderHeight);
        context.DrawImage(source, destRect);

        // Draw cursor overlay
        var cursorPixelX = offsetX + CursorX * renderWidth;
        var cursorPixelY = offsetY + CursorY * renderHeight;

        // Draw a small cursor indicator
        var cursorSize = 12;
        var cursorBrush = Brushes.White;
        var cursorPen = new Pen(Brushes.Black, 1.5);

        // Simple arrow cursor shape
        var cursorGeometry = GetCursorGeometry(CursorShape, cursorPixelX, cursorPixelY, cursorSize);
        if (cursorGeometry != null)
        {
            context.DrawGeometry(cursorBrush, cursorPen, cursorGeometry);
        }

        // Debug overlay
        var metrics = DebugMetrics;
        if (ShowDebugOverlay && metrics != null)
        {
            // Track render FPS
            _renderFrameCount++;
            var elapsed = Stopwatch.GetElapsedTime(_renderFpsTimestamp);
            if (elapsed.TotalSeconds >= 1.0)
            {
                metrics.RenderFps.Add(_renderFrameCount / elapsed.TotalSeconds);
                _renderFrameCount = 0;
                _renderFpsTimestamp = Stopwatch.GetTimestamp();
            }

            RenderDebugOverlay(context, metrics);
        }
    }

    private void RenderDebugOverlay(DrawingContext context, ScreenShareMetrics metrics)
    {
        var rows = new List<(string Label, MetricSeries Series, string Unit, IBrush Color)>();

        if (metrics.IsSender)
        {
            rows.Add(("CAPTURE FPS", metrics.CaptureFps, "", Brushes.LimeGreen));
            rows.Add(("ENCODE ms", metrics.EncodeTimeMs, "ms", Brushes.Cyan));
            rows.Add(("NAL KB", metrics.EncodedSizeKb, "KB", Brushes.Cyan));
        }
        else
        {
            rows.Add(("RECEIVE FPS", metrics.ReceiveFps, "", Brushes.LimeGreen));
            rows.Add(("DECODE ms", metrics.DecodeTimeMs, "ms", Brushes.Cyan));
            rows.Add(("SCALE ms", metrics.DownscaleTimeMs, "ms", Brushes.Cyan));
        }

        rows.Add(("UI FPS", metrics.RenderFps, "", Brushes.LimeGreen));
        rows.Add(("FRAME LAG", metrics.FrameLagMs, "ms", Brushes.Yellow));
        rows.Add(("QUEUE", metrics.QueueDepth, "", Brushes.Orange));
        rows.Add(("COPY ms", metrics.BitmapCopyTimeMs, "ms", Brushes.Cyan));

        const double rowHeight = 18;
        const double padding = 8;
        const double labelWidth = 100;
        const double sparkWidth = 80;
        const double valueWidth = 70;
        const double totalWidth = labelWidth + sparkWidth + valueWidth + padding * 2;
        var totalHeight = rows.Count * rowHeight + padding * 2;

        // Extra line for capture resolution
        if (metrics.IsSender && (metrics.CaptureWidth > 0 || metrics.CaptureHeight > 0))
            totalHeight += rowHeight;

        var overlayRect = new Rect(8, 8, totalWidth, totalHeight);

        // Semi-transparent background
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            null,
            overlayRect,
            4, 4);

        var y = 8 + padding;
        foreach (var (label, series, unit, color) in rows)
        {
            var labelX = 8 + padding;
            var sparkX = labelX + labelWidth;
            var valueX = sparkX + sparkWidth + 4;

            // Label
            var labelText = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, MonoTypeface, 11, Brushes.White);
            context.DrawText(labelText, new Point(labelX, y));

            // Sparkline
            var sparkRect = new Rect(sparkX, y + 2, sparkWidth, rowHeight - 6);
            DrawSparkline(context, series, sparkRect, color);

            // Value
            var latest = series.Latest;
            var valueStr = unit.Length > 0
                ? $"{latest:F1}{unit}"
                : latest >= 10 ? $"{latest:F0}" : $"{latest:F1}";
            var valueText = new FormattedText(valueStr, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, MonoTypeface, 11, Brushes.White);
            context.DrawText(valueText, new Point(valueX, y));

            y += rowHeight;
        }

        // Capture resolution line
        if (metrics.IsSender && (metrics.CaptureWidth > 0 || metrics.CaptureHeight > 0))
        {
            var resText = new FormattedText($"{metrics.CaptureWidth}x{metrics.CaptureHeight}",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MonoTypeface, 11, Brushes.Gray);
            context.DrawText(resText, new Point(8 + padding + labelWidth + sparkWidth + 4 - resText.Width, y));
        }
    }

    private static void DrawSparkline(DrawingContext ctx, MetricSeries series, Rect area, IBrush color)
    {
        var count = series.Count;
        if (count < 2) return;

        var max = Math.Max(series.Max, 0.001);
        var step = area.Width / (count - 1);
        var pen = new Pen(color, 1.5);

        var prev = new Point(area.X, area.Bottom - (series[0] / max) * area.Height);
        for (int i = 1; i < count; i++)
        {
            var x = area.X + i * step;
            var y = area.Bottom - (series[i] / max) * area.Height;
            var pt = new Point(x, y);
            ctx.DrawLine(pen, prev, pt);
            prev = pt;
        }
    }

    private static Geometry? GetCursorGeometry(CursorType type, double x, double y, double size)
    {
        // For all cursor types, draw a simple arrow-like shape at the position.
        // The cursor type information can be used to set the actual system cursor
        // on the viewer window if needed.
        var fig = new PathFigure
        {
            StartPoint = new Point(x, y),
            IsClosed = true,
            IsFilled = true,
        };

        fig.Segments = new PathSegments
        {
            new LineSegment { Point = new Point(x, y + size) },
            new LineSegment { Point = new Point(x + size * 0.35, y + size * 0.75) },
            new LineSegment { Point = new Point(x + size * 0.7, y + size * 0.7) },
        };

        var geometry = new PathGeometry();
        geometry.Figures.Add(fig);
        return geometry;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }
}
