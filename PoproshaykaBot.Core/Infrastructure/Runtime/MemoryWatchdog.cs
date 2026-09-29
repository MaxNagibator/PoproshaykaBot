using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Diagnostics;
using System.Diagnostics;

namespace PoproshaykaBot.Core.Infrastructure.Runtime;

public sealed class MemoryWatchdog : IDisposable
{
    private const long BytesPerMb = 1024 * 1024;

    private const long SelfThresholdMb = 1024;

    private const long TotalThresholdMb = 2048;

    private const int LogEveryNthCheck = 300;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    private readonly ILogger _logger;

    private readonly Action _flushLogs;

    private readonly MemoryUsageSink? _usageSink;

    private readonly Timer _timer;

    private int _busy;

    private int _checkCount;

    public MemoryWatchdog(ILogger logger, Action flushLogs, MemoryUsageSink? usageSink = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(flushLogs);

        _logger = logger;
        _flushLogs = flushLogs;
        _usageSink = usageSink;
        _timer = new(_ => Check(), null, CheckInterval, CheckInterval);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    internal static MemoryUsage Measure(ILogger logger)
    {
        var children = ProcessTree.ChildProcesses(logger);
        long childBytes = 0;

        foreach (var child in children)
        {
            childBytes += child.Bytes;
        }

        return new(DateTimeOffset.UtcNow,
            ProcessTree.SelfMemoryBytes(),
            childBytes,
            children,
            SelfThresholdMb * BytesPerMb,
            TotalThresholdMb * BytesPerMb);
    }

    private void Check()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            var usage = Measure(_logger);
            _usageSink?.Publish(usage);

            var selfBytes = usage.SelfBytes;
            var childBytes = usage.ChildBytes;
            var childCount = usage.ChildCount;
            var totalBytes = usage.TotalBytes;

            if (++_checkCount % LogEveryNthCheck == 1)
            {
                _logger.LogInformation("Память: процесс {SelfMb} МБ, дочерние {ChildMb} МБ ({ChildCount} проц.), всего {TotalMb} МБ",
                    selfBytes / BytesPerMb, childBytes / BytesPerMb, childCount, totalBytes / BytesPerMb);
            }

            if (selfBytes <= SelfThresholdMb * BytesPerMb && totalBytes <= TotalThresholdMb * BytesPerMb)
            {
                return;
            }

            _logger.LogCritical("Аномальное потребление памяти: процесс {SelfMb} МБ, дочерние {ChildMb} МБ ({ChildCount} проц.), всего {TotalMb} МБ – принудительное завершение процесса",
                selfBytes / BytesPerMb, childBytes / BytesPerMb, childCount, totalBytes / BytesPerMb);

            _flushLogs();
            Process.GetCurrentProcess().Kill();
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }
}
