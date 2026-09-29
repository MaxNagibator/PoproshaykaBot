using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Net;
using System.Net.Sockets;

namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

public sealed partial class HttpServerCheckPage : OnboardingPageBase
{
    private OnboardingContext? _context;
    private bool _checkInProgress;

    public HttpServerCheckPage()
    {
        InitializeComponent();
    }

    [Inject]
    public SettingsManager SettingsManager { get; internal init; } = null!;

    [Inject]
    public KestrelHttpServer KestrelHttpServer { get; internal init; } = null!;

    [Inject]
    public ILogger<HttpServerCheckPage> Logger { get; internal init; } = null!;

    public override string PageTitle => "Проверка HTTP сервера";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;
        SetCanAdvance(false);
        _ = RunCheckAsync();
    }

    private async void OnRetryButtonClicked(object? sender, EventArgs e)
    {
        await RunCheckAsync();
    }

    private static bool TryBindPort(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new(IPAddress.Loopback, port);
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

    private async Task RunCheckAsync()
    {
        if (_checkInProgress || _context == null)
        {
            return;
        }

        _checkInProgress = true;
        _retryButton.Visible = false;

        try
        {
            ShowStatus("Проверка...", Color.Blue, "");

            if (!RedirectUriPortResolver.TryResolve(_context.Settings.Twitch.RedirectUri, out var port))
            {
                ShowStatus("Некорректный Redirect URI", Color.Red, "Вернитесь на предыдущий шаг и исправьте URI.");
                _retryButton.Visible = true;
                return;
            }

            _context.Settings.Twitch.HttpServerPort = port;

            var livePort = SettingsManager.Current.Twitch.HttpServerPort;

            if (KestrelHttpServer.IsRunning && livePort == port)
            {
                ShowStatus($"HTTP сервер уже слушает порт {port}",
                    Color.Green,
                    "Можно переходить к авторизации.");

                SetCanAdvance(true);
                return;
            }

            if (KestrelHttpServer.IsRunning)
            {
                ShowStatus($"Перезапуск HTTP сервера на порту {port}...", Color.Blue, "");
            }
            else
            {
                ShowStatus($"Запуск HTTP сервера на порту {port}...", Color.Blue, "");
            }

            await Task.Yield();

            if (!KestrelHttpServer.IsRunning && !TryBindPort(port))
            {
                Logger.LogWarning("Порт {Port} недоступен для биндинга в onboarding", port);
                ShowStatus($"Порт {port} занят другим приложением",
                    Color.Red,
                    "Закройте конфликтующее приложение или измените Redirect URI на предыдущем шаге.");

                _retryButton.Visible = true;
                return;
            }

            var startedSuccessfully = await TryRestartServerAsync(port, livePort);

            if (!startedSuccessfully)
            {
                ShowStatus($"Не удалось запустить HTTP сервер на порту {port}",
                    Color.Red,
                    "Закройте конфликтующее приложение или измените Redirect URI на предыдущем шаге.");

                _retryButton.Visible = true;
                return;
            }

            ShowStatus($"HTTP сервер слушает порт {port}",
                Color.Green,
                "Можно переходить к авторизации.");

            SetCanAdvance(true);
        }
        finally
        {
            _checkInProgress = false;
        }
    }

    private async Task<bool> TryRestartServerAsync(int newPort, int previousLivePort)
    {
        try
        {
            if (KestrelHttpServer.IsRunning)
            {
                await KestrelHttpServer.StopAsync();
            }

            SettingsManager.UpdateCurrent(settings => settings.Twitch.HttpServerPort = newPort);

            await KestrelHttpServer.StartAsync();

            Logger.LogInformation("HTTP сервер перезапущен на порту {Port} в onboarding", newPort);
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Ошибка запуска HTTP сервера на порту {Port} в onboarding", newPort);

            SettingsManager.UpdateCurrent(settings => settings.Twitch.HttpServerPort = previousLivePort);

            try
            {
                if (KestrelHttpServer.IsRunning)
                {
                    await KestrelHttpServer.StopAsync();
                }

                await KestrelHttpServer.StartAsync();
            }
            catch (Exception restoreException)
            {
                Logger.LogError(restoreException, "Не удалось восстановить HTTP сервер на старом порту {Port}", previousLivePort);
            }

            return false;
        }
    }

    private void ShowStatus(string status, Color color, string details)
    {
        _statusLabel.Text = status;
        _statusLabel.ForeColor = color;
        _detailsLabel.Text = details;
    }
}
