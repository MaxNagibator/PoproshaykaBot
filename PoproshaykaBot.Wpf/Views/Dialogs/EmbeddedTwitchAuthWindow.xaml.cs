using Microsoft.Web.WebView2.Core;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Views.Dialogs;

public partial class EmbeddedTwitchAuthWindow : Window
{
    private const int MonitorDefaultToNearest = 2;

    private readonly EmbeddedTwitchAuthDialogViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly WebViewThemeBackground _themeBackground;

    private bool _initialized;
    private bool _tornDown;

    public EmbeddedTwitchAuthWindow(EmbeddedTwitchAuthDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        WindowChromeTheming.Attach(this);

        _viewModel.NavigateRequested += OnNavigateRequested;
        _viewModel.CloseRequested += OnCloseRequested;

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        Dispatcher.ShutdownStarted += OnShutdownStarted;
        _themeBackground = WebViewThemeBackground.Attach(WebView);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyOwnerMonitorBounds();
    }

    private void ApplyOwnerMonitorBounds()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        var anchor = Owner is null ? IntPtr.Zero : new WindowInteropHelper(Owner).Handle;
        var monitor = MonitorFromWindow(anchor == IntPtr.Zero ? source.Handle : anchor, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var transform = source.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var work = transform.Transform(new Vector(info.WorkRight - info.WorkLeft, info.WorkBottom - info.WorkTop));

        if (work.X > 0)
        {
            var maxWidth = Math.Floor(work.X);
            SetCurrentValue(MaxWidthProperty, maxWidth);
            SetCurrentValue(MinWidthProperty, Math.Min(MinWidth, maxWidth));
        }

        if (work.Y > 0)
        {
            var maxHeight = Math.Floor(work.Y);
            SetCurrentValue(MaxHeightProperty, maxHeight);
            SetCurrentValue(MinHeightProperty, Math.Min(MinHeight, maxHeight));
        }
    }

    private static void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (sender is not CoreWebView2 core)
        {
            return;
        }

        e.NewWindow = core;
        e.Handled = true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _tornDown)
        {
            return;
        }

        _initialized = true;

        if (!await TryInitializeWebViewAsync())
        {
            return;
        }

        await _viewModel.RunFlowAsync();
    }

    private async Task<bool> TryInitializeWebViewAsync()
    {
        try
        {
            var environment = await WebView2EnvironmentFactory.CreateAsync(
                _viewModel.UserDataFolder,
                _viewModel.Logger,
                _lifetime.Token);

            if (_tornDown)
            {
                return false;
            }

            await WebView.EnsureCoreWebView2Async(environment);

            if (_tornDown)
            {
                return false;
            }

            WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (WebView2RuntimeNotFoundException exception)
        {
            _viewModel.Logger.EmbeddedAuthRuntimeMissing(exception);
            _viewModel.ShowRuntimeMissing();

            return false;
        }
        catch (Exception exception)
        {
            _viewModel.Logger.EmbeddedAuthWebViewFailed(exception);
            _viewModel.ShowInitializationFailure();

            return false;
        }
    }

    private void OnNavigateRequested(string authUrl)
    {
        if (_tornDown || WebView.CoreWebView2 is null)
        {
            return;
        }

        WebView.CoreWebView2.Navigate(authUrl);
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (_tornDown)
        {
            return;
        }

        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.Cancel();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        TearDown();
    }

    private void OnShutdownStarted(object? sender, EventArgs e)
    {
        TearDown();
    }

    private void TearDown()
    {
        if (_tornDown)
        {
            return;
        }

        _tornDown = true;

        Dispatcher.ShutdownStarted -= OnShutdownStarted;
        Loaded -= OnLoaded;
        Closing -= OnClosing;
        Closed -= OnClosed;

        _viewModel.NavigateRequested -= OnNavigateRequested;
        _viewModel.CloseRequested -= OnCloseRequested;

        _themeBackground.Detach();
        _lifetime.Cancel();

        if (WebView.CoreWebView2 is { } core)
        {
            core.NewWindowRequested -= OnNewWindowRequested;
        }

        WebView.Dispose();
        _lifetime.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public int MonitorLeft;
        public int MonitorTop;
        public int MonitorRight;
        public int MonitorBottom;
        public int WorkLeft;
        public int WorkTop;
        public int WorkRight;
        public int WorkBottom;
        public int Flags;
    }
}
