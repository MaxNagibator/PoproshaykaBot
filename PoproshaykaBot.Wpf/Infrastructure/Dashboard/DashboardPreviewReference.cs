using System.Globalization;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public sealed record DashboardPreviewReference(string Label, Size Window)
{
    public const double TitleBarHeight = 37;
    public const double StatusBarHeight = 30;
    public const double NavWidthExpanded = 220;
    public const double NavWidthCollapsed = 56;

    public static readonly Size DefaultWindow = new(1100, 720);

    public static readonly Size MinWindow = new(1024, 640);

    public Size ContentArea(bool navCollapsed)
    {
        return new(
            Math.Max(1, Window.Width - (navCollapsed ? NavWidthCollapsed : NavWidthExpanded)),
            Math.Max(1, Window.Height - TitleBarHeight - StatusBarHeight));
    }

    public string Describe(double scale)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            "Эталон {0:0}×{1:0}, масштаб {2:0} %",
            Window.Width,
            Window.Height,
            scale * 100);
    }

    public static DashboardPreviewReference Window1024 { get; } = new("1024 × 640", new(1024, 640));

    public static DashboardPreviewReference Window1920 { get; } = new("1920 × 1080", new(1920, 1080));

    public static DashboardPreviewReference Current(Size window)
    {
        return new("Как окно сейчас", Normalize(window));
    }

    public static Size Normalize(Size window)
    {
        if (window is not { Width: > 0, Height: > 0 } || !double.IsFinite(window.Width) || !double.IsFinite(window.Height))
        {
            return DefaultWindow;
        }

        return new(Math.Max(window.Width, MinWindow.Width), Math.Max(window.Height, MinWindow.Height));
    }
}
