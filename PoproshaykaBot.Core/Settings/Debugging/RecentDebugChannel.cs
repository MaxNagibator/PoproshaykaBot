namespace PoproshaykaBot.Core.Settings.Debugging;

public sealed class RecentDebugChannel
{
    public string Login { get; set; } = string.Empty;

    public DateTimeOffset LastUsedAt { get; set; }
}
