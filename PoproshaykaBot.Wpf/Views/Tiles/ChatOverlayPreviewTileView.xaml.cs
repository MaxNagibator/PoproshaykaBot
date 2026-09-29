using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class ChatOverlayPreviewTileView : UserControl, IView<ChatOverlayPreviewTileViewModel>
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly WebViewThemeBackground _themeBackground;

    private bool _initialized;
    private bool _disposed;
    private ChatOverlayPreviewTileViewModel? _viewModel;

    public ChatOverlayPreviewTileView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Dispatcher.ShutdownStarted += OnShutdownStarted;
        _themeBackground = WebViewThemeBackground.Attach(WebView);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _disposed)
        {
            return;
        }

        if (DataContext is not ChatOverlayPreviewTileViewModel viewModel)
        {
            return;
        }

        _initialized = true;
        _viewModel = viewModel;

        await InitializeWebViewAsync(viewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!IsLoaded)
            {
                Release();
            }
        });
    }

    private void OnShutdownStarted(object? sender, EventArgs e)
    {
        Release();
    }

    private void Release()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        Dispatcher.ShutdownStarted -= OnShutdownStarted;
        _themeBackground.Detach();
        _lifetime.Cancel();

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        WebView.Dispose();
        _lifetime.Dispose();
    }

    private async Task InitializeWebViewAsync(ChatOverlayPreviewTileViewModel viewModel)
    {
        if (App.IsGallery)
        {
            return;
        }

        try
        {
            var environment = await WebView2EnvironmentFactory.CreateAsync(
                viewModel.UserDataFolder,
                viewModel.Logger,
                _lifetime.Token);

            if (_disposed)
            {
                return;
            }

            await WebView.EnsureCoreWebView2Async(environment);

            if (_disposed)
            {
                return;
            }

            WebView.CoreWebView2.Navigate(viewModel.CurrentUrl);
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebView2RuntimeNotFoundException exception)
        {
            viewModel.Logger.LogError(exception, "WebView2 Runtime не установлен");
            viewModel.ShowRuntimeMissingError();
        }
        catch (Exception exception)
        {
            viewModel.Logger.LogError(exception, "Ошибка инициализации WebView2 превью оверлея");
            viewModel.ShowGenericError();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatOverlayPreviewTileViewModel.CurrentUrl))
        {
            return;
        }

        if (_disposed || WebView.CoreWebView2 is null || _viewModel is null)
        {
            return;
        }

        WebView.CoreWebView2.Navigate(_viewModel.CurrentUrl);
    }
}
