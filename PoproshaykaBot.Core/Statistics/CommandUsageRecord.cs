namespace PoproshaykaBot.Core.Statistics;

public sealed class CommandUsageRecord
{
    public string Canonical { get; set; } = string.Empty;

    public long TotalCount { get; set; }

    public long StreamCount { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public string LastUsedBy { get; set; } = string.Empty;

    public CommandUsageRecord Clone()
    {
        return new()
        {
            Canonical = Canonical,
            TotalCount = TotalCount,
            StreamCount = StreamCount,
            LastUsedAt = LastUsedAt,
            LastUsedBy = LastUsedBy,
        };
    }
}
