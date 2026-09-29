using System.Windows;

namespace PoproshaykaBot.Wpf.Bootstrap;

public static class AppScaledSizes
{
    public const string ShortFieldWidthKey = "Setting.ShortFieldWidth";
    public const double ShortFieldWidth = 240;

    private static bool _subscribed;

    public static void Register(Application application)
    {
        if (!_subscribed)
        {
            _subscribed = true;
            FontScaleManager.Changed += (_, scale) => Apply(application, scale);
        }

        Apply(application, FontScaleManager.Current);
    }

    private static void Apply(Application application, double scale)
    {
        application.Resources[ShortFieldWidthKey] = Math.Round(ShortFieldWidth * scale);
    }
}
