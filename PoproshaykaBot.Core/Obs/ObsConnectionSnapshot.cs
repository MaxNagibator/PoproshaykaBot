namespace PoproshaykaBot.Core.Obs;

public sealed record ObsConnectionSnapshot(
    bool IsConnected,
    string? ObsVersion,
    string? ObsWebSocketVersion,
    string? ErrorMessage)
{
    public static ObsConnectionSnapshot Disconnected(string? errorMessage = null)
    {
        return new(false, null, null, errorMessage);
    }
}
