using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Twitch.Chat;

public sealed class ChatIngestionService(
    [FromKeyedServices(TwitchEndpoints.EventSubBotSession)]
    ITwitchEventSubClient eventSubClient,
    [FromKeyedServices(TwitchEndpoints.HelixBotClient)]
    ITwitchHelixClient helix,
    IBroadcasterIdProvider broadcasterIdProvider,
    IBotUserIdProvider botUserIdProvider,
    SettingsManager settingsManager,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<ChatIngestionService> logger)
    : IHostedComponent
{
    private const string ChatMessageSubscriptionType = "channel.chat.message";

    private readonly object _joinLock = new();
    private readonly EventSubSubscriptionRetry _retry = new(helix, eventSubClient, timeProvider, logger);
    private bool _subscribed;
    private long _generation;
    private IReadOnlyList<string> _subscriptionIds = [];
    private long _joinedAtUtcTicks;
    private long _lastMessageAtUtcTicks;

    public string Name => "Чтение сообщений чата (EventSub)";

    public int StartOrder => 250;

    public bool IsJoined => Interlocked.Read(ref _joinedAtUtcTicks) != 0;

    public DateTimeOffset? JoinedAt => ToTimestamp(Interlocked.Read(ref _joinedAtUtcTicks));

    public DateTimeOffset? LastMessageAt => ToTimestamp(Interlocked.Read(ref _lastMessageAtUtcTicks));

    public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_subscribed)
        {
            return Task.CompletedTask;
        }

        eventSubClient.OnSessionWelcome += HandleSessionWelcomeAsync;
        eventSubClient.OnNotification += HandleNotificationAsync;
        eventSubClient.OnDisconnected += HandleDisconnectedAsync;
        eventSubClient.OnRevocation += HandleRevocationAsync;
        _subscribed = true;

        logger.LogInformation("ChatIngestionService: подписка на EventSub установлена");

        if (eventSubClient.SessionId is { } sessionId)
        {
            return HandleSessionWelcomeAsync(new(sessionId, null), cancellationToken);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!_subscribed)
        {
            return;
        }

        eventSubClient.OnSessionWelcome -= HandleSessionWelcomeAsync;
        eventSubClient.OnNotification -= HandleNotificationAsync;
        eventSubClient.OnDisconnected -= HandleDisconnectedAsync;
        eventSubClient.OnRevocation -= HandleRevocationAsync;
        _subscribed = false;

        logger.LogInformation("ChatIngestionService: отписка от EventSub");

        await _retry.CancelAndDrainAsync(cancellationToken);
        await MarkLeftAsync(out _, out var subscriptionIds);

        await EventSubSubscriptions.DeleteAsync(helix, ChatMessageSubscriptionType, subscriptionIds, logger, cancellationToken);
    }

    private static DateTimeOffset? ToTimestamp(long utcTicks)
    {
        return utcTicks == 0 ? null : new DateTimeOffset(utcTicks, TimeSpan.Zero);
    }

    private async Task HandleSessionWelcomeAsync(EventSubSessionWelcomeArgs args, CancellationToken ct)
    {
        logger.LogInformation("ChatIngestionService: EventSub сессия открыта ({SessionId}), регистрируем channel.chat.message", args.SessionId);

        await MarkLeftAsync(out var generation, out _);
        await SubscribeAsync(generation, args.SessionId, ct);
    }

    private async Task SubscribeAsync(long generation, string sessionId, CancellationToken ct)
    {
        try
        {
            var settings = settingsManager.Current.Twitch;

            var broadcasterId = await broadcasterIdProvider.GetAsync(ct);
            if (string.IsNullOrEmpty(broadcasterId))
            {
                logger.LogError("ChatIngestionService: не удалось получить broadcaster id для канала '{Channel}'", settings.Channel);
                return;
            }

            var botId = await botUserIdProvider.GetAsync(ct);
            if (string.IsNullOrEmpty(botId))
            {
                logger.LogError("ChatIngestionService: не удалось получить user id бота через токен");
                return;
            }

            var delays = new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
            Exception? lastError = null;

            var condition = new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = broadcasterId,
                ["user_id"] = botId,
            };

            for (var attempt = 0; attempt < delays.Length; attempt++)
            {
                if (delays[attempt] > TimeSpan.Zero)
                {
                    await Task.Delay(delays[attempt], ct);
                }

                try
                {
                    var result = await _retry.CreateAsync(ChatMessageSubscriptionType,
                        "1",
                        condition,
                        sessionId,
                        retried => JoinAsync(generation, retried),
                        ct);

                    if (!result.IsSubscribed || !await JoinAsync(generation, result))
                    {
                        return;
                    }

                    if (result.Outcome == EventSubSubscribeOutcome.Reused)
                    {
                        logger.LogInformation("ChatIngestionService: подписка channel.chat.message {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем (broadcaster={BroadcasterId}, bot={BotId})",
                            result.SubscriptionIds, broadcasterId, botId);
                    }
                    else
                    {
                        logger.LogInformation("ChatIngestionService: подписка channel.chat.message создана (broadcaster={BroadcasterId}, bot={BotId}, попытка {Attempt})",
                            broadcasterId, botId, attempt + 1);
                    }

                    return;
                }
                catch (Exception ex) when (attempt < delays.Length - 1)
                {
                    lastError = ex;
                    logger.LogWarning(ex, "ChatIngestionService: попытка {Attempt}/{Max} создать подписку channel.chat.message не удалась", attempt + 1, delays.Length);
                }
            }

            logger.LogError(lastError, "ChatIngestionService: не удалось подписаться на чат Twitch – все {Max} попытки создать подписку channel.chat.message провалились", delays.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ChatIngestionService: ошибка создания EventSub подписки channel.chat.message");
        }
    }

    private async Task HandleNotificationAsync(EventSubNotificationArgs args, CancellationToken ct)
    {
        if (!string.Equals(args.SubscriptionType, "channel.chat.message", StringComparison.Ordinal))
        {
            return;
        }

        Interlocked.Exchange(ref _lastMessageAtUtcTicks, timeProvider.GetUtcNow().UtcTicks);

        try
        {
            var botId = await botUserIdProvider.GetAsync(ct);
            var chatMessage = EventSubChatMessageMapper.Map(args.Payload, botId);
            var timestamp = new DateTimeOffset(args.MessageTimestamp, TimeSpan.Zero);
            await eventBus.PublishAsync(new RawChatMessageReceived(chatMessage, timestamp), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ChatIngestionService: ошибка обработки channel.chat.message");
        }
    }

    private Task HandleDisconnectedAsync(EventSubDisconnectedArgs args, CancellationToken ct)
    {
        return MarkLeftAsync();
    }

    private async Task HandleRevocationAsync(EventSubRevocationArgs args, CancellationToken ct)
    {
        if (!string.Equals(args.SubscriptionType, ChatMessageSubscriptionType, StringComparison.Ordinal))
        {
            return;
        }

        lock (_joinLock)
        {
            if (_subscriptionIds.Count > 0 && !_subscriptionIds.Contains(args.SubscriptionId, StringComparer.Ordinal))
            {
                logger.LogInformation("ChatIngestionService: отзыв подписки channel.chat.message {SubscriptionId} не относится к текущей {SubscriptionIds} – пропускаем",
                    args.SubscriptionId, _subscriptionIds);

                return;
            }
        }

        logger.LogWarning("ChatIngestionService: Twitch отозвал подписку channel.chat.message ({Status}) – пересоздаём", args.Status);

        await MarkLeftAsync(out var generation, out _);

        if (eventSubClient.SessionId is not { Length: > 0 } sessionId)
        {
            logger.LogError("ChatIngestionService: подписка channel.chat.message отозвана, нет активной сессии для восстановления – сообщения чата не приходят");
            return;
        }

        await SubscribeAsync(generation, sessionId, ct);
    }

    private async Task<bool> JoinAsync(long generation, EventSubSubscribeResult result)
    {
        await MarkJoinedAsync(generation, result.SubscriptionIds, out var current);

        if (current)
        {
            return true;
        }

        bool held;

        lock (_joinLock)
        {
            held = result.SubscriptionIds.Any(x => _subscriptionIds.Contains(x, StringComparer.Ordinal));
        }

        if (held)
        {
            logger.LogInformation("ChatIngestionService: подписка channel.chat.message {SubscriptionIds} переиспользована устаревшим запросом – её держит текущий, не удаляем",
                result.SubscriptionIds);

            return false;
        }

        logger.LogInformation("ChatIngestionService: подписка channel.chat.message {SubscriptionIds} получена, когда приём чата уже остановлен – удаляем",
            result.SubscriptionIds);

        await EventSubSubscriptions.DeleteDetachedAsync(helix, ChatMessageSubscriptionType, result.SubscriptionIds, logger);
        return false;
    }

    private Task MarkJoinedAsync(long generation, IReadOnlyList<string> subscriptionIds, out bool current)
    {
        var now = timeProvider.GetUtcNow();

        lock (_joinLock)
        {
            current = generation == _generation;
            if (!current)
            {
                return Task.CompletedTask;
            }

            _subscriptionIds = subscriptionIds;

            if (Interlocked.Exchange(ref _joinedAtUtcTicks, now.UtcTicks) != 0)
            {
                return Task.CompletedTask;
            }
        }

        return eventBus.PublishAsync(new ChatIngestionStarted(now), CancellationToken.None);
    }

    private Task MarkLeftAsync()
    {
        return MarkLeftAsync(out _, out _);
    }

    private Task MarkLeftAsync(out long generation, out IReadOnlyList<string> subscriptionIds)
    {
        long previous;

        lock (_joinLock)
        {
            generation = ++_generation;
            subscriptionIds = _subscriptionIds;
            _subscriptionIds = [];
            previous = Interlocked.Exchange(ref _joinedAtUtcTicks, 0);
        }

        _retry.Cancel();

        return previous != 0
            ? eventBus.PublishAsync(new ChatIngestionStopped(timeProvider.GetUtcNow()), CancellationToken.None)
            : Task.CompletedTask;
    }
}
