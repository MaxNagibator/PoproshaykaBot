using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Wpf.ViewModels;

public enum StreamRecordKind
{
    None = 0,
    Duration = 1,
    PeakViewers = 2,
    Messages = 3,
    Chatters = 4,
}

public sealed record StreamCategoryStat(string Game, TimeSpan AirTime, int SessionCount, double Share);

public sealed record StreamRecordStat(StreamRecordKind Kind, StreamSessionRecord Session, long Value);

public sealed record StreamHistorySummary(
    IReadOnlyList<StreamCategoryStat> Categories,
    IReadOnlyList<StreamRecordStat> Records)
{
    public const int MaxCategories = 5;
    public const int MinSessionsForRecords = 2;

    public static readonly StreamHistorySummary Empty = new([], []);

    public static StreamHistorySummary Build(IReadOnlyList<StreamSessionRecord> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        if (sessions.Count == 0)
        {
            return Empty;
        }

        return new(BuildCategories(sessions), BuildRecords(sessions));
    }

    private static List<StreamCategoryStat> BuildCategories(IReadOnlyList<StreamSessionRecord> sessions)
    {
        var totals = new Dictionary<string, CategoryAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions)
        {
            foreach (var segment in session.Segments)
            {
                if (segment.Game is not { Length: > 0 } game)
                {
                    continue;
                }

                if (!totals.TryGetValue(game, out var accumulator))
                {
                    accumulator = new(game);
                    totals.Add(game, accumulator);
                }

                accumulator.AirTime += segment.Duration;
                accumulator.Sessions.Add(session.Id);
            }
        }

        if (totals.Count == 0)
        {
            return [];
        }

        var totalAirTime = totals.Values.Aggregate(TimeSpan.Zero, (sum, item) => sum + item.AirTime);

        return totals.Values
            .OrderByDescending(item => item.AirTime)
            .ThenByDescending(item => item.Sessions.Count)
            .ThenBy(item => item.Game, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxCategories)
            .Select(item => new StreamCategoryStat(
                item.Game,
                item.AirTime,
                item.Sessions.Count,
                totalAirTime > TimeSpan.Zero ? item.AirTime / totalAirTime : 0))
            .ToList();
    }

    private static List<StreamRecordStat> BuildRecords(IReadOnlyList<StreamSessionRecord> sessions)
    {
        if (sessions.Count < MinSessionsForRecords)
        {
            return [];
        }

        var records = new List<StreamRecordStat>(4);

        Add(StreamRecordKind.Duration, session => session.Duration.Ticks);
        Add(StreamRecordKind.PeakViewers, session => session.PeakViewers);
        Add(StreamRecordKind.Messages, session => session.MessageCount);
        Add(StreamRecordKind.Chatters, session => session.ChatterCount);

        return records;

        void Add(StreamRecordKind kind, Func<StreamSessionRecord, long> value)
        {
            StreamSessionRecord? leader = null;
            var leaderValue = 0L;

            foreach (var session in sessions)
            {
                var current = value(session);

                if (current <= 0)
                {
                    continue;
                }

                if (leader is null || current > leaderValue || (current == leaderValue && session.StartedAt > leader.StartedAt))
                {
                    leader = session;
                    leaderValue = current;
                }
            }

            if (leader is not null)
            {
                records.Add(new(kind, leader, leaderValue));
            }
        }
    }

    private sealed class CategoryAccumulator(string game)
    {
        public string Game { get; } = game;
        public TimeSpan AirTime { get; set; }
        public HashSet<Guid> Sessions { get; } = [];
    }
}
