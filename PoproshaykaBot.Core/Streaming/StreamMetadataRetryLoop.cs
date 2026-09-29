using Microsoft.Extensions.Logging;

namespace PoproshaykaBot.Core.Streaming;

internal sealed class StreamMetadataRetryLoop(
    Func<int, CancellationToken, Task<bool>> attempt,
    Func<bool> shouldContinue,
    ILogger logger)
    : IAsyncDisposable
{
    private const int MaxAttempts = 6;
    private const int DelayStepSeconds = 5;

    private readonly CancellationTokenSource _disposeCts = new();

    private CancellationTokenSource? _activeCts;
    private bool _disposed;

    public Task? CurrentTask { get; private set; }

    public async Task RestartAsync(CancellationToken outerToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await CancelAndDrainAsync().ConfigureAwait(false);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, outerToken);
        _activeCts = linkedCts;
        var loopToken = linkedCts.Token;

        CurrentTask = Task.Run(() => RunAsync(loopToken, linkedCts), loopToken);
    }

    public async Task CancelAndDrainAsync()
    {
        var previousCts = Interlocked.Exchange(ref _activeCts, null);
        var previousTask = CurrentTask;

        if (previousCts != null)
        {
            try
            {
                await previousCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // already disposed
            }
        }

        if (previousTask is { IsCompleted: false })
        {
            try
            {
                await previousTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Предыдущий retry-цикл метаданных завершился с ошибкой при отмене (игнорируется)");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _disposeCts.CancelAsync().ConfigureAwait(false);

        var task = CurrentTask;

        if (task != null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // disposing – swallow any leftover failure
            }
        }

        _disposeCts.Dispose();
        Interlocked.Exchange(ref _activeCts, null);
    }

    private async Task RunAsync(CancellationToken loopToken, CancellationTokenSource ownedCts)
    {
        try
        {
            logger.LogDebug("Запуск опроса метаданных стрима (попыток до {Max}, бэкофф 5/10/15/20/25/30 сек)", MaxAttempts);

            for (var i = 0; i < MaxAttempts; i++)
            {
                loopToken.ThrowIfCancellationRequested();

                var loaded = await attempt(i + 1, loopToken).ConfigureAwait(false);

                if (loaded)
                {
                    logger.LogInformation("Метаданные стрима получены из API (попытка {Attempt}/{Max})", i + 1, MaxAttempts);
                    break;
                }

                if (!shouldContinue())
                {
                    logger.LogDebug("Опрос метаданных прерван: стрим больше не онлайн (попытка {Attempt}/{Max})", i + 1, MaxAttempts);
                    break;
                }

                var delaySeconds = DelayStepSeconds * (i + 1);
                logger.LogWarning("Метаданные ещё не доступны в API. Повтор через {DelaySeconds} сек (попытка {Attempt}/{Max})",
                    delaySeconds, i + 1, MaxAttempts);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), loopToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Опрос метаданных стрима отменён (новый stream.online или остановка менеджера)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Непредвиденная ошибка во время фонового опроса метаданных стрима");
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeCts, null, ownedCts);
            ownedCts.Dispose();
        }
    }
}
