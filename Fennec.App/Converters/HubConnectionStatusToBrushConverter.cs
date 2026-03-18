using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Fennec.Client;

namespace Fennec.App.Converters;

public class HubConnectionStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is HubConnectionStatus status
            ? status switch
            {
                HubConnectionStatus.Connected    => new SolidColorBrush(Color.Parse("#4CAF50")),
                HubConnectionStatus.Connecting   => new SolidColorBrush(Color.Parse("#FFC107")),
                HubConnectionStatus.Reconnecting => new SolidColorBrush(Color.Parse("#FFC107")),
                HubConnectionStatus.Disconnected => new SolidColorBrush(Color.Parse("#F44336")),
                _                                => new SolidColorBrush(Color.Parse("#808080")),
            }
            : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
