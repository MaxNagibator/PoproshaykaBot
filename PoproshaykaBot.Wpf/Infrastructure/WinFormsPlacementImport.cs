using PoproshaykaBot.Core.Settings.Ui;
using System.Runtime.InteropServices;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure;

public static class WinFormsPlacementImport
{
    private const double DefaultDpi = 96.0;

    public static Rect ToDeviceIndependent(MainWindowSettings saved, double scale)
    {
        ArgumentNullException.ThrowIfNull(saved);

        var divisor = scale > 0 ? scale : 1.0;

        return new(saved.X / divisor, saved.Y / divisor, saved.Width / divisor, saved.Height / divisor);
    }

    public static double GetSystemScale()
    {
        var dpi = GetDpiForSystem();

        return dpi > 0 ? dpi / DefaultDpi : 1.0;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
