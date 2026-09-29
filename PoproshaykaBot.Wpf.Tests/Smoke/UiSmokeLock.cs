namespace PoproshaykaBot.Wpf.Tests.Smoke;

internal sealed class UiSmokeLock : IDisposable
{
    private const string MutexName = @"Local\PoproshaykaBot-ui-smoke";

    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromMinutes(5);

    private readonly Mutex _mutex;

    private UiSmokeLock(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static UiSmokeLock Acquire()
    {
        var mutex = new Mutex(false, MutexName);

        try
        {
            if (!mutex.WaitOne(AcquireTimeout))
            {
                throw new TimeoutException(
                    $"Рабочий стол занят другим UI-смоуком дольше {AcquireTimeout}: замок {MutexName} не достался этому прогону.");
            }
        }
        catch (AbandonedMutexException)
        {
        }
        catch
        {
            mutex.Dispose();
            throw;
        }

        return new(mutex);
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
        }

        _mutex.Dispose();
    }
}
