using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed record EmbeddedTwitchAuthRequest(
    TwitchOAuthRole Role,
    string ClientId,
    string ClientSecret,
    string[] Scopes,
    string? RedirectUri,
    bool CheckBroadcasterChannel);
