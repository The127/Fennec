using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Fennec.App.Messages;

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

    private DispatcherTimer? _renderTimer;

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
