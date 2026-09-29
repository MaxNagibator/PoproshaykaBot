using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Diagnostics;

public sealed record EventSubStatus(
    TwitchOAuthRole Role,
    ConnectionState State,
    DateTimeOffset? LastMessageAt,
    int ReconnectAttempt,
    int ReconnectLimit);
