using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public static class DashboardTileSlot
{
    public static readonly DependencyProperty FillsProperty = DependencyProperty.RegisterAttached(
        "Fills",
        typeof(bool),
        typeof(DashboardTileSlot),
        new FrameworkPropertyMetadata(false));

    public static void SetFills(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.SetValue(FillsProperty, value);
    }

    public static bool GetFills(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return (bool)element.GetValue(FillsProperty);
    }
}
