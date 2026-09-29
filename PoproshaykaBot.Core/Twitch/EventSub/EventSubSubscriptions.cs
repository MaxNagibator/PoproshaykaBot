using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;

namespace PoproshaykaBot.Core.Twitch.EventSub;

public enum EventSubSubscribeOutcome
{
    None = 0,
    Created = 1,
    Reused = 2,
    TakenByOtherSession = 3,
    Unresolved = 4,
    LookupFailed = 5,
}

public sealed record EventSubSubscribeResult(EventSubSubscribeOutcome Outcome, IReadOnlyList<string> SubscriptionIds)
{
    public bool IsSubscribed => SubscriptionIds.Count > 0;
}

public static class EventSubSubscriptions
{
    private const string EnabledStatus = "enabled";
    private const string FilterConditionKey = "broadcaster_user_id";

    internal static readonly TimeSpan DetachedDeleteTimeout = TimeSpan.FromSeconds(10);

    public static Task<EventSubSubscribeResult> CreateAsync(
        ITwitchHelixClient helix,
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        return CreateAsync(helix, type, version, condition, sessionId, logger, LogLevel.Warning, cancellationToken);
    }

    internal static async Task<EventSubSubscribeResult> CreateAsync(
        ITwitchHelixClient helix,
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        ILogger logger,
        LogLevel transientLevel,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0;; attempt++)
        {
            try
            {
                var id = await helix.CreateEventSubSubscriptionAsync(type, version, condition, sessionId, cancellationToken);
                return new(EventSubSubscribeOutcome.Created, [id]);
            }
            catch (HelixRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogDebug("Twitch ответил 409 на подписку EventSub {Type} для сессии {SessionId} – ищем существующую", type, sessionId);
            }

            var matches = await FindMatchesAsync(helix, type, condition, sessionId, logger, transientLevel, cancellationToken);

            if (matches is null)
            {
                return new(EventSubSubscribeOutcome.LookupFailed, []);
            }

            var own = matches
                .Where(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal)
                            && string.Equals(x.Status, EnabledStatus, StringComparison.Ordinal))
                .Select(x => x.Id)
                .ToArray();

            if (own.Length > 0)
            {
                return new(EventSubSubscribeOutcome.Reused, own);
            }

            var active = matches
                .Where(x => string.Equals(x.Status, EnabledStatus, StringComparison.Ordinal))
                .ToArray();

            if (active.Length > 0)
            {
                logger.Log(transientLevel,
                    "Подписка EventSub {Type} уже активна на другой сессии {OtherSessionId} (подписка {SubscriptionId}) – её держит другой запущенный экземпляр приложения либо прошлая сессия, которую Twitch ещё не признал отключённой; трогать её нельзя, на сессию {SessionId} уведомления этого типа не придут, пока она не освободится",
                    type, active[0].SessionId, active[0].Id, sessionId);

                var stale = matches
                    .Where(x => !string.Equals(x.Status, EnabledStatus, StringComparison.Ordinal))
                    .Select(x => x.Id)
                    .ToArray();

                await DeleteAsync(helix, type, stale, logger, cancellationToken);

                return new(EventSubSubscribeOutcome.TakenByOtherSession, []);
            }

            if (matches.Count == 0)
            {
                logger.LogWarning("Twitch ответил 409 на подписку EventSub {Type}, но подписки с тем же условием не нашлось – на сессию {SessionId} уведомления этого типа не придут",
                    type, sessionId);

                return new(EventSubSubscribeOutcome.Unresolved, []);
            }

            if (attempt > 0)
            {
                logger.LogWarning("Подписки EventSub {Type} отключённых сессий ({Count}) не удалось убрать – Twitch снова ответил 409, на сессию {SessionId} уведомления этого типа не придут",
                    type, matches.Count, sessionId);

                return new(EventSubSubscribeOutcome.Unresolved, []);
            }

            logger.LogInformation("Подписка EventSub {Type} осталась от отключённой сессии ({Status}) – удаляем {Count} и создаём заново для сессии {SessionId}",
                type, matches[0].Status, matches.Count, sessionId);

            await DeleteAsync(helix, type, matches.Select(x => x.Id).ToArray(), logger, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public static async Task<IReadOnlyList<string>> DeleteAsync(
        ITwitchHelixClient helix,
        string type,
        IReadOnlyList<string> subscriptionIds,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        List<string>? undeleted = null;

        for (var i = 0; i < subscriptionIds.Count; i++)
        {
            var subscriptionId = subscriptionIds[i];

            try
            {
                if (!await helix.DeleteEventSubSubscriptionAsync(subscriptionId, cancellationToken))
                {
                    logger.LogDebug("Подписки EventSub {Type} {SubscriptionId} в Twitch уже нет", type, subscriptionId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Удаление подписок EventSub {Type} прервано отменой, не удалено {Count} – уведомления будут приходить, пока открыт сокет EventSub",
                    type, subscriptionIds.Count - i);

                undeleted ??= [];
                undeleted.AddRange(subscriptionIds.Skip(i));
                return undeleted;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось удалить подписку EventSub {Type} {SubscriptionId} – уведомления будут приходить, пока открыт сокет EventSub",
                    type, subscriptionId);

                undeleted ??= [];
                undeleted.Add(subscriptionId);
            }
        }

        return undeleted ?? [];
    }

    internal static async Task<IReadOnlyList<string>> DeleteDetachedAsync(
        ITwitchHelixClient helix,
        string type,
        IReadOnlyList<string> subscriptionIds,
        ILogger logger)
    {
        using var timeout = new CancellationTokenSource(DetachedDeleteTimeout);
        return await DeleteAsync(helix, type, subscriptionIds, logger, timeout.Token);
    }

    private static async Task<IReadOnlyList<EventSubSubscriptionInfo>?> FindMatchesAsync(
        ITwitchHelixClient helix,
        string type,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        ILogger logger,
        LogLevel failureLevel,
        CancellationToken cancellationToken)
    {
        if (!condition.TryGetValue(FilterConditionKey, out var userId) || string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Подписку EventSub {Type} после 409 искать не по чему – в условии нет {Key}; на сессию {SessionId} уведомления этого типа не придут",
                type, FilterConditionKey, sessionId);

            return null;
        }

        try
        {
            var subscriptions = await helix.GetEventSubSubscriptionsByUserAsync(userId, cancellationToken);

            return subscriptions
                .Where(x => string.Equals(x.Type, type, StringComparison.Ordinal) && HasCondition(x, condition))
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Log(failureLevel, ex, "Не удалось найти существующую подписку EventSub {Type} после 409 – на сессию {SessionId} уведомления этого типа не придут",
                type, sessionId);

            return null;
        }
    }

    private static bool HasCondition(EventSubSubscriptionInfo subscription, IReadOnlyDictionary<string, string> condition)
    {
        return condition.All(pair => subscription.Condition.TryGetValue(pair.Key, out var actual)
                                     && string.Equals(actual, pair.Value, StringComparison.Ordinal));
    }
}
