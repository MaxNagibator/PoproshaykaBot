namespace PoproshaykaBot.Core.Diagnostics;

public sealed record EventTypeStatistics(
    string EventType,
    long PublishCount,
    long HandlerFailureCount,
    TimeSpan TotalDuration,
    TimeSpan MaxDuration,
    DateTimeOffset LastPublishedAt);
