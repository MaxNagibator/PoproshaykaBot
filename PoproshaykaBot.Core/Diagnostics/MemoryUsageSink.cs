namespace PoproshaykaBot.Core.Diagnostics;

public sealed class MemoryUsageSink
{
    private MemoryUsage? _last;

    public MemoryUsage? Last => Volatile.Read(ref _last);

    public void Publish(MemoryUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        Volatile.Write(ref _last, usage);
    }
}
