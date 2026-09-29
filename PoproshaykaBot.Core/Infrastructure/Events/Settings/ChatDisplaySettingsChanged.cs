using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Infrastructure.Events.Settings;

public sealed record ChatDisplaySettingsChanged(string Channel, TwitchOAuthRole ChatDisplayAccount) : EventBase;
