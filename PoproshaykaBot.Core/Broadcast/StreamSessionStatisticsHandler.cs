using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Infrastructure.Events.Statistics;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Broadcast;

public sealed class StreamSessionStatisticsHandler :
    IEventHandler<StreamWentOnline>,
    IEventHandler<StreamWentOffline>,
    IEventHandler<StreamMetadataResolved>,
    IEventHandler<ChatMessageReceived>,
    IEventHandler<ChannelUpdated>,
    IEventHandler<ChatIngestionStarted>,
    IEventHandler<ChatIngestionStopped>,
    IEventSubscriber,
    IAsyncDisposable
{
    private const string MissingValuePlaceholder = "–";
    private static readonly TimeSpan ViewerSampleInterval = TimeSpan.FromMinutes(1);

    internal static readonly TimeSpan StreamMatchTolerance = TimeSpan.FromMinutes(5);

    private readonly IChatMessenger _messenger;
    private readonly IStreamStatus _streamStatus;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly SettingsManager _settingsManager;
    private readonly TimeProvider _timeProvider;
    private readonly IEventBus _eventBus;
    private readonly ActiveStreamSessionStore _activeSessionStore;
    private readonly ILogger<StreamSessionStatisticsHandler> _logger;
    private readonly IDisposable _onlineSubscription;
    private readonly IDisposable _offlineSubscription;
    private readonly IDisposable _metadataResolvedSubscription;
    private readonly IDisposable _chatSubscription;
    private readonly IDisposable _channelUpdatedSubscription;
    private readonly IDisposable _ingestionStartedSubscription;
    private readonly IDisposable _ingestionStoppedSubscription;

    private readonly object _stateLock = new();
    private readonly Dictionary<string, ChatterTally> _chatters = new(StringComparer.Ordinal);
    private readonly List<SegmentTally> _segments = [];
    private readonly List<ActiveStreamSessionInterval> _tracked = [];
    private bool _trackedKnown;
    private DateTimeOffset? _ingestionSince;
    private string? _channel;
    private string? _streamId;
    private DateTimeOffset? _sessionStartedAt;

    private CancellationTokenSource? _samplingCts;
    private Task? _samplingTask;
    private bool _disposed;

    public StreamSessionStatisticsHandler(
        IChatMessenger messenger,
        IStreamStatus streamStatus,
        ITargetChannelProvider targetChannelProvider,
        SettingsManager settingsManager,
        TimeProvider timeProvider,
        IEventBus eventBus,
        ActiveStreamSessionStore activeSessionStore,
        ILogger<StreamSessionStatisticsHandler> logger)
    {
        _messenger = messenger;
        _streamStatus = streamStatus;
        _targetChannelProvider = targetChannelProvider;
        _settingsManager = settingsManager;
        _timeProvider = timeProvider;
        _eventBus = eventBus;
        _activeSessionStore = activeSessionStore;
        _logger = logger;

        _onlineSubscription = eventBus.Subscribe<StreamWentOnline>(this);
        _offlineSubscription = eventBus.Subscribe<StreamWentOffline>(this);
        _metadataResolvedSubscription = eventBus.Subscribe<StreamMetadataResolved>(this);
        _chatSubscription = eventBus.Subscribe<ChatMessageReceived>(this);
        _channelUpdatedSubscription = eventBus.Subscribe<ChannelUpdated>(this);
        _ingestionStartedSubscription = eventBus.Subscribe<ChatIngestionStarted>(this);
        _ingestionStoppedSubscription = eventBus.Subscribe<ChatIngestionStopped>(this);
    }

    public async Task HandleAsync(StreamWentOnline @event, CancellationToken cancellationToken)
    {
        if (!_targetChannelProvider.Current.RecordsUserData)
        {
            _logger.LogInformation("Статистика сессии не собирается – идёт отладка на чужом канале в общем профиле данных");
            return;
        }

        var startedAt = ResolveStartedAt(@event);
        var channel = string.IsNullOrEmpty(@event.Channel)
            ? _targetChannelProvider.Current.Login
            : @event.Channel;

        var streamId = NullIfEmpty(@event.Stream?.Id);
        var initialViewers = @event.Stream?.ViewerCount ?? 0;
        var initialTitle = NullIfEmpty(@event.Stream?.Title);
        var initialGame = NullIfEmpty(@event.Stream?.GameName);

        await StopSamplingLoopAsync().ConfigureAwait(false);

        var existingDraft = _activeSessionStore.Load();
        var usableDraft = existingDraft != null && IsUsableDraft(existingDraft);

        var resume = @event.IsCatchUp
                     && usableDraft
                     && IsSameStream(existingDraft!, channel, startedAt, streamId);

        if (!resume && usableDraft)
        {
            await FinalizeDraftAsync(existingDraft!, existingDraft!.UpdatedAt, "незакрытый черновик прошлого стрима", cancellationToken)
                .ConfigureAwait(false);
        }

        lock (_stateLock)
        {
            _channel = channel;
            _chatters.Clear();
            _segments.Clear();
            _tracked.Clear();
            _trackedKnown = !resume || existingDraft!.TrackedIntervals != null;

            if (resume)
            {
                _sessionStartedAt = existingDraft!.StartedAt;
                _streamId = streamId ?? existingDraft.StreamId;
                _segments.AddRange(RestoreSegments(existingDraft));
                _tracked.AddRange(RestoreTracked(existingDraft, existingDraft.UpdatedAt));

                foreach (var chatter in existingDraft.Chatters)
                {
                    _chatters[chatter.UserId] = new()
                    {
                        DisplayName = chatter.DisplayName,
                        MessageCount = chatter.MessageCount,
                    };
                }

                ApplyMetadata(initialTitle, initialGame);

                var current = _segments[^1];

                if (initialViewers > current.PeakViewers)
                {
                    current.PeakViewers = initialViewers;
                }
            }
            else
            {
                _sessionStartedAt = startedAt;
                _streamId = streamId;
                _segments.Add(new()
                {
                    StartedAt = startedAt,
                    Title = initialTitle,
                    Game = initialGame,
                    PeakViewers = initialViewers,
                    ViewerSamplesSum = initialViewers,
                    ViewerSamplesCount = initialViewers > 0 ? 1 : 0,
                });
            }

            if (_ingestionSince is { } ingestionSince)
            {
                OpenTracked(ingestionSince);
            }
        }

        Checkpoint();
        StartSamplingLoop();

        if (resume)
        {
            _logger.LogInformation("Сбор статистики сессии возобновлён из черновика (старт {StartedAt}, сообщений {Messages}, чаттеров {Chatters})",
                startedAt,
                existingDraft!.MessageCount,
                existingDraft.Chatters.Count);
        }
        else
        {
            _logger.LogDebug("Сбор статистики сессии запущен (старт {StartedAt}, catch-up {IsCatchUp}, начальные зрители {Viewers})",
                startedAt,
                @event.IsCatchUp,
                initialViewers);
        }
    }

    public Task HandleAsync(ChatMessageReceived @event, CancellationToken cancellationToken)
    {
        if (@event.IsBot || !_targetChannelProvider.Current.RecordsUserData)
        {
            return Task.CompletedTask;
        }

        lock (_stateLock)
        {
            if (_sessionStartedAt == null)
            {
                return Task.CompletedTask;
            }

            _segments[^1].MessageCount++;

            if (!string.IsNullOrEmpty(@event.UserId))
            {
                if (!_chatters.TryGetValue(@event.UserId, out var tally))
                {
                    tally = new();
                    _chatters[@event.UserId] = tally;
                }

                tally.MessageCount++;

                var displayName = FirstNonEmpty(@event.DisplayName, @event.Username);

                if (displayName != null)
                {
                    tally.DisplayName = displayName;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(ChannelUpdated @event, CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_sessionStartedAt == null)
            {
                return Task.CompletedTask;
            }

            ApplyMetadata(@event.Title, @event.GameName);
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(ChatIngestionStarted @event, CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_ingestionSince != null)
            {
                return Task.CompletedTask;
            }

            _ingestionSince = @event.At;

            if (_sessionStartedAt == null)
            {
                return Task.CompletedTask;
            }

            OpenTracked(@event.At);
        }

        Checkpoint();

        return Task.CompletedTask;
    }

    public Task HandleAsync(ChatIngestionStopped @event, CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_ingestionSince is not { } since || @event.At < since)
            {
                return Task.CompletedTask;
            }

            _ingestionSince = null;

            if (_sessionStartedAt == null)
            {
                return Task.CompletedTask;
            }

            foreach (var interval in _tracked)
            {
                interval.EndedAt ??= @event.At;
            }
        }

        Checkpoint();

        return Task.CompletedTask;
    }

    public Task HandleAsync(StreamMetadataResolved @event, CancellationToken cancellationToken)
    {
        bool identityChanged;

        lock (_stateLock)
        {
            if (_sessionStartedAt == null || IsForeignStream(@event.Stream))
            {
                return Task.CompletedTask;
            }

            identityChanged = AdoptCanonicalIdentity(@event.Stream);

            ApplyMetadata(@event.Stream.Title, @event.Stream.GameName);

            var current = _segments[^1];

            if (@event.Stream.ViewerCount > current.PeakViewers)
            {
                current.PeakViewers = @event.Stream.ViewerCount;
            }
        }

        if (identityChanged)
        {
            Checkpoint();
        }

        return Task.CompletedTask;
    }

    public async Task HandleAsync(StreamWentOffline @event, CancellationToken cancellationToken)
    {
        if (@event.IsCatchUp)
        {
            await HandleCatchUpOfflineAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await StopSamplingLoopAsync().ConfigureAwait(false);

        var endedAt = _timeProvider.GetUtcNow();
        var record = TakeMemorySession(@event.Channel, endedAt);

        if (record == null)
        {
            var orphan = _activeSessionStore.Load();

            if (orphan != null && IsUsableDraft(orphan))
            {
                await FinalizeDraftAsync(orphan, endedAt, "черновик без активной сессии в памяти", cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _activeSessionStore.Delete();
            }

            _logger.LogDebug("Офлайн без активной сессии в памяти – статистика собрана из черновика (если он был)");
            return;
        }

        await _eventBus.PublishAsync(new StreamSessionCompleted(record), cancellationToken).ConfigureAwait(false);

        _activeSessionStore.Delete();

        var duration = record.Duration;

        _logger.LogInformation("Сессия стрима канала {Channel} завершена и сохранена: длительность {Duration}, сегментов {Segments}, сообщений {Messages}, чаттеров {Chatters}, пик {Peak}, средний {Avg}",
            @event.Channel,
            duration,
            record.Segments.Count,
            record.MessageCount,
            record.ChatterCount,
            record.PeakViewers,
            record.AverageViewers);

        var settings = _settingsManager.Current.Twitch.AutoBroadcast;

        if (!settings.StreamStatusNotificationsEnabled
            || string.IsNullOrWhiteSpace(settings.StreamEndStatsMessage))
        {
            return;
        }

        var message = MessageTemplate.For(settings.StreamEndStatsMessage)
            .With("duration", FormattingUtils.FormatTimeSpan(duration))
            .With("messages", FormattingUtils.FormatNumber(record.MessageCount))
            .With("chatters", FormattingUtils.FormatNumber(record.ChatterCount))
            .With("peakViewers", FormattingUtils.FormatNumber(record.PeakViewers))
            .With("avgViewers", FormattingUtils.FormatNumber(record.AverageViewers))
            .With("title", record.Title ?? MissingValuePlaceholder)
            .With("game", record.Game ?? MissingValuePlaceholder)
            .With("channel", @event.Channel)
            .Render();

        _logger.LogInformation("Отправка итоговой статистики стрима для канала {Channel}: длительность {Duration}, сообщений {Messages}, активных зрителей {Chatters}, пик {Peak}, средний {Avg}, игра \"{Game}\"",
            @event.Channel,
            duration,
            record.MessageCount,
            record.ChatterCount,
            record.PeakViewers,
            record.AverageViewers,
            record.Game);

        _messenger.Send(message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _onlineSubscription.Dispose();
        _offlineSubscription.Dispose();
        _metadataResolvedSubscription.Dispose();
        _chatSubscription.Dispose();
        _channelUpdatedSubscription.Dispose();
        _ingestionStartedSubscription.Dispose();
        _ingestionStoppedSubscription.Dispose();

        try
        {
            await StopSamplingLoopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Ошибка остановки цикла семплирования зрителей при DisposeAsync");
        }

        Checkpoint();
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string? FirstNonEmpty(string? primary, string? fallback)
    {
        return NullIfEmpty(primary) ?? NullIfEmpty(fallback);
    }

    private static bool IsUsableDraft(ActiveStreamSession draft)
    {
        return draft.StartedAt != default && !string.IsNullOrEmpty(draft.Channel);
    }

    private static bool IsSameStream(ActiveStreamSession draft, string channel, DateTimeOffset startedAt, string? streamId)
    {
        if (!string.Equals(draft.Channel, channel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(streamId) && !string.IsNullOrEmpty(draft.StreamId))
        {
            return string.Equals(draft.StreamId, streamId, StringComparison.Ordinal);
        }

        return (draft.StartedAt - startedAt).Duration() <= StreamMatchTolerance;
    }

    private static List<StreamSessionChatter> SortChatters(IEnumerable<StreamSessionChatter> chatters)
    {
        return chatters
            .OrderByDescending(chatter => chatter.MessageCount)
            .ThenBy(chatter => chatter.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int AverageViewers(long samplesSum, int samplesCount)
    {
        return samplesCount > 0
            ? (int)Math.Round((double)samplesSum / samplesCount, MidpointRounding.AwayFromZero)
            : 0;
    }

    private static List<SegmentTally> RestoreSegments(ActiveStreamSession draft)
    {
        if (draft.Segments.Count > 0)
        {
            return draft.Segments
                .Select(segment => new SegmentTally
                {
                    StartedAt = segment.StartedAt,
                    EndedAt = segment.EndedAt,
                    Title = segment.Title,
                    Game = segment.Game,
                    MessageCount = segment.MessageCount,
                    PeakViewers = segment.PeakViewers,
                    ViewerSamplesSum = segment.ViewerSamplesSum,
                    ViewerSamplesCount = segment.ViewerSamplesCount,
                })
                .ToList();
        }

        return
        [
            new()
            {
                StartedAt = draft.StartedAt,
                Title = draft.Title,
                Game = draft.Game,
                MessageCount = draft.MessageCount,
                PeakViewers = draft.PeakViewers,
                ViewerSamplesSum = draft.ViewerSamplesSum,
                ViewerSamplesCount = draft.ViewerSamplesCount,
            },
        ];
    }

    private static List<ActiveStreamSessionInterval> RestoreTracked(ActiveStreamSession draft, DateTimeOffset closeOpenAt)
    {
        if (draft.TrackedIntervals == null)
        {
            return [];
        }

        return draft.TrackedIntervals
            .OfType<ActiveStreamSessionInterval>()
            .Select(interval => new ActiveStreamSessionInterval
            {
                StartedAt = interval.StartedAt,
                EndedAt = interval.EndedAt ?? (closeOpenAt > interval.StartedAt ? closeOpenAt : interval.StartedAt),
            })
            .ToList();
    }

    private static List<StreamSessionInterval>? ToIntervals(bool known, IEnumerable<ActiveStreamSessionInterval> tracked, DateTimeOffset closeOpenAt)
    {
        if (!known)
        {
            return null;
        }

        return tracked
            .Select(interval => new StreamSessionInterval
            {
                StartedAt = interval.StartedAt,
                EndedAt = interval.EndedAt ?? closeOpenAt,
            })
            .ToList();
    }

    private static StreamSessionRecord BuildRecord(
        string channel,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        int chatterCount,
        List<StreamSessionChatter> chatters,
        IReadOnlyList<SegmentTally> segments,
        List<StreamSessionInterval>? tracked)
    {
        var normalizedEnd = endedAt < startedAt ? startedAt : endedAt;

        var resultSegments = new List<StreamSessionSegment>(segments.Count);
        long totalMessages = 0;
        var peakViewers = 0;
        long viewerSamplesSum = 0;
        var viewerSamplesCount = 0;

        foreach (var segment in segments)
        {
            var segmentEnd = segment.EndedAt ?? normalizedEnd;

            if (segmentEnd < segment.StartedAt)
            {
                segmentEnd = segment.StartedAt;
            }

            resultSegments.Add(new()
            {
                StartedAt = segment.StartedAt,
                EndedAt = segmentEnd,
                Title = segment.Title,
                Game = segment.Game,
                MessageCount = segment.MessageCount,
                PeakViewers = segment.PeakViewers,
                AverageViewers = AverageViewers(segment.ViewerSamplesSum, segment.ViewerSamplesCount),
            });

            totalMessages += segment.MessageCount;
            peakViewers = Math.Max(peakViewers, segment.PeakViewers);
            viewerSamplesSum += segment.ViewerSamplesSum;
            viewerSamplesCount += segment.ViewerSamplesCount;
        }

        var lastSegment = segments.Count > 0 ? segments[^1] : null;

        return new()
        {
            Channel = channel,
            StartedAt = startedAt,
            EndedAt = normalizedEnd,
            Title = lastSegment?.Title,
            Game = lastSegment?.Game,
            MessageCount = totalMessages,
            ChatterCount = chatterCount,
            PeakViewers = peakViewers,
            AverageViewers = AverageViewers(viewerSamplesSum, viewerSamplesCount),
            Chatters = chatters,
            Segments = resultSegments,
            TrackedIntervals = tracked == null ? null : StreamSessionInterval.Normalize(tracked, startedAt, normalizedEnd),
        };
    }

    private static StreamSessionRecord ToRecord(ActiveStreamSession draft, DateTimeOffset endedAt)
    {
        var segments = RestoreSegments(draft);

        segments[^1].EndedAt ??= endedAt;

        var chatters = SortChatters(draft.Chatters.Select(chatter => new StreamSessionChatter
        {
            UserId = chatter.UserId,
            DisplayName = string.IsNullOrEmpty(chatter.DisplayName) ? chatter.UserId : chatter.DisplayName,
            MessageCount = chatter.MessageCount,
        }));

        var lastSeenAlive = draft.UpdatedAt < endedAt ? draft.UpdatedAt : endedAt;
        var tracked = ToIntervals(draft.TrackedIntervals != null, RestoreTracked(draft, lastSeenAlive), lastSeenAlive);

        return BuildRecord(draft.Channel, draft.StartedAt, endedAt, draft.Chatters.Count, chatters, segments, tracked);
    }

    private void OpenTracked(DateTimeOffset at)
    {
        if (_tracked.Count > 0 && _tracked[^1].EndedAt == null)
        {
            return;
        }

        _tracked.Add(new() { StartedAt = at });
    }

    private void ApplyMetadata(string? title, string? game)
    {
        var current = _segments[^1];

        var nextGame = string.IsNullOrEmpty(game) ? current.Game : game;
        var nextTitle = string.IsNullOrEmpty(title) ? current.Title : title;

        var gameChanged = !string.IsNullOrEmpty(game)
                          && !string.IsNullOrEmpty(current.Game)
                          && !string.Equals(current.Game, game, StringComparison.OrdinalIgnoreCase);

        var titleChanged = !string.IsNullOrEmpty(title)
                           && !string.IsNullOrEmpty(current.Title)
                           && !string.Equals(current.Title, title, StringComparison.Ordinal);

        if (!gameChanged && !titleChanged)
        {
            current.Game = nextGame;
            current.Title = nextTitle;
            return;
        }

        var now = _timeProvider.GetUtcNow();
        current.EndedAt = now;

        _segments.Add(new()
        {
            StartedAt = now,
            Title = nextTitle,
            Game = nextGame,
        });

        if (gameChanged && titleChanged)
        {
            _logger.LogInformation("Категория и название стрима изменены на \"{Game}\" и \"{Title}\" – открыт новый сегмент статистики (всего сегментов: {Count})",
                nextGame,
                nextTitle,
                _segments.Count);
        }
        else if (gameChanged)
        {
            _logger.LogInformation("Категория стрима изменена на \"{Game}\" – открыт новый сегмент статистики (всего сегментов: {Count})",
                nextGame,
                _segments.Count);
        }
        else
        {
            _logger.LogInformation("Название стрима изменено на \"{Title}\" – открыт новый сегмент статистики (всего сегментов: {Count})",
                nextTitle,
                _segments.Count);
        }
    }

    private bool IsForeignStream(StreamInfo stream)
    {
        var streamId = NullIfEmpty(stream.Id);

        if (streamId == null || _streamId == null || string.Equals(_streamId, streamId, StringComparison.Ordinal))
        {
            return false;
        }

        _logger.LogWarning("Метаданные относятся к другому стриму ({ResolvedId} вместо {SessionId}) – в статистику сессии не попадают",
            streamId,
            _streamId);

        return true;
    }

    private bool AdoptCanonicalIdentity(StreamInfo stream)
    {
        var streamId = NullIfEmpty(stream.Id);
        var changed = streamId != null && _streamId == null;

        if (changed)
        {
            _streamId = streamId;
        }

        if (stream.StartedAt == default)
        {
            return changed;
        }

        var canonical = new DateTimeOffset(DateTime.SpecifyKind(stream.StartedAt, DateTimeKind.Utc));

        if (canonical == _sessionStartedAt)
        {
            return changed;
        }

        _logger.LogInformation("Начало сессии статистики уточнено по данным Twitch: {PreviousStart} → {CanonicalStart}",
            _sessionStartedAt,
            canonical);

        _segments[0].StartedAt = canonical;
        _sessionStartedAt = canonical;

        return true;
    }

    private DateTimeOffset ResolveStartedAt(StreamWentOnline @event)
    {
        return @event.Stream?.StartedAt is { } streamStart && streamStart != default
            ? new(DateTime.SpecifyKind(streamStart, DateTimeKind.Utc))
            : _timeProvider.GetUtcNow();
    }

    private Task HandleCatchUpOfflineAsync(CancellationToken cancellationToken)
    {
        bool hasMemorySession;

        lock (_stateLock)
        {
            hasMemorySession = _sessionStartedAt != null;
        }

        if (hasMemorySession)
        {
            _logger.LogDebug("Catch-up offline при активной сессии в памяти – событие проигнорировано");
            return Task.CompletedTask;
        }

        var orphan = _activeSessionStore.Load();

        if (orphan != null && IsUsableDraft(orphan))
        {
            return FinalizeDraftAsync(orphan, orphan.UpdatedAt, "стрим завершился во время простоя бота", cancellationToken);
        }

        _activeSessionStore.Delete();

        return Task.CompletedTask;
    }

    private async Task FinalizeDraftAsync(ActiveStreamSession draft, DateTimeOffset endedAt, string reason, CancellationToken cancellationToken)
    {
        var record = ToRecord(draft, endedAt);

        await _eventBus.PublishAsync(new StreamSessionCompleted(record), cancellationToken).ConfigureAwait(false);
        _activeSessionStore.Delete();

        _logger.LogInformation("Восстановлена сессия стрима из черновика ({Reason}): канал {Channel}, сообщений {Messages}, чаттеров {Chatters}, сегментов {Segments}, длительность {Duration}",
            reason,
            record.Channel,
            record.MessageCount,
            record.ChatterCount,
            record.Segments.Count,
            record.Duration);
    }

    private StreamSessionRecord? TakeMemorySession(string channel, DateTimeOffset endedAt)
    {
        lock (_stateLock)
        {
            if (_sessionStartedAt == null)
            {
                return null;
            }

            if (_segments[^1].EndedAt == null)
            {
                _segments[^1].EndedAt = endedAt;
            }

            var chatters = SortChatters(_chatters.Select(pair => new StreamSessionChatter
            {
                UserId = pair.Key,
                DisplayName = string.IsNullOrEmpty(pair.Value.DisplayName) ? pair.Key : pair.Value.DisplayName,
                MessageCount = pair.Value.MessageCount,
            }));

            var tracked = ToIntervals(_trackedKnown, _tracked, endedAt);
            var record = BuildRecord(channel, _sessionStartedAt.Value, endedAt, _chatters.Count, chatters, _segments, tracked);

            _channel = null;
            _streamId = null;
            _sessionStartedAt = null;
            _chatters.Clear();
            _segments.Clear();
            _tracked.Clear();

            return record;
        }
    }

    private ActiveStreamSession? CreateCheckpointSnapshot()
    {
        lock (_stateLock)
        {
            if (_sessionStartedAt == null || _channel == null)
            {
                return null;
            }

            var segments = _segments
                .Select(segment => new ActiveStreamSessionSegment
                {
                    StartedAt = segment.StartedAt,
                    EndedAt = segment.EndedAt,
                    Title = segment.Title,
                    Game = segment.Game,
                    MessageCount = segment.MessageCount,
                    PeakViewers = segment.PeakViewers,
                    ViewerSamplesSum = segment.ViewerSamplesSum,
                    ViewerSamplesCount = segment.ViewerSamplesCount,
                })
                .ToList();

            var lastSegment = _segments[^1];

            return new()
            {
                Channel = _channel,
                StreamId = _streamId,
                StartedAt = _sessionStartedAt.Value,
                UpdatedAt = _timeProvider.GetUtcNow(),
                MessageCount = _segments.Sum(segment => segment.MessageCount),
                PeakViewers = _segments.Max(segment => segment.PeakViewers),
                ViewerSamplesSum = _segments.Sum(segment => segment.ViewerSamplesSum),
                ViewerSamplesCount = _segments.Sum(segment => segment.ViewerSamplesCount),
                Title = lastSegment.Title,
                Game = lastSegment.Game,
                Chatters = _chatters
                    .Select(pair => new ActiveStreamSessionChatter
                    {
                        UserId = pair.Key,
                        DisplayName = pair.Value.DisplayName,
                        MessageCount = pair.Value.MessageCount,
                    })
                    .ToList(),
                Segments = segments,
                TrackedIntervals = _trackedKnown
                    ? _tracked.Select(interval => new ActiveStreamSessionInterval
                        {
                            StartedAt = interval.StartedAt,
                            EndedAt = interval.EndedAt,
                        })
                        .ToList()
                    : null,
            };
        }
    }

    private void Checkpoint()
    {
        var snapshot = CreateCheckpointSnapshot();

        if (snapshot == null)
        {
            return;
        }

        try
        {
            _activeSessionStore.Save(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось сохранить чекпойнт активной сессии стрима: канал {Channel}, стрим {StreamId}",
                snapshot.Channel,
                snapshot.StreamId);
        }
    }

    private void StartSamplingLoop()
    {
        lock (_stateLock)
        {
            var newCts = new CancellationTokenSource();
            var token = newCts.Token;
            _samplingCts?.Dispose();
            _samplingCts = newCts;
            _samplingTask = Task.Run(() => RunSamplingLoopAsync(token), token);
        }
    }

    private async Task StopSamplingLoopAsync()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_stateLock)
        {
            cts = _samplingCts;
            task = _samplingTask;
            _samplingCts = null;
            _samplingTask = null;
        }

        if (cts != null)
        {
            try
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // already disposed
            }
        }

        if (task != null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Цикл семплирования зрителей завершился с ошибкой");
            }
        }

        cts?.Dispose();
    }

    private async Task RunSamplingLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ViewerSampleInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                SampleStreamSnapshot();
                Checkpoint();
            }
        }
        catch (OperationCanceledException)
        {
            // expected when sampling loop is cancelled
        }
    }

    private void SampleStreamSnapshot()
    {
        var snapshot = _streamStatus.CurrentStream;

        if (snapshot == null)
        {
            return;
        }

        lock (_stateLock)
        {
            if (_sessionStartedAt == null)
            {
                return;
            }

            ApplyMetadata(snapshot.Title, snapshot.GameName);

            var current = _segments[^1];

            if (snapshot.ViewerCount > current.PeakViewers)
            {
                current.PeakViewers = snapshot.ViewerCount;
            }

            // TODO: нулевой замер отбрасывается, чтобы голый снимок из stream.online не занижал среднее; стрим с настоящим нулём зрителей замеров не получит – при признаке источника в StreamInfo фильтровать по нему
            if (snapshot.ViewerCount > 0)
            {
                current.ViewerSamplesSum += snapshot.ViewerCount;
                current.ViewerSamplesCount++;
            }
        }
    }

    private sealed class SegmentTally
    {
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public string? Title { get; set; }
        public string? Game { get; set; }
        public long MessageCount { get; set; }
        public int PeakViewers { get; set; }
        public long ViewerSamplesSum { get; set; }
        public int ViewerSamplesCount { get; set; }
    }

    private sealed class ChatterTally
    {
        public string DisplayName { get; set; } = string.Empty;
        public long MessageCount { get; set; }
    }
}
