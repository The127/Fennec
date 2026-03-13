using System.Globalization;
using Avalonia.Data.Converters;

namespace Fennec.App.Converters;

public class BoolToFontSizeConverter : IValueConverter
{
    public double TrueSize { get; set; }
    public double FalseSize { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueSize : FalseSize;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
