using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Statistics;

public sealed class StreamSessionInterval
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => EndedAt > StartedAt ? EndedAt - StartedAt : TimeSpan.Zero;

    public static List<StreamSessionInterval> Normalize(
        IEnumerable<StreamSessionInterval> intervals,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        var merged = new List<StreamSessionInterval>();

        var clipped = intervals
            .OfType<StreamSessionInterval>()
            .Select(interval => (StartedAt: Max(interval.StartedAt, from), EndedAt: Min(interval.EndedAt, to)))
            .Where(interval => interval.EndedAt > interval.StartedAt)
            .OrderBy(interval => interval.StartedAt);

        foreach (var (startedAt, endedAt) in clipped)
        {
            if (merged.Count > 0 && startedAt <= merged[^1].EndedAt)
            {
                merged[^1].EndedAt = Max(merged[^1].EndedAt, endedAt);
                continue;
            }

            merged.Add(new() { StartedAt = startedAt, EndedAt = endedAt });
        }

        return merged;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}
