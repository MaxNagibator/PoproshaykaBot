namespace PoproshaykaBot.Core.Diagnostics;

public sealed record SseStatus(bool Running, int ClientCount, long DroppedMessageCount);
