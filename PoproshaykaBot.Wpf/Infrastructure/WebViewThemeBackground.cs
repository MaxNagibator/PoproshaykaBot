using KeepShell.Bootstrap;
using Microsoft.Web.WebView2.Wpf;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Windows;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class WebViewThemeBackground
{
    private WebView2? _webView;

    private WebViewThemeBackground(WebView2 webView)
    {
        _webView = webView;
        webView.Loaded += OnLoaded;
        ThemeManager.Changed += OnThemeChanged;
        Apply();
    }

    public static WebViewThemeBackground Attach(WebView2 webView)
    {
        ArgumentNullException.ThrowIfNull(webView);

        return new(webView);
    }

    public void Detach()
    {
        if (_webView is null)
        {
            return;
        }

        _webView.Loaded -= OnLoaded;
        ThemeManager.Changed -= OnThemeChanged;
        _webView = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Apply();
    }

    private void OnThemeChanged(object? sender, ThemeDefinition theme)
    {
        Apply();
    }

    private void Apply()
    {
        if (_webView?.TryFindResource(ThemeKeys.BgSurface) is not SolidColorBrush brush)
        {
            return;
        }

        var color = brush.Color;
        _webView.DefaultBackgroundColor = DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
    }
}
