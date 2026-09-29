using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Wpf.Infrastructure;

public enum EmbeddedTwitchAuthOutcome
{
    None = 0,
    Completed = 1,
    Canceled = 2,
    Unavailable = 3,
    Failed = 4,
}

public sealed record EmbeddedTwitchAuthResult(
    EmbeddedTwitchAuthOutcome Outcome,
    OAuthFlowResult? Flow,
    string Message)
{
    public static EmbeddedTwitchAuthResult Completed(OAuthFlowResult flow)
    {
        return new(EmbeddedTwitchAuthOutcome.Completed, flow, string.Empty);
    }

    public static EmbeddedTwitchAuthResult Canceled(string message)
    {
        return new(EmbeddedTwitchAuthOutcome.Canceled, null, message);
    }

    public static EmbeddedTwitchAuthResult Unavailable(string message)
    {
        return new(EmbeddedTwitchAuthOutcome.Unavailable, null, message);
    }

    public static EmbeddedTwitchAuthResult Failed(string message)
    {
        return new(EmbeddedTwitchAuthOutcome.Failed, null, message);
    }
}
