namespace PoproshaykaBot.Core.Debugging;

public sealed record TargetChannelState(
    string Login,
    string OwnChannel,
    bool IsDebugSession,
    bool IsSendingAllowed,
    bool IsProfileIsolated = false)
{
    public bool IsForeign => IsDebugSession
        && Login.Length > 0
        && !string.Equals(Login, OwnChannel, StringComparison.OrdinalIgnoreCase);

    public bool RecordsUserData => !IsForeign || IsProfileIsolated;
}
