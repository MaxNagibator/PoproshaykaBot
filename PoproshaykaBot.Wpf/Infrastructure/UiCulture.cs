using System.Globalization;

namespace PoproshaykaBot.Wpf.Infrastructure;

public static class UiCulture
{
    public static CultureInfo Russian { get; } = CultureInfo.GetCultureInfo("ru-RU");
}
