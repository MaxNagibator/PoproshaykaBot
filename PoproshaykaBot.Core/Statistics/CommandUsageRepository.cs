using PoproshaykaBot.Core.Chat.Commands;

namespace PoproshaykaBot.Core.Statistics;

public sealed class CommandUsageRepository(TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, CommandUsageRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    private string _streamId = string.Empty;
    private bool _hasChanges;
    private bool _streamReset;

    public bool HasChanges
    {
        get
        {
            lock (_lock)
            {
                return _hasChanges;
            }
        }
    }

    public void Track(string canonical, string displayName)
    {
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return;
        }

        lock (_lock)
        {
            if (!_records.TryGetValue(canonical, out var record))
            {
                record = new()
                {
                    Canonical = canonical,
                };

                _records[canonical] = record;
            }

            record.TotalCount++;
            record.StreamCount++;
            record.LastUsedAt = timeProvider.GetUtcNow();
            record.LastUsedBy = displayName ?? string.Empty;
            _hasChanges = true;
        }
    }

    public bool ResetStreamCounters(string streamId)
    {
        lock (_lock)
        {
            var sameStream = !string.IsNullOrEmpty(streamId)
                             && string.Equals(streamId, _streamId, StringComparison.Ordinal);

            if (sameStream)
            {
                return false;
            }

            _streamId = streamId ?? string.Empty;
            _hasChanges = true;
            _streamReset = true;

            foreach (var record in _records.Values)
            {
                record.StreamCount = 0;
            }

            return true;
        }
    }

    public IReadOnlyList<CommandUsageRecord> GetSnapshot()
    {
        lock (_lock)
        {
            return CloneRecords();
        }
    }

    public void Replace(CommandUsageData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (_lock)
        {
            _records.Clear();

            foreach (var record in data.Commands ?? [])
            {
                if (record is null || string.IsNullOrWhiteSpace(record.Canonical))
                {
                    continue;
                }

                var loaded = record.Clone();

                if (_streamReset)
                {
                    loaded.StreamCount = 0;
                }

                _records[record.Canonical] = loaded;
            }

            CommandRenames.MoveRenamed(_records, static (record, canonical) => record.Canonical = canonical);

            if (!_streamReset)
            {
                _streamId = data.StreamId ?? string.Empty;
            }

            _hasChanges = _streamReset;
        }
    }

    public CommandUsageData CreateSnapshotAndMarkSaved()
    {
        lock (_lock)
        {
            var snapshot = new CommandUsageData
            {
                StreamId = _streamId,
            };

            snapshot.Commands.AddRange(CloneRecords());
            _hasChanges = false;
            return snapshot;
        }
    }

    public void MarkChanged()
    {
        lock (_lock)
        {
            _hasChanges = true;
        }
    }

    private List<CommandUsageRecord> CloneRecords()
    {
        return _records.Values
            .OrderBy(record => record.Canonical, StringComparer.OrdinalIgnoreCase)
            .Select(record => record.Clone())
            .ToList();
    }
}
