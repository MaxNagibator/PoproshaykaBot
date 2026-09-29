using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Json;

namespace PoproshaykaBot.Core.Streaming;

public sealed class ChannelUpdateSubscriber(
    [FromKeyedServices(TwitchEndpoints.EventSubBroadcasterSession)]
    ITwitchEventSubClient eventSubClient,
    [FromKeyedServices(TwitchEndpoints.HelixBroadcasterClient)]
    ITwitchHelixClient helix,
    IBroadcasterIdProvider broadcasterIdProvider,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<ChannelUpdateSubscriber> logger)
    : IStreamHostedComponent
{
    private const string SubscriptionType = "channel.update";
    private const string SubscriptionVersion = "2";

    private readonly EventSubSubscriptionLedger _ledger = new(helix, logger);
    private readonly EventSubSubscriptionRetry _retry = new(helix, eventSubClient, timeProvider, logger);
    private bool _subscribed;

    public bool IsHealthy { get; private set; } = true;

    public string Name => "Подписка на EventSub channel.update";

    public int StartOrder => 255;

    public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_subscribed)
        {
            return Task.CompletedTask;
        }

        eventSubClient.OnSessionWelcome += HandleSessionWelcomeAsync;
        eventSubClient.OnNotification += HandleNotificationAsync;
        eventSubClient.OnRevocation += HandleRevocationAsync;
        eventSubClient.OnDisconnected += HandleDisconnectedAsync;
        _subscribed = true;

        logger.LogInformation("ChannelUpdateSubscriber: хуки EventSub установлены");
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
        eventSubClient.OnRevocation -= HandleRevocationAsync;
        eventSubClient.OnDisconnected -= HandleDisconnectedAsync;
        _subscribed = false;

        await _retry.CancelAndDrainAsync(cancellationToken);
        await _ledger.CloseAsync(cancellationToken);
    }

    private async Task HandleSessionWelcomeAsync(EventSubSessionWelcomeArgs args, CancellationToken cancellationToken)
    {
        _retry.Cancel();

        var generation = await _ledger.BeginSessionAsync(cancellationToken);
        var broadcasterId = await broadcasterIdProvider.GetAsync(cancellationToken);

        if (string.IsNullOrEmpty(broadcasterId))
        {
            logger.LogWarning("ChannelUpdateSubscriber: подписка channel.update пропущена – broadcaster id недоступен (вероятно, нет токена бота).");
            IsHealthy = false;
            return;
        }

        try
        {
            var result = await SubscribeAsync(generation, broadcasterId, args.SessionId, cancellationToken);

            IsHealthy = _ledger.Holds(SubscriptionType);

            if (result.Outcome == EventSubSubscribeOutcome.Created)
            {
                logger.LogInformation("ChannelUpdateSubscriber: подписка на {Type} создана", SubscriptionType);
            }
            else if (result.Outcome == EventSubSubscribeOutcome.Reused)
            {
                logger.LogInformation("ChannelUpdateSubscriber: подписка {Type} {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем",
                    SubscriptionType, result.SubscriptionIds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ChannelUpdateSubscriber: не удалось подписаться на {Type} – смена title/game работать не будет", SubscriptionType);
            IsHealthy = false;
        }
    }

    private async Task HandleNotificationAsync(EventSubNotificationArgs args, CancellationToken cancellationToken)
    {
        if (!string.Equals(args.SubscriptionType, SubscriptionType, StringComparison.Ordinal))
        {
            return;
        }

        if (!args.Payload.TryGetProperty("event", out var evt))
        {
            return;
        }

        var title = evt.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty;
        var language = evt.TryGetProperty("language", out var langProp) ? langProp.GetString() ?? string.Empty : string.Empty;
        var gameId = evt.TryGetProperty("category_id", out var gameIdProp) ? gameIdProp.GetString() ?? string.Empty : string.Empty;
        var gameName = evt.TryGetProperty("category_name", out var gameNameProp) ? gameNameProp.GetString() ?? string.Empty : string.Empty;
        var labels = ParseStringArray(evt, "content_classification_labels");

        try
        {
            await eventBus.PublishAsync(new ChannelUpdated(title, language, gameId, gameName, labels), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ChannelUpdateSubscriber: ошибка публикации ChannelUpdated");
        }
    }

    private async Task HandleRevocationAsync(EventSubRevocationArgs args, CancellationToken cancellationToken)
    {
        if (!string.Equals(args.SubscriptionType, SubscriptionType, StringComparison.Ordinal))
        {
            return;
        }

        logger.LogWarning("ChannelUpdateSubscriber: подписка {Type} отозвана ({Status})", args.SubscriptionType, args.Status);
        _ledger.Forget(SubscriptionType, args.SubscriptionId);
        IsHealthy = false;

        var generation = _ledger.Generation;

        var sessionId = eventSubClient.SessionId;

        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogError("ChannelUpdateSubscriber: подписка {Type} отозвана, нет активной сессии для восстановления", SubscriptionType);
            return;
        }

        var broadcasterId = await broadcasterIdProvider.GetAsync(cancellationToken);

        if (string.IsNullOrEmpty(broadcasterId))
        {
            logger.LogWarning("ChannelUpdateSubscriber: подписка {Type} отозвана, восстановление пропущено – broadcaster id недоступен", SubscriptionType);
            return;
        }

        try
        {
            var result = await SubscribeAsync(generation, broadcasterId, sessionId, cancellationToken);

            IsHealthy = _ledger.Holds(SubscriptionType);

            if (result.Outcome == EventSubSubscribeOutcome.Created)
            {
                logger.LogInformation("ChannelUpdateSubscriber: подписка {Type} восстановлена после revocation", SubscriptionType);
            }
            else if (result.Outcome == EventSubSubscribeOutcome.Reused)
            {
                logger.LogInformation("ChannelUpdateSubscriber: подписка {Type} {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем после revocation",
                    SubscriptionType, result.SubscriptionIds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ChannelUpdateSubscriber: не удалось восстановить подписку {Type} после revocation", SubscriptionType);
        }
    }

    private async Task<EventSubSubscribeResult> SubscribeAsync(long generation, string broadcasterId, string sessionId, CancellationToken cancellationToken)
    {
        var result = await _retry.CreateAsync(SubscriptionType,
            SubscriptionVersion,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = broadcasterId,
            },
            sessionId,
            retried => RecordRetriedAsync(generation, retried),
            cancellationToken);

        await _ledger.RecordAsync(generation, SubscriptionType, result);
        return result;
    }

    private async Task RecordRetriedAsync(long generation, EventSubSubscribeResult result)
    {
        if (await _ledger.RecordAsync(generation, SubscriptionType, result))
        {
            IsHealthy = true;
        }
    }

    private Task HandleDisconnectedAsync(EventSubDisconnectedArgs args, CancellationToken cancellationToken)
    {
        _retry.Cancel();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var arrayProp) || arrayProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>(arrayProp.GetArrayLength());

        foreach (var item in arrayProp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();

            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }

        return result;
    }
}
