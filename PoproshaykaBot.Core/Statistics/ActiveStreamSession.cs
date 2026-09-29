namespace PoproshaykaBot.Core.Statistics;

public sealed class ActiveStreamSession
{
    public string Channel { get; set; } = string.Empty;

    public string? StreamId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long MessageCount { get; set; }

    public int PeakViewers { get; set; }

    public long ViewerSamplesSum { get; set; }

    public int ViewerSamplesCount { get; set; }

    public string? Title { get; set; }

    public string? Game { get; set; }

    public List<ActiveStreamSessionChatter> Chatters { get; set; } = [];

    public List<ActiveStreamSessionSegment> Segments { get; set; } = [];

    public List<ActiveStreamSessionInterval>? TrackedIntervals { get; set; }
}
