namespace PoproshaykaBot.Core.Settings.Debugging;

public sealed class DebugChannelSettings
{
    public bool IsEnabled { get; set; }

    public string Channel { get; set; } = string.Empty;

    public bool AllowSending { get; set; }

    public List<RecentDebugChannel> RecentChannels { get; set; } = [];
}
