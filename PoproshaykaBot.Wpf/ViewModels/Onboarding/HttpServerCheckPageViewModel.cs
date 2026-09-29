using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using System.Net;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Net.Sockets;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed partial class HttpServerCheckPageViewModel : OnboardingPageViewModelBase
{
    private readonly SettingsManager _settingsManager;
    private readonly KestrelHttpServer _kestrelHttpServer;
    private readonly ILogger<HttpServerCheckPageViewModel> _logger;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _statusSeverity = string.Empty;

    [ObservableProperty]
    private string _detailsText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private bool _isChecking;

    [ObservableProperty]
    private bool _isRetryVisible;

    private OnboardingContext? _context;

    public HttpServerCheckPageViewModel(
        SettingsManager settingsManager,
        KestrelHttpServer kestrelHttpServer,
        ILogger<HttpServerCheckPageViewModel> logger)
    {
        _settingsManager = settingsManager;
        _kestrelHttpServer = kestrelHttpServer;
        _logger = logger;
    }

    public override string PageTitle => "Проверка HTTP сервера";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;
        CanAdvance = false;
        _ = RunCheckAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private async Task RetryAsync()
    {
        await RunCheckAsync();
    }

    private bool CanRetry() => !IsChecking;

    private async Task RunCheckAsync()
    {
        if (IsChecking || _context is null)
        {
            return;
        }

        IsChecking = true;
        IsRetryVisible = false;
        CanAdvance = false;

        try
        {
            ShowStatus("Проверка...", "Info", string.Empty);

            if (!RedirectUriPortResolver.TryResolve(_context.Settings.Twitch.RedirectUri, out var port))
            {
                ShowStatus("Некорректный Redirect URI", "Error", "Вернитесь на предыдущий шаг и исправьте URI.");
                IsRetryVisible = true;
                return;
            }

            _context.Settings.Twitch.HttpServerPort = port;

            var livePort = _settingsManager.Current.Twitch.HttpServerPort;

            if (_kestrelHttpServer.IsRunning && livePort == port)
            {
                ShowStatus($"HTTP сервер уже слушает порт {port}", "Success", "Можно переходить к авторизации.");
                CanAdvance = true;
                return;
            }

            var restartingMessage = _kestrelHttpServer.IsRunning
                ? $"Перезапуск HTTP сервера на порту {port}..."
                : $"Запуск HTTP сервера на порту {port}...";

            ShowStatus(restartingMessage, "Info", string.Empty);

            if (!_kestrelHttpServer.IsRunning && !TryBindPort(port))
            {
                _logger.OnboardingPortUnavailable(port);
                ShowStatus(
                    $"Порт {port} занят другим приложением",
                    "Error",
                    "Закройте конфликтующее приложение или измените Redirect URI на предыдущем шаге.");
                IsRetryVisible = true;
                return;
            }

            var started = await TryRestartServerAsync(port, livePort);

            if (!started)
            {
                ShowStatus(
                    $"Не удалось запустить HTTP сервер на порту {port}",
                    "Error",
                    "Закройте конфликтующее приложение или измените Redirect URI на предыдущем шаге.");
                IsRetryVisible = true;
                return;
            }

            ShowStatus($"HTTP сервер слушает порт {port}", "Success", "Можно переходить к авторизации.");
            CanAdvance = true;
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task<bool> TryRestartServerAsync(int newPort, int previousLivePort)
    {
        try
        {
            if (_kestrelHttpServer.IsRunning)
            {
                await _kestrelHttpServer.StopAsync();
            }

            _settingsManager.UpdateCurrent(settings => settings.Twitch.HttpServerPort = newPort);

            await _kestrelHttpServer.StartAsync();

            _logger.OnboardingHttpServerRestarted(newPort);
            return true;
        }
        catch (Exception exception)
        {
            _logger.OnboardingHttpServerStartFailed(exception, newPort);

            _settingsManager.UpdateCurrent(settings => settings.Twitch.HttpServerPort = previousLivePort);

            try
            {
                if (_kestrelHttpServer.IsRunning)
                {
                    await _kestrelHttpServer.StopAsync();
                }

                await _kestrelHttpServer.StartAsync();
            }
            catch (Exception restoreException)
            {
                _logger.OnboardingHttpServerRestoreFailed(restoreException, previousLivePort);
            }

            return false;
        }
    }

    private void ShowStatus(string status, string severity, string details)
    {
        StatusText = status;
        StatusSeverity = severity;
        DetailsText = details;
    }

    private static bool TryBindPort(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}
