namespace PoproshaykaBot.Core.Diagnostics;

public sealed record MemoryUsage(
    DateTimeOffset MeasuredAt,
    long SelfBytes,
    long ChildBytes,
    IReadOnlyList<ChildProcessUsage> Children,
    long SelfThresholdBytes,
    long TotalThresholdBytes)
{
    public long TotalBytes => SelfBytes + ChildBytes;

    public int ChildCount => Children.Sum(child => child.Count);
}
