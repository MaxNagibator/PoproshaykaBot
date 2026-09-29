namespace PoproshaykaBot.Core.Statistics;

public sealed class ActiveStreamSessionInterval
{
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }
}
