namespace PoproshaykaBot.Core.Obs;

public sealed record ObsDashboardSnapshot(
    ObsConnectionSnapshot Connection,
    string? CurrentSceneName,
    bool? IsStreaming,
    bool? IsRecording,
    ObsMicrophoneSnapshot? Microphone,
    DateTimeOffset UpdatedAt)
{
    public bool IsConnected => Connection.IsConnected;

    public static ObsDashboardSnapshot Disabled()
    {
        return Unavailable(ObsConnectionSnapshot.Disconnected("OBS интеграция отключена."));
    }

    public static ObsDashboardSnapshot Unavailable(ObsConnectionSnapshot connection)
    {
        return new(connection, null, null, null, null, DateTimeOffset.Now);
    }
}
