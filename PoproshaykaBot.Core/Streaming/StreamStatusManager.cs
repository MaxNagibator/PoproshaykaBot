using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Json;

namespace PoproshaykaBot.Core.Streaming;

public class StreamStatusManager : IStreamStatus, IStreamHostedComponent, IAsyncDisposable
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RefreshGateDrainTimeout = TimeSpan.FromSeconds(2);

    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly ITwitchHelixClient _helix;
    private readonly IBroadcasterIdProvider _broadcasterIdProvider;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly SettingsManager _settingsManager;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StreamStatusManager> _logger;

    private readonly StreamStateMachine _state = new();
    private readonly StreamMetadataRetryLoop _metadataRetryLoop;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly EventSubSubscriptionLedger _ledger;
    private readonly EventSubSubscriptionRetry _retry;

    private IDisposable? _channelUpdatedSubscription;
    private CancellationTokenSource? _runCts;
    private bool _subscribed;
    private bool _disposed;

    public StreamStatusManager(
        [FromKeyedServices(TwitchEndpoints.EventSubBotSession)]
        ITwitchEventSubClient eventSubClient,
        [FromKeyedServices(TwitchEndpoints.HelixBotClient)]
        ITwitchHelixClient helix,
        IBroadcasterIdProvider broadcasterIdProvider,
        ITargetChannelProvider targetChannelProvider,
        SettingsManager settingsManager,
        IEventBus eventBus,
        TimeProvider timeProvider,
        ILogger<StreamStatusManager> logger)
    {
        _eventSubClient = eventSubClient;
        _helix = helix;
        _broadcasterIdProvider = broadcasterIdProvider;
        _targetChannelProvider = targetChannelProvider;
        _settingsManager = settingsManager;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
        _logger = logger;
        _metadataRetryLoop = new(FetchMetadataAttemptAsync, IsCurrentlyOnline, logger);
        _ledger = new(helix, logger);
        _retry = new(helix, eventSubClient, timeProvider, logger);
    }

    public StreamStatus CurrentStatus => _state.CurrentStatus;

    public StreamInfo? CurrentStream => _state.CurrentStream;

    public string Name => "Отслеживание статуса стрима";

    public int StartOrder => 256;

    private TimeSpan StuckOnlineThreshold =>
        TimeSpan.FromSeconds(_settingsManager.Current.Twitch.Infrastructure.StreamStuckOnlineThresholdSeconds);

    public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_subscribed)
        {
            _logger.LogDebug("StreamStatusManager.StartAsync проигнорирован: подписки уже установлены");
            return Task.CompletedTask;
        }

        _runCts = new();

        _eventSubClient.OnSessionWelcome += OnSessionWelcomeAsync;
        _eventSubClient.OnNotification += OnNotificationAsync;
        _eventSubClient.OnSessionReconnect += OnSessionReconnectAsync;
        _eventSubClient.OnRevocation += OnRevocationAsync;
        _eventSubClient.OnDisconnected += OnDisconnectedAsync;

        _channelUpdatedSubscription = _eventBus.Subscribe<ChannelUpdated>(OnChannelUpdated);
        _subscribed = true;

        _logger.LogInformation("StreamStatusManager: подписки на EventSub установлены");
        return Task.CompletedTask;
    }

    public async Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!_subscribed)
        {
            _logger.LogDebug("StreamStatusManager.StopAsync проигнорирован: подписки уже сняты");
            return;
        }

        _eventSubClient.OnSessionWelcome -= OnSessionWelcomeAsync;
        _eventSubClient.OnNotification -= OnNotificationAsync;
        _eventSubClient.OnSessionReconnect -= OnSessionReconnectAsync;
        _eventSubClient.OnRevocation -= OnRevocationAsync;
        _eventSubClient.OnDisconnected -= OnDisconnectedAsync;

        _channelUpdatedSubscription?.Dispose();
        _channelUpdatedSubscription = null;
        _subscribed = false;

        if (_runCts != null)
        {
            await _runCts.CancelAsync().ConfigureAwait(false);
        }

        await _metadataRetryLoop.CancelAndDrainAsync().ConfigureAwait(false);

        _runCts?.Dispose();
        _runCts = null;

        _state.ResetToUnknown();

        await _retry.CancelAndDrainAsync(cancellationToken).ConfigureAwait(false);
        await _ledger.CloseAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("StreamStatusManager: подписки на EventSub сняты, состояние сброшено в Unknown");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Уничтожение ресурсов StreamStatusManager");
        _disposed = true;

        await _disposeCts.CancelAsync();

        if (_runCts != null)
        {
            await _runCts.CancelAsync();
        }

        _retry.Cancel();

        await _metadataRetryLoop.DisposeAsync().ConfigureAwait(false);

        if (await _refreshGate.WaitAsync(RefreshGateDrainTimeout, CancellationToken.None).ConfigureAwait(false))
        {
            _refreshGate.Dispose();
        }
        else
        {
            _logger.LogDebug("Опрос Helix не завершился за {Timeout} – семафор опроса оставлен сборщику мусора", RefreshGateDrainTimeout);
        }

        _disposeCts.Dispose();
        _runCts?.Dispose();
        _runCts = null;

        if (_subscribed)
        {
            _eventSubClient.OnSessionWelcome -= OnSessionWelcomeAsync;
            _eventSubClient.OnNotification -= OnNotificationAsync;
            _eventSubClient.OnSessionReconnect -= OnSessionReconnectAsync;
            _eventSubClient.OnRevocation -= OnRevocationAsync;
            _eventSubClient.OnDisconnected -= OnDisconnectedAsync;

            _channelUpdatedSubscription?.Dispose();
            _channelUpdatedSubscription = null;
            _subscribed = false;
        }

        GC.SuppressFinalize(this);
    }

    public async Task RefreshLiveSnapshotAsync()
    {
        var token = _disposeCts.Token;

        try
        {
            await _refreshGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Live-snapshot отменён в очереди: StreamStatusManager уничтожается");
            return;
        }

        try
        {
            var broadcasterId = await _broadcasterIdProvider.GetAsync(token);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                _logger.LogDebug("Live-snapshot пропущен: BroadcasterId недоступен");
                return;
            }

            var stream = await _helix.GetStreamAsync(broadcasterId, token);

            if (stream != null)
            {
                var transition = _state.ApplyOnlineSnapshot(StreamInfoMapper.MapFromHelix(stream));

                if (transition.Transitioned)
                {
                    _logger.LogInformation("Live-snapshot: переход {OldStatus} → Online для BroadcasterId {BroadcasterId}, стрим {StreamId}, начат {StartedAt}",
                        transition.Previous,
                        broadcasterId,
                        stream.Id,
                        stream.StartedAt);

                    await PublishStatusTransitionAsync(StreamStatus.Online, true).ConfigureAwait(false);
                }
                else if (transition.SnapshotChanged)
                {
                    _logger.LogInformation("Live-snapshot: метаданные стрима {StreamId} обновлены без смены статуса", stream.Id);

                    await PublishMetadataResolvedAsync().ConfigureAwait(false);
                }

                return;
            }

            var probe = _state.ProbeOfflineDivergence(_timeProvider.GetUtcNow().UtcDateTime, StuckOnlineThreshold);

            switch (probe.Action)
            {
                case OfflineProbeAction.ForcedOffline:
                    _logger.LogWarning("Принудительный перевод в Offline после {ElapsedSeconds} с расхождения с API для BroadcasterId {BroadcasterId}, стрим {StreamId}",
                        (int)probe.Elapsed.TotalSeconds, broadcasterId, CurrentStream?.Id);

                    await PublishStatusTransitionAsync(StreamStatus.Offline).ConfigureAwait(false);
                    return;

                case OfflineProbeAction.Pending:
                    _logger.LogWarning("API сообщает офлайн при локальном Online, ждём подтверждения (расхождение {Elapsed}/{Threshold}), стрим {StreamId}",
                        probe.Elapsed, StuckOnlineThreshold, CurrentStream?.Id);

                    return;

                default:
                    return;
            }
        }
        catch (OperationCanceledException ex) when (_disposeCts.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Live-snapshot отменён: StreamStatusManager уничтожается");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось обновить live-snapshot стрима: {Reason}", StreamingErrorMessages.SafeMessage(ex));
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task OnSessionWelcomeAsync(EventSubSessionWelcomeArgs args, CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventSub WebSocket подключен. SessionId: {SessionId}", args.SessionId);

        _retry.Cancel();
        _state.ForgetChannelUpdate();

        await CreateEventSubSubscriptionsAsync(args.SessionId, cancellationToken);
        await InitializeFromApiAsync(cancellationToken);
    }

    private async Task OnNotificationAsync(EventSubNotificationArgs args, CancellationToken cancellationToken)
    {
        switch (args.SubscriptionType)
        {
            case "stream.online":
                await HandleStreamOnlineAsync(args.Payload);
                break;

            case "stream.offline":
                await HandleStreamOfflineAsync();
                break;

            default:
                _logger.LogDebug("StreamStatusManager игнорирует EventSub-нотификацию типа {Type}", args.SubscriptionType);
                break;
        }
    }

    private Task OnSessionReconnectAsync(EventSubReconnectArgs args, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Сессия EventSub мигрировала: {OldSession} → {NewSession}; подписки и статус стрима сохраняются", args.OldSessionId, args.NewSessionId);
        return Task.CompletedTask;
    }

    private async Task OnRevocationAsync(EventSubRevocationArgs args, CancellationToken cancellationToken)
    {
        if (args.SubscriptionType is not ("stream.online" or "stream.offline"))
        {
            _logger.LogDebug("Revocation {Type} не относится к StreamStatusManager – игнорируется", args.SubscriptionType);
            return;
        }

        _logger.LogWarning("EventSub revocation: id={Id}, type={Type}, status={Status}", args.SubscriptionId, args.SubscriptionType, args.Status);
        _ledger.Forget(args.SubscriptionType, args.SubscriptionId);

        var sessionId = _eventSubClient.SessionId;

        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogDebug("Восстановление подписки {Type} пропущено: SessionId уже отсутствует", args.SubscriptionType);
            return;
        }

        var generation = _ledger.Generation;

        try
        {
            var broadcasterId = await _broadcasterIdProvider.GetAsync(cancellationToken);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                _logger.LogWarning("Восстановление подписки {Type} прервано: BroadcasterId недоступен", args.SubscriptionType);
                return;
            }

            var condition = new Dictionary<string, string>
            {
                { "broadcaster_user_id", broadcasterId },
            };

            var result = await _retry.CreateAsync(args.SubscriptionType,
                "1",
                condition,
                sessionId,
                retried => _ledger.RecordAsync(generation, args.SubscriptionType, retried),
                cancellationToken);

            await _ledger.RecordAsync(generation, args.SubscriptionType, result);

            if (result.Outcome == EventSubSubscribeOutcome.Created)
            {
                _logger.LogInformation("Подписка {Type} восстановлена после revocation. Новый id: {Id}", args.SubscriptionType, result.SubscriptionIds[0]);
            }
            else if (result.Outcome == EventSubSubscribeOutcome.Reused)
            {
                _logger.LogInformation("Подписка {Type} {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем",
                    args.SubscriptionType, result.SubscriptionIds);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Не удалось восстановить подписку {Type} после revocation: {Reason}",
                args.SubscriptionType, StreamingErrorMessages.SafeMessage(ex));
        }
    }

    private Task OnDisconnectedAsync(EventSubDisconnectedArgs args, CancellationToken cancellationToken)
    {
        _logger.LogWarning("EventSub WebSocket отключен ({Reason}) – статус стрима {OldStatus} (стрим {StreamId}) сброшен в Unknown до восстановления соединения",
            args.Reason,
            _state.CurrentStatus,
            CurrentStream?.Id);

        _retry.Cancel();
        _state.ResetToUnknown();
        return Task.CompletedTask;
    }

    private async Task InitializeFromApiAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var token = linkedCts.Token;

        try
        {
            await _refreshGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Инициализация по API отменена в очереди опроса");
            return;
        }

        try
        {
            var broadcasterId = await _broadcasterIdProvider.GetAsync(token);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                _logger.LogDebug("Инициализация по API пропущена: BroadcasterId недоступен");
                return;
            }

            _logger.LogDebug("Запрос начального статуса стрима через API для BroadcasterId: {BroadcasterId}", broadcasterId);
            var stream = await _helix.GetStreamAsync(broadcasterId, token);

            StatusTransition transition;
            StreamStatus newStatus;

            if (stream != null)
            {
                transition = _state.ApplyOnlineSnapshot(StreamInfoMapper.MapFromHelix(stream));
                newStatus = StreamStatus.Online;
            }
            else
            {
                transition = _state.ApplyOffline();
                newStatus = StreamStatus.Offline;
            }

            if (transition.Transitioned)
            {
                var isCatchUp = transition.Previous == StreamStatus.Unknown;

                _logger.LogInformation("Инициализация: статус стрима {OldStatus} → {NewStatus} для BroadcasterId {BroadcasterId}, стрим {StreamId}, начат {StartedAt}, catch-up {IsCatchUp}",
                    transition.Previous,
                    newStatus,
                    broadcasterId,
                    stream?.Id,
                    stream?.StartedAt,
                    isCatchUp);

                await PublishStatusTransitionAsync(newStatus, isCatchUp).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Инициализация: статус стрима {Status} подтверждён для BroadcasterId {BroadcasterId}, стрим {StreamId}",
                    newStatus,
                    broadcasterId,
                    stream?.Id);

                if (transition.SnapshotChanged && newStatus == StreamStatus.Online)
                {
                    _logger.LogInformation("Инициализация: метаданные стрима {StreamId} обновлены без смены статуса", stream?.Id);

                    await PublishMetadataResolvedAsync().ConfigureAwait(false);
                }
            }

            if (stream != null)
            {
                _logger.LogInformation("Начальный статус по данным API: онлайн, стрим {StreamId}, начат {StartedAt}", stream.Id, stream.StartedAt);
            }
            else
            {
                _logger.LogInformation("Начальный статус по данным API: офлайн");
            }
        }
        catch (OperationCanceledException ex) when (token.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Инициализация по API отменена (cancellationToken: {CallerCancelled}, dispose: {DisposeCancelled})",
                cancellationToken.IsCancellationRequested, _disposeCts.IsCancellationRequested);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось получить начальный статус стрима: {Reason}", StreamingErrorMessages.SafeMessage(ex));
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void OnChannelUpdated(ChannelUpdated @event)
    {
        if (_state.ApplyChannelUpdate(@event))
        {
            _logger.LogInformation("Метаданные стрима обновлены через channel.update: title={Title}, game={GameName}",
                @event.Title, @event.GameName);
        }
        else
        {
            _logger.LogDebug("ChannelUpdated сохранён без обновления CurrentStream: стрим не в состоянии online");
        }
    }

    private async Task HandleStreamOnlineAsync(JsonElement payload)
    {
        var data = payload.Deserialize<EventSubStreamOnlinePayloadDto>(WebJsonOptions);

        const bool isCatchUp = false;

        var streamId = data?.Event?.Id;
        var startedAt = data?.Event?.StartedAt ?? default;
        var eventType = data?.Event?.Type ?? "live";

        _logger.LogInformation("Стрим запущен (EventSub): стрим {StreamId}, начат {StartedAt}, тип {EventType}",
            streamId,
            startedAt,
            eventType);

        var transition = _state.MarkOnline(streamId, startedAt);

        if (transition.Transitioned)
        {
            _logger.LogInformation("EventSub: переход {OldStatus} → Online, стрим {StreamId}, начат {StartedAt}, catch-up {IsCatchUp}",
                transition.Previous,
                streamId,
                startedAt,
                isCatchUp);

            await PublishStatusTransitionAsync(StreamStatus.Online, isCatchUp).ConfigureAwait(false);
        }

        var runToken = _runCts?.Token ?? CancellationToken.None;
        await _metadataRetryLoop.RestartAsync(runToken).ConfigureAwait(false);
    }

    private async Task HandleStreamOfflineAsync()
    {
        var streamId = CurrentStream?.Id;

        _logger.LogInformation("Стрим завершен (EventSub): стрим {StreamId}", streamId);

        var transition = _state.ApplyOffline();

        if (transition.Transitioned)
        {
            _logger.LogInformation("EventSub: переход {OldStatus} → Offline, стрим {StreamId}", transition.Previous, streamId);
            await PublishStatusTransitionAsync(StreamStatus.Offline).ConfigureAwait(false);
        }
    }

    private async Task<bool> FetchMetadataAttemptAsync(int attempt, CancellationToken cancellationToken)
    {
        try
        {
            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var broadcasterId = await _broadcasterIdProvider.GetAsync(cancellationToken);

                if (string.IsNullOrEmpty(broadcasterId))
                {
                    return false;
                }

                var stream = await _helix.GetStreamAsync(broadcasterId, cancellationToken);

                if (stream == null)
                {
                    return false;
                }

                _state.UpdateStreamSnapshot(StreamInfoMapper.MapFromHelix(stream));

                if (_state.CurrentStream == null)
                {
                    return false;
                }

                await PublishMetadataResolvedAsync().ConfigureAwait(false);
                return true;
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения метаданных стрима (попытка {Attempt}): {Reason}",
                attempt, StreamingErrorMessages.SafeMessage(ex));

            return false;
        }
    }

    private bool IsCurrentlyOnline()
    {
        return _state.CurrentStatus == StreamStatus.Online;
    }

    private Task PublishStatusTransitionAsync(StreamStatus newStatus, bool isCatchUp = false)
    {
        var channel = _targetChannelProvider.Current.Login;

        if (string.IsNullOrWhiteSpace(channel))
        {
            _logger.LogDebug("Публикация события статуса стрима пропущена: имя канала неизвестно");
            return Task.CompletedTask;
        }

        return newStatus switch
        {
            StreamStatus.Online => _eventBus.PublishAsync(new StreamWentOnline(channel, CurrentStream, isCatchUp), CancellationToken.None),
            StreamStatus.Offline => _eventBus.PublishAsync(new StreamWentOffline(channel, isCatchUp), CancellationToken.None),
            _ => Task.CompletedTask,
        };
    }

    private Task PublishMetadataResolvedAsync()
    {
        var channel = _targetChannelProvider.Current.Login;
        var stream = CurrentStream;

        if (string.IsNullOrWhiteSpace(channel) || stream == null)
        {
            _logger.LogDebug("Публикация StreamMetadataResolved пропущена (channel пуст или stream null)");
            return Task.CompletedTask;
        }

        return _eventBus.PublishAsync(new StreamMetadataResolved(channel, stream), CancellationToken.None);
    }

    private async Task CreateEventSubSubscriptionsAsync(string sessionId, CancellationToken cancellationToken)
    {
        var generation = await _ledger.BeginSessionAsync(cancellationToken);
        var broadcasterId = await _broadcasterIdProvider.GetAsync(cancellationToken);

        if (string.IsNullOrEmpty(broadcasterId))
        {
            _logger.LogWarning("Подписки EventSub пропущены – BroadcasterId недоступен (вероятно, нет токена бота). Подписки создадутся после авторизации.");
            return;
        }

        _logger.LogInformation("Создание подписок EventSub для BroadcasterId: {BroadcasterId}", broadcasterId);

        var subscriptionsCreated = 0;
        var condition = new Dictionary<string, string>
        {
            { "broadcaster_user_id", broadcasterId },
        };

        foreach (var type in new[] { "stream.online", "stream.offline" })
        {
            try
            {
                var result = await _retry.CreateAsync(type,
                    "1",
                    condition,
                    sessionId,
                    retried => _ledger.RecordAsync(generation, type, retried),
                    cancellationToken);

                if (await _ledger.RecordAsync(generation, type, result) && result.IsSubscribed)
                {
                    subscriptionsCreated++;
                }

                if (result.Outcome == EventSubSubscribeOutcome.Created)
                {
                    _logger.LogInformation("Подписка '{Type}' создана. SubscriptionId: {SubscriptionId}, SessionId: {SessionId}",
                        type, result.SubscriptionIds[0], sessionId);
                }
                else if (result.Outcome == EventSubSubscribeOutcome.Reused)
                {
                    _logger.LogInformation("Подписка '{Type}' {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем (SessionId: {SessionId})",
                        type, result.SubscriptionIds, sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка создания подписки {Type} для BroadcasterId {BroadcasterId}: {Reason}",
                    type, broadcasterId, StreamingErrorMessages.SafeMessage(ex));
            }
        }

        if (subscriptionsCreated == 2)
        {
            _logger.LogInformation("Все необходимые подписки EventSub успешно созданы для BroadcasterId: {BroadcasterId}", broadcasterId);
        }
        else
        {
            _logger.LogWarning("Создано только {CreatedCount}/2 подписок EventSub stream.* для BroadcasterId {BroadcasterId} – статус стрима будет неполным",
                subscriptionsCreated, broadcasterId);
        }
    }

    internal Task? MetadataRetryTask => _metadataRetryLoop.CurrentTask;
}
