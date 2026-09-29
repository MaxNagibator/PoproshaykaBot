namespace PoproshaykaBot.Core.Diagnostics;

public sealed record ChatQueueStatus(
    int Length,
    int Capacity,
    long SentCount,
    long FailedCount,
    DateTimeOffset? LastSentAt);
