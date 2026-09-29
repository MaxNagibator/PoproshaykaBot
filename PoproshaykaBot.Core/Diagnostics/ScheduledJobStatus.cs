namespace PoproshaykaBot.Core.Diagnostics;

public sealed record ScheduledJobStatus(
    ScheduledJob Job,
    TimeSpan? Interval,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt,
    string? LastError);
