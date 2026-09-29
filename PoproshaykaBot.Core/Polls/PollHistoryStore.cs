using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PoproshaykaBot.Core.Polls;

public sealed class PollHistoryStore(
    PollsStore pollsStore,
    [FromKeyedServices(TwitchEndpoints.HelixBroadcasterClient)]
    ITwitchHelixClient helix,
    IBroadcasterIdProvider broadcasterIdProvider,
    ITargetChannelProvider targetChannelProvider,
    ILogger<PollHistoryStore> logger,
    string? filePath = null)
    : IHostedComponent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { JsonStoreNullDefaults.Apply },
        },
    };

    private readonly string _filePath = filePath ?? AppPaths.Combine("polls-history.json");

    private readonly List<PollHistoryEntry> _entries = [];
    private readonly object _sync = new();
    private CancellationTokenSource? _backgroundCts;
    private Task? _backgroundTask;
    private StatisticsLoadState _readState = StatisticsLoadState.NotRead;
    private bool _canPersist;
    private bool _rewrittenExternally;
    private bool _hasUnsavedChanges;

    public string Name => "История голосований";

    public int StartOrder => 120;

    public bool IsLoaded
    {
        get
        {
            lock (_sync)
            {
                return _canPersist;
            }
        }
    }

    public static PollHistoryEntry BuildEntry(PollSnapshot snapshot, PollChoiceSnapshot? winner, bool winnerIsTie)
    {
        var ended = snapshot.EndedAtUtc ?? snapshot.EndsAtUtc;

        return new()
        {
            PollId = snapshot.PollId,
            SourceProfileId = snapshot.SourceProfileId,
            Title = snapshot.Title,
            FinalChoices = snapshot.Choices
                .Select(c => new PollHistoryChoice
                {
                    ChoiceId = c.ChoiceId,
                    Title = c.Title,
                    Votes = c.Votes,
                    ChannelPointsVotes = c.ChannelPointsVotes,
                    BitsVotes = c.BitsVotes,
                })
                .ToList(),
            StartedAtUtc = snapshot.StartedAtUtc,
            EndedAtUtc = ended,
            FinalStatus = snapshot.Status,
            WinnerChoiceId = winner?.ChoiceId,
            WinnerIsTie = winnerIsTie,
        };
    }

    public IReadOnlyList<PollHistoryEntry> GetAll()
    {
        lock (_sync)
        {
            EnsureLoadedUnderLock();
            return _entries.ToList();
        }
    }

    public bool TryAdd(PollHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            EnsureLoadedUnderLock();

            if (_entries.Any(e => string.Equals(e.PollId, entry.PollId, StringComparison.Ordinal)))
            {
                return false;
            }

            _entries.Add(entry);
            TruncateToMax();
            _hasUnsavedChanges = true;

            if (!_canPersist)
            {
                logger.LogWarning(
                    "PollHistoryStore: {Reason} ({FilePath}), файл не переписывается – запись опроса {PollId} остаётся в памяти и попадёт в файл, только если право записи вернётся до перезапуска",
                    DescribeLostWriteRight(),
                    _filePath,
                    entry.PollId);

                return true;
            }

            PersistNoThrow();
            return true;
        }
    }

    public bool Invalidate()
    {
        lock (_sync)
        {
            EnsureLoadedUnderLock();

            if (!_canPersist)
            {
                return false;
            }

            _canPersist = false;
            _rewrittenExternally = true;

            logger.LogInformation(
                "PollHistoryStore: файл {FilePath} меняют мимо приложения, история голосований этого сеанса до перезапуска не сохраняется",
                _filePath);

            return true;
        }
    }

    public void Restore(bool wasLoaded)
    {
        if (!wasLoaded)
        {
            return;
        }

        lock (_sync)
        {
            _canPersist = true;
            _rewrittenExternally = false;

            logger.LogInformation("PollHistoryStore: файл {FilePath} никто не менял, история голосований снова сохраняется", _filePath);

            FlushUnsavedChanges();
        }
    }

    public bool TryFlush()
    {
        lock (_sync)
        {
            return _canPersist ? FlushUnsavedChanges() : !_hasUnsavedChanges;
        }
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken)
    {
        EnsureLoaded();

        if (targetChannelProvider.Current.IsForeign)
        {
            logger.LogInformation("PollHistoryStore: бэкфилл пропущен – идёт отладка на чужом канале");
            return 0;
        }

        try
        {
            var broadcasterId = await broadcasterIdProvider.GetAsync(cancellationToken);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                return 0;
            }

            var polls = await helix.GetPollsAsync(broadcasterId, 20, cancellationToken);
            var added = 0;

            foreach (var poll in polls)
            {
                if (poll.EndedAt is null)
                {
                    continue;
                }

                var snapshot = PollEventSubMapper.FromHelix(poll, null);

                if (snapshot is null)
                {
                    logger.LogWarning("PollHistoryStore: опрос {PollId} – неизвестный статус {Status}, пропущен",
                        poll.Id, poll.Status);

                    continue;
                }

                if (snapshot.Status == PollSnapshotStatus.Active)
                {
                    continue;
                }

                var (winner, isTie) = PollEventSubMapper.DetectWinner(snapshot);
                var entry = BuildEntry(snapshot, winner, isTie);

                if (TryAdd(entry))
                {
                    added++;
                }
            }

            return added;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PollHistoryStore: бэкфилл не выполнен");
            return 0;
        }
    }

    public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        EnsureLoaded();

        _backgroundCts?.Dispose();
        _backgroundCts = new();
        var token = _backgroundCts.Token;
        _backgroundTask = Task.Run(() => BackfillAsync(token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_backgroundCts is not null)
        {
            await _backgroundCts.CancelAsync();
        }

        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "PollHistoryStore: фоновый бэкфилл завершился с ошибкой при остановке");
            }

            _backgroundTask = null;
        }

        _backgroundCts?.Dispose();
        _backgroundCts = null;
    }

    private void EnsureLoaded()
    {
        lock (_sync)
        {
            EnsureLoadedUnderLock();
        }
    }

    private void EnsureLoadedUnderLock()
    {
        if (_readState != StatisticsLoadState.NotRead)
        {
            return;
        }

        try
        {
            if (!File.Exists(_filePath))
            {
                _readState = StatisticsLoadState.Loaded;
                _canPersist = true;
                return;
            }

            var json = File.ReadAllText(_filePath);
            var file = JsonSerializer.Deserialize<HistoryFile>(json, JsonOptions);

            if (file is null)
            {
                logger.LogError("PollHistoryStore: файл {FilePath} разобран в пустое значение, история голосований этого сеанса не сохраняется", _filePath);
                Abandon();
                return;
            }

            if (file.Entries is { Count: > 0 })
            {
                _entries.AddRange(file.Entries);
            }

            _readState = StatisticsLoadState.Loaded;
            _canPersist = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PollHistoryStore: ошибка чтения {FilePath}, история голосований этого сеанса не сохраняется", _filePath);
            Abandon();
        }
    }

    private void Abandon()
    {
        _readState = StatisticsLoadState.Abandoned;
        _entries.Clear();

        JsonStoreBackup.CreateBackup(_filePath, "invalid", logger);
    }

    private string DescribeLostWriteRight()
    {
        return _rewrittenExternally ? "файл переписан мимо приложения" : "чтение файла сорвалось";
    }

    private bool FlushUnsavedChanges()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        try
        {
            Persist();
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "PollHistoryStore: не удалось записать {FilePath} – несохранённое осталось в памяти до следующей попытки",
                _filePath);

            return false;
        }

        logger.LogInformation("PollHistoryStore: несохранённое дописано в {FilePath} (всего записей: {EntryCount})",
            _filePath,
            _entries.Count);

        return true;
    }

    private void TruncateToMax()
    {
        var max = Math.Max(1, pollsStore.Load().HistoryMaxItems);

        if (_entries.Count <= max)
        {
            return;
        }

        _entries.RemoveRange(0, _entries.Count - max);
    }

    private void PersistNoThrow()
    {
        try
        {
            Persist();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PollHistoryStore: не удалось сохранить историю – несохранённое осталось в памяти до ближайшей попытки записи");
        }
    }

    private void Persist()
    {
        var payload = new HistoryFile(1, _entries);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        AtomicFile.Save(_filePath, json, logger);
        _hasUnsavedChanges = false;
    }

    private sealed record HistoryFile(int Version, List<PollHistoryEntry> Entries);
}
