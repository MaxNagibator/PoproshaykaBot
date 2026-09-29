using System.Linq;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure;

public static class DialogOwner
{
    public static Window? Resolve()
    {
        var windows = Application.Current?.Windows.OfType<Window>().ToList();

        if (windows is null)
        {
            return null;
        }

        return windows.FirstOrDefault(window => window.IsActive)
               ?? windows.LastOrDefault(window => window.IsVisible)
               ?? Application.Current?.MainWindow;
    }
}
