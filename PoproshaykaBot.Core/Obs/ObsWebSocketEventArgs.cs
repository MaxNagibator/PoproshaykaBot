using System.Text.Json;

namespace PoproshaykaBot.Core.Obs;

public sealed class ObsWebSocketEventArgs(
    string eventType,
    int eventIntent,
    JsonElement? eventData)
    : EventArgs
{
    public string EventType { get; } = eventType;

    public int EventIntent { get; } = eventIntent;

    public JsonElement? EventData { get; } = eventData;
}
