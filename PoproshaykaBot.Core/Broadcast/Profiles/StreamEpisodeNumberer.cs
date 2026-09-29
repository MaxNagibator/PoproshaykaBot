using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Broadcast.Profiles;

public sealed class StreamEpisodeNumberer :
    IEventHandler<StreamWentOnline>,
    IEventHandler<StreamWentOffline>,
    IEventSubscriber,
    IDisposable
{
    public static readonly TimeSpan FlapCooldown = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RecentApplyWindow = TimeSpan.FromMinutes(15);

    private readonly BroadcastProfilesStore _profilesStore;
    private readonly BroadcastProfilesManager _profilesManager;
    private readonly IChannelInformationApplier _applier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StreamEpisodeNumberer> _logger;
    private readonly object _sync = new();
    private readonly List<IDisposable> _subscriptions;
    private DateTimeOffset? _lastOfflineAt;

    public StreamEpisodeNumberer(
        BroadcastProfilesStore profilesStore,
        BroadcastProfilesManager profilesManager,
        IChannelInformationApplier applier,
        IEventBus eventBus,
        TimeProvider timeProvider,
        ILogger<StreamEpisodeNumberer> logger)
    {
        _profilesStore = profilesStore;
        _profilesManager = profilesManager;
        _applier = applier;
        _timeProvider = timeProvider;
        _logger = logger;
        _subscriptions =
        [
            eventBus.Subscribe<StreamWentOnline>(this),
            eventBus.Subscribe<StreamWentOffline>(this),
        ];
    }

    public async Task HandleAsync(StreamWentOnline @event, CancellationToken cancellationToken)
    {
        if (@event.IsCatchUp)
        {
            _logger.LogDebug("StreamEpisodeNumberer: пропуск – catch-up Online");
            return;
        }

        var lastAppliedId = _profilesStore.Load().LastAppliedProfileId;
        if (lastAppliedId is null)
        {
            return;
        }

        var profile = _profilesManager.Find(lastAppliedId.Value);
        if (profile is null)
        {
            return;
        }

        var template = MessageTemplate.For(profile.Title);
        if (!template.Contains("n"))
        {
            return;
        }

        if (profile.LastApplyAt is null)
        {
            _logger.LogDebug("StreamEpisodeNumberer: пропуск – профиль ни разу не применялся вручную");
            return;
        }

        var now = _timeProvider.GetUtcNow();

        if (now - profile.LastApplyAt.Value < RecentApplyWindow)
        {
            _logger.LogDebug("StreamEpisodeNumberer: пропуск – недавний Apply ({Span})", now - profile.LastApplyAt.Value);
            return;
        }

        DateTimeOffset? lastOffline;
        lock (_sync)
        {
            lastOffline = _lastOfflineAt;
        }

        if (lastOffline is { } off && now - off < FlapCooldown)
        {
            _logger.LogDebug("StreamEpisodeNumberer: пропуск – флап стрима ({Span})", now - off);
            return;
        }

        var nextValue = profile.CurrentNumber + 1;
        var renderedTitle = template.With("n", nextValue.ToString()).Render();
        var success = await _applier.ApplyPatchAsync(renderedTitle, null, null, cancellationToken);

        if (!success)
        {
            _logger.LogWarning("StreamEpisodeNumberer: не удалось применить заголовок {Title}", renderedTitle);
            return;
        }

        _profilesManager.AdvanceCurrentNumber(profile.Id, profile.CurrentNumber, nextValue, now);
    }

    public Task HandleAsync(StreamWentOffline @event, CancellationToken cancellationToken)
    {
        if (@event.IsCatchUp)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            _lastOfflineAt = _timeProvider.GetUtcNow();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }
}
