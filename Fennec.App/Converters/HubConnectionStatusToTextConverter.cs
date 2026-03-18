using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Fennec.Client;

namespace Fennec.App.Converters;

public class ConnectionStatusToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ConnectionStatus status
            ? status switch
            {
                ConnectionStatus.Connected    => "Connected",
                ConnectionStatus.Connecting   => "Connecting...",
                ConnectionStatus.Reconnecting => "Reconnecting...",
                ConnectionStatus.Disconnected => "Disconnected",
                _                                => "Unknown",
            }
            : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
