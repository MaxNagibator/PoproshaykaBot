using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Infrastructure.Hosting;

public sealed class BotConnectionManager : IBotConnectionController, IAsyncDisposable
{
    private enum BotCancellationReason
    {
        None = 0,
        UserRequested = 1,
        Shutdown = 2,
    }

    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ForcedStopTimeout = TimeSpan.FromSeconds(6);
    private readonly ITwitchOAuthService _tokenService;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly TwitchChatHandler _twitchChatHandler;
    private readonly AppHost _appHost;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BotConnectionManager> _logger;

    private readonly SemaphoreSlim _stopGate = new(1, 1);
    private readonly object _stopSync = new();

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _activeStopCts;
    private Task? _connectionTask;
    private bool _disposed;
    private bool _shutdownCompleted;
    private BotCancellationReason _cancellationReason = BotCancellationReason.None;

    public BotConnectionManager(
        ITwitchOAuthService tokenService,
        ITargetChannelProvider targetChannelProvider,
        TwitchChatHandler twitchChatHandler,
        AppHost appHost,
        IEventBus eventBus,
        ILogger<BotConnectionManager> logger)
    {
        _tokenService = tokenService;
        _targetChannelProvider = targetChannelProvider;
        _twitchChatHandler = twitchChatHandler;
        _appHost = appHost;
        _eventBus = eventBus;
        _logger = logger;

        _logger.LogDebug("Менеджер подключений бота инициализирован");
    }

    public bool IsBusy => _connectionTask is { IsCompleted: false };

    public Task WaitForConnectionAsync() => _connectionTask ?? Task.CompletedTask;

    public BotLifecyclePhase CurrentPhase { get; private set; } = BotLifecyclePhase.Idle;

    public void StartConnection()
    {
        _logger.LogDebug("Попытка запуска подключения");

        if (IsBusy)
        {
            _logger.LogWarning("Попытка запуска подключения отклонена: процесс уже выполняется");
            throw new InvalidOperationException("Connection is already in progress");
        }

        _cts?.Dispose();
        _cts = new();
        _cancellationReason = BotCancellationReason.None;

        _connectionTask = ConnectAsync(_cts.Token);
    }

    public void CancelConnection()
    {
        CancelConnection(BotCancellationReason.UserRequested);
    }

    private void CancelConnection(BotCancellationReason reason)
    {
        if (_cts == null || _cts.IsCancellationRequested)
        {
            return;
        }

        _cancellationReason = reason;

        if (reason == BotCancellationReason.UserRequested)
        {
            _logger.LogInformation("Пользователь запросил отмену подключения");
        }
        else
        {
            _logger.LogDebug("Отмена незавершённого подключения при остановке");
        }

        _cts.Cancel();
    }

