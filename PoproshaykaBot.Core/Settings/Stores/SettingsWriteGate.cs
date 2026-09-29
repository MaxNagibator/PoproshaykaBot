using Microsoft.Extensions.Logging;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class SettingsWriteGate(ILogger<SettingsWriteGate>? logger = null)
{
    private readonly HashSet<string> _revoked = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    private int _externalWriteDepth;
    private bool _externalWriteAbandoned;

    public bool HasRevoked
    {
        get
        {
            lock (_syncLock)
            {
                return _externalWriteDepth > 0 || _externalWriteAbandoned || _revoked.Count > 0;
            }
        }
    }

    public bool IsRevoked(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        lock (_syncLock)
        {
            return IsRevokedUnderLock(filePath);
        }
    }

    public bool TryWrite(string filePath, Action write)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(write);

        lock (_syncLock)
        {
            if (IsRevokedUnderLock(filePath))
            {
                return false;
            }

            write();

            return true;
        }
    }

    public async Task<T> RunExternalWriteAsync<T>(
        Func<Task<T>> write,
        Func<T, IReadOnlyCollection<string>> rewritten)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(rewritten);

        Begin();

        try
        {
            var result = await write().ConfigureAwait(false);
            Restore(rewritten(result));

            return result;
        }
        catch (Exception exception)
        {
            Abandon();

            logger?.LogWarning(
                exception,
                "Внешняя запись в файлы настроек оборвалась. Настройки этого сеанса до перезапуска сохраняться не будут: файлы могли остаться переписанными наполовину, и класть поверх них прочитанное до записи нельзя");

            throw;
        }
    }

    private bool IsRevokedUnderLock(string filePath)
    {
        if (_externalWriteDepth > 0 || _externalWriteAbandoned)
        {
            return true;
        }

        return _revoked.Contains(Path.GetFileName(filePath));
    }

    private void Begin()
    {
        lock (_syncLock)
        {
            _externalWriteDepth++;
        }

        logger?.LogInformation("Запись файлов настроек приостановлена на время внешней записи в них");
    }

    private void Restore(IReadOnlyCollection<string> rewritten)
    {
        ArgumentNullException.ThrowIfNull(rewritten);

        var reportKept = logger?.IsEnabled(LogLevel.Information) == true;
        string[] kept = [];

        lock (_syncLock)
        {
            foreach (var fileName in rewritten.Where(name => !string.IsNullOrEmpty(name)))
            {
                _revoked.Add(Path.GetFileName(fileName));
            }

            _externalWriteDepth--;

            if (reportKept)
            {
                kept = [.. _revoked.Order(StringComparer.OrdinalIgnoreCase)];
            }
        }

        if (!reportKept)
        {
            return;
        }

        if (kept.Length == 0)
        {
            logger?.LogInformation("Внешняя запись настроек ни одного файла не тронула, запись возвращена всем");
            return;
        }

        logger?.LogInformation(
            "Право записи снято до перезапуска у файлов настроек: {Files}. Остальным файлам настроек запись возвращена",
            string.Join(", ", kept));
    }

    private void Abandon()
    {
        lock (_syncLock)
        {
            _externalWriteDepth--;
            _externalWriteAbandoned = true;
        }
    }
}
