namespace PoproshaykaBot.Core.Debugging;

public sealed record ChannelLiveStatus(
    string Login,
    ChannelLiveState State,
    int ViewerCount = 0,
    string? Title = null,
    string? GameName = null,
    DateTimeOffset? StartedAt = null);