    public async Task StopAsync(BotStopMode mode = BotStopMode.Graceful)
    {
        if (mode == BotStopMode.Forced)
        {
            lock (_stopSync)
            {
                _activeStopCts?.Cancel();
            }
        }

        await _stopGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            SetPhase(BotLifecyclePhase.Disconnecting,
                mode == BotStopMode.Forced ? "принудительная остановка" : "штатная остановка");

            await _eventBus.PublishAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Disconnecting), CancellationToken.None);

            var progressReporter = new Progress<string>(ReportProgress);
            var stopTimeout = mode == BotStopMode.Forced ? ForcedStopTimeout : GracefulStopTimeout;

            try
            {
                using var stopCts = new CancellationTokenSource(stopTimeout);

                lock (_stopSync)
                {
                    _activeStopCts?.Dispose();
                    _activeStopCts = stopCts;
                }

                await _appHost.StopAsync(progressReporter, stopCts.Token);
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(exception, "Остановка компонентов AppHost прервана по таймауту {Timeout} c (режим {Mode})", stopTimeout.TotalSeconds, mode);
                ReportProgress("Остановка компонентов прервана по таймауту");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Произошла ошибка при остановке компонентов AppHost");
                ReportProgress($"Ошибка остановки компонентов: {exception.Message}");
            }
            finally
            {
                lock (_stopSync)
                {
                    _activeStopCts?.Dispose();
                    _activeStopCts = null;
                }
            }

            _twitchChatHandler.Reset();
            _targetChannelProvider.EndSession();
            PublishPhase(BotLifecyclePhase.Disconnected);
        }
        finally
        {
            _stopGate.Release();
        }
    }

    public async Task ShutdownAsync(BotStopMode mode = BotStopMode.Graceful)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        _logger.LogDebug("Инициализация полной остановки бота (ShutdownAsync, режим {Mode})", mode);

        CancelConnection(BotCancellationReason.Shutdown);

        var pendingConnect = _connectionTask;

        if (pendingConnect != null)
        {
            try
            {
                await pendingConnect;
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Незавершённое подключение завершилось с ошибкой при остановке");
            }
        }

        await StopAsync(mode);
        _shutdownCompleted = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Освобождение ресурсов BotConnectionManager (DisposeAsync)");

        try
        {
            await ShutdownAsync(BotStopMode.Forced).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка во время финального ShutdownAsync в DisposeAsync");
        }

        _cts?.Dispose();
        _stopGate.Dispose();
        _disposed = true;
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        PublishPhase(BotLifecyclePhase.Connecting, reason: "запрошено подключение бота");

        var ownsHost = false;

        try
        {
            ReportProgress("Получение токена доступа...");
            _logger.LogDebug("Запрос токена доступа");

            var accessToken = await _tokenService.GetAccessTokenAsync(TwitchOAuthRole.Bot, ct);

            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Не удалось получить токен доступа (токен пуст или null)");
                throw new InvalidOperationException("Токен бота отсутствует или недействителен. Авторизуйте бота в настройках Twitch (вкладка «Бот»).");
            }

            ReportProgress("Запуск компонентов бота...");
            _targetChannelProvider.BeginSession();
            var target = _targetChannelProvider.Current;

            if (target.IsDebugSession)
            {
                ReportProgress(target.IsSendingAllowed
                    ? $"Отладка: подключение к каналу {target.Login}"
                    : $"Отладка: подключение к каналу {target.Login} без отправки сообщений");
            }

            _logger.LogInformation("Запуск AppHost для канала {Channel}", target.Login);

            var progressReporter = new Progress<string>(ReportProgress);
            ownsHost = !_appHost.IsRunning;
            await _appHost.StartAsync(progressReporter, ct);

            _logger.LogDebug("Публикация BotJoinedChannel для канала {Channel}", target.Login);
            await _eventBus.PublishAsync(new BotJoinedChannel(target.Login), ct);

            ReportProgress("Подключение установлено успешно");
            _logger.LogInformation("Процесс подключения бота успешно завершен (канал {Channel})", target.Login);
            PublishPhase(BotLifecyclePhase.Connected);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Процесс подключения бота был отменен");
            await StopUnfinishedHostAsync(ownsHost);
            _targetChannelProvider.EndSession();
            PublishPhase(BotLifecyclePhase.Cancelled, reason: DescribeCancellation(_cancellationReason));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Произошла ошибка в процессе подключения бота");
            ReportProgress($"Ошибка подключения: {exception.Message}");
            await StopUnfinishedHostAsync(ownsHost);
            _targetChannelProvider.EndSession();
            PublishPhase(BotLifecyclePhase.Failed, exception, "ошибка подключения");
        }
    }

    private async Task StopUnfinishedHostAsync(bool ownsHost)
    {
        if (!ownsHost || !_appHost.IsRunning)
        {
            return;
        }

        _logger.LogInformation("Подключение не завершено – останавливаю уже запущенные компоненты бота");

        try
        {
            using var stopCts = new CancellationTokenSource(ForcedStopTimeout);
            await _appHost.StopAsync(new Progress<string>(ReportProgress), stopCts.Token).WaitAsync(ForcedStopTimeout, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось остановить компоненты незавершённого подключения");
        }
    }

    private void ReportProgress(string message)
    {
        _ = _eventBus.PublishAsync(new BotConnectionStatusUpdated(message), CancellationToken.None);
        _logger.LogInformation("{Message}", message);
    }

    private static string DescribeCancellation(BotCancellationReason reason)
    {
        return reason switch
        {
            BotCancellationReason.UserRequested => "отмена по запросу пользователя",
            BotCancellationReason.Shutdown => "отмена при завершении работы",
            _ => "отмена без явного запроса",
        };
    }

    private void PublishPhase(BotLifecyclePhase phase, Exception? exception = null, string? reason = null)
    {
        SetPhase(phase, reason);
        _ = _eventBus.PublishAsync(new BotLifecyclePhaseChanged(phase, exception), CancellationToken.None);
    }

    private void SetPhase(BotLifecyclePhase phase, string? reason)
    {
        var previous = CurrentPhase;
        CurrentPhase = phase;

        if (string.IsNullOrEmpty(reason))
        {
            _logger.LogInformation("Фаза бота: {PreviousPhase} → {Phase}", previous, phase);
        }
        else
        {
            _logger.LogInformation("Фаза бота: {PreviousPhase} → {Phase} (причина: {Reason})", previous, phase, reason);
        }
    }
}
