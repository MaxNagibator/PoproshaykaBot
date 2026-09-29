namespace PoproshaykaBot.Core.Diagnostics;

public sealed record ChatConnectionStatus(
    ConnectionState State,
    string? Channel,
    DateTimeOffset? JoinedAt,
    DateTimeOffset? LastMessageAt);
