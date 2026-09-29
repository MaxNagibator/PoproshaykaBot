using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Polling;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Polls;

public sealed class PollEventSubscriber(
    [FromKeyedServices(TwitchEndpoints.EventSubBroadcasterSession)]
    ITwitchEventSubClient eventSubClient,
    [FromKeyedServices(TwitchEndpoints.HelixBroadcasterClient)]
    ITwitchHelixClient helix,
    IBroadcasterIdProvider broadcasterIdProvider,
    PollsAvailabilityService availability,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<PollEventSubscriber> logger)
    : IStreamHostedComponent
{
    private static readonly string[] SubscriptionTypes =
    [
        "channel.poll.begin",
        "channel.poll.progress",
        "channel.poll.end",
    ];

    private readonly EventSubSubscriptionLedger _ledger = new(helix, logger);
    private readonly EventSubSubscriptionRetry _retry = new(helix, eventSubClient, timeProvider, logger);
    private bool _subscribed;

    public bool IsHealthy { get; private set; } = true;

    public string Name => "Подписка на EventSub голосований";

    public int StartOrder => 260;

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

        logger.LogInformation("PollEventSubscriber: хуки EventSub установлены");

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
        eventSubClient.OnRevocation -= HandleRevocationAsync;
        eventSubClient.OnDisconnected -= HandleDisconnectedAsync;
        _subscribed = false;

        await _retry.CancelAndDrainAsync(cancellationToken);
        await _ledger.CloseAsync(cancellationToken);
    }

    private async Task HandleSessionWelcomeAsync(EventSubSessionWelcomeArgs args, CancellationToken ct)
    {
        _retry.Cancel();

        var generation = await _ledger.BeginSessionAsync(ct);
        var availabilityResult = await availability.GetAsync(ct);

        if (!availabilityResult.IsAvailable)
        {
            logger.LogInformation("PollEventSubscriber: подписки не создаются – {Reason}", availabilityResult.UnavailableReason);
            IsHealthy = false;
            return;
        }

        var broadcasterId = await broadcasterIdProvider.GetAsync(ct);

        if (string.IsNullOrEmpty(broadcasterId))
        {
            logger.LogError("PollEventSubscriber: не удалось получить broadcaster id");
            IsHealthy = false;
            return;
        }

        foreach (var type in SubscriptionTypes)
        {
            try
            {
                var result = await SubscribeAsync(generation, type, broadcasterId, args.SessionId, ct);

                if (result.Outcome == EventSubSubscribeOutcome.Created)
                {
                    logger.LogInformation("PollEventSubscriber: подписка на {Type} создана", type);
                }
                else if (result.Outcome == EventSubSubscribeOutcome.Reused)
                {
                    logger.LogInformation("PollEventSubscriber: подписка {Type} {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем",
                        type, result.SubscriptionIds);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PollEventSubscriber: ошибка подписки на {Type}", type);
            }
        }

        RefreshHealth();

        await TryRecoverActivePollAsync(broadcasterId, ct);
    }

    private async Task<EventSubSubscribeResult> SubscribeAsync(long generation, string type, string broadcasterId, string sessionId, CancellationToken ct)
    {
        var result = await _retry.CreateAsync(type,
            "1",
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = broadcasterId,
            },
            sessionId,
            retried => RecordRetriedAsync(generation, type, retried),
            ct);

        await _ledger.RecordAsync(generation, type, result);
        return result;
    }

    private async Task RecordRetriedAsync(long generation, string type, EventSubSubscribeResult result)
    {
        if (await _ledger.RecordAsync(generation, type, result))
        {
            RefreshHealth();
        }
    }

    private void RefreshHealth()
    {
        IsHealthy = SubscriptionTypes.All(_ledger.Holds);
    }

    private Task HandleDisconnectedAsync(EventSubDisconnectedArgs args, CancellationToken ct)
    {
        _retry.Cancel();
        return Task.CompletedTask;
    }

    private async Task HandleNotificationAsync(EventSubNotificationArgs args, CancellationToken ct)
    {
        if (!args.SubscriptionType.StartsWith("channel.poll.", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await JsonEventProjector.ProjectAsync(args, eventBus, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PollEventSubscriber: ошибка обработки {Type}", args.SubscriptionType);
        }
    }

    private async Task HandleRevocationAsync(EventSubRevocationArgs args, CancellationToken ct)
    {
        var type = args.SubscriptionType;

        if (!SubscriptionTypes.Contains(type, StringComparer.Ordinal))
        {
            return;
        }

        logger.LogWarning("PollEventSubscriber: подписка {Type} отозвана ({Status})", type, args.Status);
        _ledger.Forget(type, args.SubscriptionId);
        IsHealthy = false;

        var generation = _ledger.Generation;
        var sessionId = eventSubClient.SessionId;

        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogError("PollEventSubscriber: подписка {Type} отозвана, нет активной сессии для восстановления", type);
            return;
        }

        try
        {
            var availabilityResult = await availability.GetAsync(ct);

            if (!availabilityResult.IsAvailable)
            {
                logger.LogInformation("PollEventSubscriber: подписка {Type} после revocation не восстанавливается – {Reason}",
                    type, availabilityResult.UnavailableReason);
                return;
            }

            var broadcasterId = await broadcasterIdProvider.GetAsync(ct);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                logger.LogWarning("PollEventSubscriber: подписка {Type} отозвана, восстановление пропущено – broadcaster id недоступен", type);
                return;
            }

            var result = await SubscribeAsync(generation, type, broadcasterId, sessionId, ct);

            if (result.Outcome == EventSubSubscribeOutcome.Created)
            {
                logger.LogInformation("PollEventSubscriber: подписка {Type} восстановлена после revocation", type);
            }
            else if (result.Outcome == EventSubSubscribeOutcome.Reused)
            {
                logger.LogInformation("PollEventSubscriber: подписка {Type} {SubscriptionIds} уже существует для текущей EventSub-сессии – переиспользуем после revocation",
                    type, result.SubscriptionIds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PollEventSubscriber: не удалось восстановить подписку {Type} после revocation", type);
        }

        RefreshHealth();
    }

    private async Task TryRecoverActivePollAsync(string broadcasterId, CancellationToken ct)
    {
        try
        {
            var polls = await helix.GetPollsAsync(broadcasterId, 20, ct);

            var snapshot = polls
                .Select(MapOrWarn)
                .FirstOrDefault(candidate => candidate?.Status == PollSnapshotStatus.Active);

            if (snapshot is null)
            {
                logger.LogDebug("PollEventSubscriber: активных голосований нет, восстанавливать нечего");
                return;
            }

            logger.LogInformation("PollEventSubscriber: найдено активное голосование {PollId}, восстанавливаем", snapshot.PollId);
            await eventBus.PublishAsync(new PollStarted(snapshot), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PollEventSubscriber: не удалось проверить активное голосование");
        }

        PollSnapshot? MapOrWarn(HelixPollInfo poll)
        {
            var candidate = PollEventSubMapper.FromHelix(poll, null);

            if (candidate is null)
            {
                logger.LogWarning("PollEventSubscriber: опрос {PollId} – неизвестный статус {Status}, пропущен",
                    poll.Id, poll.Status);
            }

            return candidate;
        }
    }

    private static class JsonEventProjector
    {
        public static async Task ProjectAsync(EventSubNotificationArgs args, IEventBus bus, CancellationToken ct)
        {
            switch (args.SubscriptionType)
            {
                case "channel.poll.begin":
                    await bus.PublishAsync(new PollStarted(PollEventSubMapper.FromEventSubBegin(args.Payload)), ct);
                    break;

                case "channel.poll.progress":
                    await bus.PublishAsync(new PollProgressed(PollEventSubMapper.FromEventSubProgress(args.Payload)), ct);
                    break;

                case "channel.poll.end":
                    var snapshot = PollEventSubMapper.FromEventSubEnd(args.Payload);

                    switch (snapshot.Status)
                    {
                        case PollSnapshotStatus.Completed:
                            var (winner, isTie) = PollEventSubMapper.DetectWinner(snapshot);
                            await bus.PublishAsync(new PollFinalized(snapshot, winner, isTie), ct);
                            break;

                        case PollSnapshotStatus.Terminated:
                            await bus.PublishAsync(new PollTerminated(snapshot), ct);
                            break;

                        case PollSnapshotStatus.Archived:
                            await bus.PublishAsync(new PollArchived(snapshot), ct);
                            break;

                        default:
                            await bus.PublishAsync(new PollFinalized(snapshot, null, false), ct);
                            break;
                    }

                    break;
            }
        }
    }
}
