namespace PoproshaykaBot.Core.Diagnostics;

public sealed record ObsConnectionStatus(ConnectionState State, DateTimeOffset? LastEventAt);
