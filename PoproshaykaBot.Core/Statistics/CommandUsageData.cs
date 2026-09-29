namespace PoproshaykaBot.Core.Statistics;

public sealed class CommandUsageData
{
    public string StreamId { get; set; } = string.Empty;

    public List<CommandUsageRecord> Commands { get; set; } = [];
}
