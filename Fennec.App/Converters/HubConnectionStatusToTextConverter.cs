using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Fennec.Client;

namespace Fennec.App.Converters;

public class HubConnectionStatusToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is HubConnectionStatus status
            ? status switch
            {
                HubConnectionStatus.Connected    => "Connected",
                HubConnectionStatus.Connecting   => "Connecting...",
                HubConnectionStatus.Reconnecting => "Reconnecting...",
                HubConnectionStatus.Disconnected => "Disconnected",
                _                                => "Unknown",
            }
            : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
