namespace PoproshaykaBot.Core.Diagnostics;

public enum ConnectionState
{
    Unknown = 0,
    Connecting = 1,
    Connected = 2,
    Reconnecting = 3,
    Disconnected = 4,
    Failed = 5,
}
