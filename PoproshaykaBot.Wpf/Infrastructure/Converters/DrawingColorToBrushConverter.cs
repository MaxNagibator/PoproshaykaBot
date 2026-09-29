using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;

namespace PoproshaykaBot.Wpf.Infrastructure.Converters;

public sealed class DrawingColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DrawingColor color)
        {
            return Brushes.Transparent;
        }

        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            SolidColorBrush brush => DrawingColor.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B),
            Color color => DrawingColor.FromArgb(color.A, color.R, color.G, color.B),
            _ => DrawingColor.Transparent,
        };
    }
}
