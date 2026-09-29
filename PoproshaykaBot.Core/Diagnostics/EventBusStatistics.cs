namespace PoproshaykaBot.Core.Diagnostics;

public sealed record EventBusStatistics(
    long PublishedTotal,
    long HandlerFailures,
    long ContinuationsStarted,
    long ContinuationFailures,
    IReadOnlyList<EventTypeStatistics> ByType);
