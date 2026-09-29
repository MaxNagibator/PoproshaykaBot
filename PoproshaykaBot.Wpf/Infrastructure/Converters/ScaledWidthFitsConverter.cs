using System.Globalization;
using System.Windows.Data;

namespace PoproshaykaBot.Wpf.Infrastructure.Converters;

public sealed class ScaledWidthFitsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width || !double.IsFinite(width))
        {
            return false;
        }

        var threshold = parameter switch
        {
            double number => number,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0d,
        };

        return width >= threshold * FontScaleManager.Current;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
