using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Hosting;

namespace PoproshaykaBot.Core.Streaming;

public sealed class StreamStatusWatchdog : IStreamHostedComponent, IAsyncDisposable
{
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(1);

    private readonly IStreamStatus _streamStatus;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<StreamStatusWatchdog> _logger;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    public StreamStatusWatchdog(
        IStreamStatus streamStatus,
        TimeProvider timeProvider,
        ILogger<StreamStatusWatchdog> logger)
        : this(streamStatus, timeProvider, DefaultPollInterval, logger)
    {
    }

    internal StreamStatusWatchdog(
        IStreamStatus streamStatus,
        TimeProvider timeProvider,
        TimeSpan pollInterval,
        ILogger<StreamStatusWatchdog> logger)
    {
        _streamStatus = streamStatus;
        _timeProvider = timeProvider;
        _pollInterval = pollInterval;
        _logger = logger;
    }

    public string Name => "Watchdog статуса стрима";

    public int StartOrder => 257;

    public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_loopTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        _cts?.Dispose();
        _cts = new();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), cancellationToken);

        _logger.LogInformation("StreamStatusWatchdog: запущен с интервалом {Interval}", _pollInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_cts == null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        if (_loopTask != null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StreamStatusWatchdog: цикл завершился с ошибкой при остановке");
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_cts != null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_loopTask != null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch
            {
                // disposing – swallow any leftover failure
            }
        }

        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
        GC.SuppressFinalize(this);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_streamStatus.CurrentStatus != StreamStatus.Online)
                {
                    continue;
                }

                try
                {
                    await _streamStatus.RefreshLiveSnapshotAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "StreamStatusWatchdog: ошибка RefreshLiveSnapshotAsync");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
    }
}
