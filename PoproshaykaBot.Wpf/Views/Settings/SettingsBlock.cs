using System.Windows;

namespace PoproshaykaBot.Wpf.Views.Settings;

public static class SettingsBlock
{
    public const double Reach = 24;

    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key",
        typeof(string),
        typeof(SettingsBlock),
        new PropertyMetadata(null));

    public static string? GetKey(DependencyObject element)
    {
        return (string?)element.GetValue(KeyProperty);
    }

    public static void SetKey(DependencyObject element, string? value)
    {
        element.SetValue(KeyProperty, value);
    }

    public static string? VisibleAt(IReadOnlyList<(string Key, double Top)> blocks, double offset, bool atBottom)
    {
        string? visible = null;

        foreach (var (key, top) in blocks)
        {
            if (visible is null || atBottom || top <= offset + Reach)
            {
                visible = key;
            }
        }

        return visible;
    }
}
