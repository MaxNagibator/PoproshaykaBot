using Serilog;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class UiSettingsWriteGuard(ISettingsStore inner) : ISettingsStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _held = new(StringComparer.Ordinal);
    private bool _suspended;
    private bool _revoked;
    private bool _blockReported;

    public event EventHandler<string>? Changed;

    public event EventHandler<SettingsWriteFailedEventArgs>? WriteFailed
    {
        add => inner.WriteFailed += value;
        remove => inner.WriteFailed -= value;
    }

    public string FilePath => inner.FilePath;

    public bool IsRevoked
    {
        get
        {
            lock (_lock)
            {
                return _revoked;
            }
        }
    }

    private static Serilog.ILogger Logger => Log.ForContext<UiSettingsWriteGuard>();

    public string? GetStringValue(string key)
    {
        lock (_lock)
        {
            if (_held.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return inner.GetStringValue(key);
    }

    public void SetValue(string key, string value)
    {
        var reportBlocked = false;

        lock (_lock)
        {
            if (_suspended || _revoked)
            {
                _held[key] = value;
                reportBlocked = !_blockReported;
                _blockReported = true;
            }
            else
            {
                inner.SetValue(key, value);
            }
        }

        if (reportBlocked)
        {
            Logger.Warning("Настройки вида приняты в памяти до перезапуска, файл {Path} не переписан: туда переносятся данные предыдущей версии", FilePath);
        }

        Changed?.Invoke(this, key);
    }

    public void Flush()
    {
        inner.Flush();
    }

    public void Close()
    {
        int held;

        lock (_lock)
        {
            held = _held.Count;
        }

        inner.Close();

        if (held > 0)
        {
            Logger.Information("Настройки вида не записаны в {Path} ({Count} ключей): файл перенесён из предыдущей версии и действует после перезапуска", FilePath, held);
        }
    }

    public void Suspend()
    {
        var unsent = 0;

        lock (_lock)
        {
            if (!_suspended && !_revoked)
            {
                inner.Flush();

                foreach (var (key, value) in inner.DiscardPending())
                {
                    _held[key] = value;
                }

                unsent = _held.Count;
            }

            _suspended = true;
        }

        if (unsent > 0)
        {
            Logger.Warning("Настройки вида не удалось записать в {Path} перед переносом ({Count} ключей): они приняты в памяти и в файл попадут, только если перенос не принесёт свой", FilePath, unsent);
        }
    }

    public void Resume(bool rewritten)
    {
        int replayed;

        lock (_lock)
        {
            _suspended = false;

            if (rewritten)
            {
                _revoked = true;
            }

            if (_revoked)
            {
                return;
            }

            foreach (var (key, value) in _held)
            {
                inner.SetValue(key, value);
            }

            replayed = _held.Count;
            _held.Clear();
            _blockReported = false;
        }

        if (replayed > 0)
        {
            Logger.Information("Настройки вида, принятые во время переноса, записаны в {Path} ({Count} ключей)", FilePath, replayed);
        }
    }
}
