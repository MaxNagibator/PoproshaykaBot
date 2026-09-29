using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Twitch.EventSub;

public sealed class EventSubSubscriptionLedger(ITwitchHelixClient helix, ILogger logger)
{
    private readonly object _lock = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _idsByType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _undeletedByType = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revoked = new(StringComparer.Ordinal);
    private long _generation;

    public long Generation
    {
        get
        {
            lock (_lock)
            {
                return _generation;
            }
        }
    }

    public bool Holds(string type)
    {
        lock (_lock)
        {
            return _idsByType.ContainsKey(type);
        }
    }

    public async Task<long> BeginSessionAsync(CancellationToken cancellationToken)
    {
        long generation;
        KeyValuePair<string, IReadOnlyList<string>>[] previous;

        lock (_lock)
        {
            generation = ++_generation;
            previous = TakeAllForDeletion();
        }

        foreach (var (type, ids) in previous)
        {
            logger.LogDebug("Подписка EventSub {Type} {SubscriptionIds} прошлой сессии удаляется на новом session_welcome", type, ids);
            KeepUndeleted(type, await EventSubSubscriptions.DeleteAsync(helix, type, ids, logger, cancellationToken));
        }

        return generation;
    }

    public void Forget(string type, string subscriptionId)
    {
        lock (_lock)
        {
            _revoked.Add(subscriptionId);

            if (!_idsByType.TryGetValue(type, out var ids))
            {
                return;
            }

            var remaining = ids.Where(x => !string.Equals(x, subscriptionId, StringComparison.Ordinal)).ToArray();

            if (remaining.Length > 0)
            {
                _idsByType[type] = remaining;
            }
            else
            {
                _idsByType.Remove(type);
            }
        }
    }

    public async Task<bool> RecordAsync(long generation, string type, EventSubSubscribeResult result)
    {
        string[] displaced = [];
        bool recorded;
        bool revoked;

        lock (_lock)
        {
            var ids = result.SubscriptionIds.Where(x => !_revoked.Contains(x)).ToArray();
            revoked = result.IsSubscribed && ids.Length == 0;
            recorded = generation == _generation && !revoked;

            if (recorded)
            {
                displaced = _idsByType.TryGetValue(type, out var previous)
                    ? previous.Except(ids, StringComparer.Ordinal).ToArray()
                    : [];

                if (result.IsSubscribed)
                {
                    _idsByType[type] = ids;
                }
                else
                {
                    _idsByType.Remove(type);
                }
            }
        }

        if (recorded)
        {
            if (displaced.Length > 0)
            {
                logger.LogInformation("Подписка EventSub {Type} {SubscriptionIds} вытеснена новым ответом – удаляем", type, displaced);
                KeepUndeleted(type, await EventSubSubscriptions.DeleteDetachedAsync(helix, type, displaced, logger));
            }

            return true;
        }

        if (revoked)
        {
            logger.LogInformation("Подписка EventSub {Type} {SubscriptionIds} получена уже отозванной – не записываем", type, result.SubscriptionIds);
            return false;
        }

        if (!result.IsSubscribed)
        {
            return false;
        }

        logger.LogInformation("Подписка EventSub {Type} {SubscriptionIds} получена для прошлой сессии либо после остановки подписчика – удаляем",
            type, result.SubscriptionIds);

        KeepUndeleted(type, await EventSubSubscriptions.DeleteDetachedAsync(helix, type, result.SubscriptionIds, logger));
        return false;
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        KeyValuePair<string, IReadOnlyList<string>>[] owned;

        lock (_lock)
        {
            _generation++;
            owned = TakeAllForDeletion();
        }

        foreach (var (type, ids) in owned)
        {
            KeepUndeleted(type, await EventSubSubscriptions.DeleteAsync(helix, type, ids, logger, cancellationToken));
        }
    }

    private KeyValuePair<string, IReadOnlyList<string>>[] TakeAllForDeletion()
    {
        var all = new Dictionary<string, IReadOnlyList<string>>(_idsByType, StringComparer.Ordinal);

        foreach (var (type, ids) in _undeletedByType)
        {
            all[type] = all.TryGetValue(type, out var owned)
                ? owned.Union(ids, StringComparer.Ordinal).ToArray()
                : ids.ToArray();
        }

        _idsByType.Clear();
        _undeletedByType.Clear();
        _revoked.Clear();
        return all.ToArray();
    }

    private void KeepUndeleted(string type, IReadOnlyList<string> undeleted)
    {
        if (undeleted.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            if (!_undeletedByType.TryGetValue(type, out var ids))
            {
                ids = new(StringComparer.Ordinal);
                _undeletedByType[type] = ids;
            }

            ids.UnionWith(undeleted);
        }
    }
}
