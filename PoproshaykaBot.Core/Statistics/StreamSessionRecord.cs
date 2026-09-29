using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Statistics;

public sealed class StreamSessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public string? Title { get; set; }
    public string? Game { get; set; }
    public long MessageCount { get; set; }
    public int ChatterCount { get; set; }
    public int PeakViewers { get; set; }
    public int AverageViewers { get; set; }
    public bool IsHidden { get; set; }
    public List<StreamSessionChatter> Chatters { get; set; } = [];

    public List<StreamSessionSegment> Segments { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StreamSessionInterval>? TrackedIntervals { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => EndedAt > StartedAt ? EndedAt - StartedAt : TimeSpan.Zero;

    [JsonIgnore]
    public TimeSpan? TrackedDuration => TrackedIntervals == null
        ? null
        : StreamSessionInterval.Normalize(TrackedIntervals, StartedAt, EndedAt)
            .Aggregate(TimeSpan.Zero, (sum, interval) => sum + interval.Duration);

    public void EnsureSegments()
    {
        if (Segments.Count > 0)
        {
            return;
        }

        Segments.Add(new()
        {
            StartedAt = StartedAt,
            EndedAt = EndedAt > StartedAt ? EndedAt : StartedAt,
            Title = Title,
            Game = Game,
            MessageCount = MessageCount,
            PeakViewers = PeakViewers,
            AverageViewers = AverageViewers,
        });
    }
}
