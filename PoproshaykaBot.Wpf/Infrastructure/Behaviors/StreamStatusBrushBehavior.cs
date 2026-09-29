using PoproshaykaBot.Wpf.Bootstrap;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace PoproshaykaBot.Wpf.Infrastructure.Behaviors;

public static class StreamStatusBrushBehavior
{
    private const string NeutralKey = ThemeKeys.FgMuted;

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterAttached(
        "Foreground",
        typeof(object),
        typeof(StreamStatusBrushBehavior),
        new PropertyMetadata(null, OnForegroundChanged));

    public static readonly DependencyProperty FillProperty = DependencyProperty.RegisterAttached(
        "Fill",
        typeof(object),
        typeof(StreamStatusBrushBehavior),
        new PropertyMetadata(null, OnFillChanged));

    public static object? GetForeground(DependencyObject element)
    {
        return element.GetValue(ForegroundProperty);
    }

    public static void SetForeground(DependencyObject element, object? value)
    {
        element.SetValue(ForegroundProperty, value);
    }

    public static object? GetFill(DependencyObject element)
    {
        return element.GetValue(FillProperty);
    }

    public static void SetFill(DependencyObject element, object? value)
    {
        element.SetValue(FillProperty, value);
    }

    private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var target = d switch
        {
            Control => Control.ForegroundProperty,
            TextBlock => TextBlock.ForegroundProperty,
            _ => TextElement.ForegroundProperty,
        };

        Apply(d, target, TextKey(ResolveKey(e.NewValue)));
    }

    private static string TextKey(string? key)
    {
        return key switch
        {
            ThemeKeys.StateSuccess => ThemeKeys.StateSuccessText,
            ThemeKeys.StateError => ThemeKeys.StateErrorText,
            _ => NeutralKey,
        };
    }

    private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Apply(d, Shape.FillProperty, ResolveKey(e.NewValue) ?? NeutralKey);
    }

    private static void Apply(DependencyObject element, DependencyProperty property, string key)
    {
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.SetResourceReference(property, key);
        }
    }

    private static string? ResolveKey(object? value)
    {
        if (value is bool flag)
        {
            return flag ? ThemeKeys.StateSuccess : null;
        }

        var name = (value as Enum)?.ToString() ?? value as string;

        return name?.ToLowerInvariant() switch
        {
            "online" or "live" or "connected" or "active" or "running" or "started" or "success" or "ok" => ThemeKeys.StateSuccess,
            "error" or "failed" or "fault" or "faulted" or "stuck" => ThemeKeys.StateError,
            _ => null,
        };
    }
}
