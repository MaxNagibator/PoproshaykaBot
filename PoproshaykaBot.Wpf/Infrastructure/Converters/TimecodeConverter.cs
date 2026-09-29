using System.Globalization;
using System.Windows.Data;

namespace PoproshaykaBot.Wpf.Infrastructure.Converters;

public sealed class TimecodeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var span = ResolveSpan(value);

        if (span is null)
        {
            return string.Empty;
        }

        var value2 = span.Value < TimeSpan.Zero ? TimeSpan.Zero : span.Value;

        return $"{(int)value2.TotalHours:00}:{value2.Minutes:00}:{value2.Seconds:00}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static TimeSpan? ResolveSpan(object? value)
    {
        return value switch
        {
            TimeSpan span => span,
            double seconds => TimeSpan.FromSeconds(seconds),
            float seconds => TimeSpan.FromSeconds(seconds),
            int seconds => TimeSpan.FromSeconds(seconds),
            long seconds => TimeSpan.FromSeconds(seconds),
            string text when double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => TimeSpan.FromSeconds(parsed),
            string text when TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }
}
