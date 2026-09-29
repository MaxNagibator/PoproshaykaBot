using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace PoproshaykaBot.Core.Infrastructure.Runtime;

public sealed class ShutdownWatchdog : IDisposable
{
    private readonly Timer _timer;

    public ShutdownWatchdog(ILogger logger, Action flushLogs, TimeSpan deadline)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(flushLogs);

        _timer = new(_ =>
            {
                logger.LogCritical("Завершение работы превысило лимит {Seconds} c – принудительное завершение процесса", (int)deadline.TotalSeconds);
                flushLogs();
                Process.GetCurrentProcess().Kill();
            },
            null,
            deadline,
            Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
