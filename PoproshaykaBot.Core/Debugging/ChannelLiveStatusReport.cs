namespace PoproshaykaBot.Core.Debugging;

public sealed record ChannelLiveStatusReport(IReadOnlyList<ChannelLiveStatus> Channels, string? UnknownReason);
