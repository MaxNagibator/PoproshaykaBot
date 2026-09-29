namespace PoproshaykaBot.Wpf.Bootstrap;

public static class AppThemes
{
    public const string LightKey = "light";
    public const string DarkKey = "dark";

    public static string ToKey(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Dark => DarkKey,
            _ => LightKey,
        };
    }

    public static AppTheme FromKey(string? key)
    {
        return key?.Trim().ToLowerInvariant() switch
        {
            DarkKey => AppTheme.Dark,
            _ => AppTheme.Light,
        };
    }

    public static void Register()
    {
        ThemeManager.Register(ThemeManager.DefaultLight);
        ThemeManager.Register(ThemeManager.DefaultDark);
    }
}
