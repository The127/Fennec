using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Fennec.App.Controls;

public class AudioLevelMeter : Control
{
    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<AudioLevelMeter, double>(nameof(Level));

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    static AudioLevelMeter()
    {
        AffectsRender<AudioLevelMeter>(LevelProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var w = bounds.Width;
        var h = bounds.Height;

        // Background track
        var trackBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        context.DrawRectangle(trackBrush, null, new Rect(0, 0, w, h), h / 2, h / 2);

        // Filled portion
        var level = Math.Clamp(Level, 0, 1);
        if (level > 0.001)
        {
            var fillWidth = w * level;
            var color = level < 0.7
                ? Color.FromRgb(76, 175, 80)   // green
                : level < 0.9
                    ? Color.FromRgb(255, 193, 7) // amber
                    : Color.FromRgb(244, 67, 54); // red
            var fillBrush = new SolidColorBrush(color);
            context.DrawRectangle(fillBrush, null, new Rect(0, 0, fillWidth, h), h / 2, h / 2);
        }
    }
}
