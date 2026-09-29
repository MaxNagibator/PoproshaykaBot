using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace PoproshaykaBot.Core.Settings.Stores;

internal sealed class JsonStore<T>
    where T : class, new()
{
    private readonly string _filePath;
    private readonly ILogger? _logger;
    private readonly Func<string, string>? _backupRedactor;
    private readonly Func<string, T?>? _parser;
    private readonly Func<T, string>? _describe;
    private readonly SettingsWriteGate? _gate;
    private readonly object _syncLock = new();

    private T _state;

    public JsonStore(
        string filePath,
        ILogger? logger = null,
        Func<string, string>? backupRedactor = null,
        Func<string, T?>? parser = null,
        Func<T, string>? describe = null,
        SettingsWriteGate? gate = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _filePath = filePath;
        _logger = logger;
        _backupRedactor = backupRedactor;
        _parser = parser;
        _describe = describe;
        _gate = gate;
        _state = ReadFile();
    }

    public event EventHandler<JsonStoreWriteFailedEventArgs>? WriteFailed;

    public T Load()
    {
        lock (_syncLock)
        {
            return JsonStoreClone.DeepClone(_state);
        }
    }

    public bool Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Exception? failure;
        bool written;
        var snapshot = JsonStoreClone.DeepClone(value);

        lock (_syncLock)
        {
            var json = JsonSerializer.Serialize(snapshot, JsonStoreOptions.Default);
            written = TryWrite(json, out failure);

            if (failure == null)
            {
                _state = snapshot;
            }
        }

        if (failure != null)
        {
            ReportFailure(failure);
        }

        if (!written)
        {
            LogBlocked();
            return false;
        }

        LogSaved(LogLevel.Information, "замена", snapshot);

        return true;
    }

    public bool Mutate(Action<T> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        MutateCore(state =>
            {
                mutator(state);
                return true;
            },
            _ => true,
            out var written);

        return written;
    }

    public TResult Mutate<TResult>(Func<T, TResult> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        return MutateCore(mutator, _ => true, out _);
    }

    public bool MutateIf(Func<T, bool> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        return MutateCore(mutator, applied => applied, out _);
    }

    private TResult MutateCore<TResult>(Func<T, TResult> mutator, Func<TResult, bool> shouldWrite, out bool written)
    {
        TResult result;
        Exception? failure = null;
        var blocked = false;
        T draft;

        written = false;

        lock (_syncLock)
        {
            draft = JsonStoreClone.DeepClone(_state);
            result = mutator(draft);

            if (shouldWrite(result))
            {
                var json = JsonSerializer.Serialize(draft, JsonStoreOptions.Default);
                written = TryWrite(json, out failure);
                blocked = !written && failure == null;

                if (failure == null)
                {
                    _state = draft;
                }
            }
        }

        if (failure != null)
        {
            ReportFailure(failure);
        }

        if (blocked)
        {
            LogBlocked();
            return result;
        }

        if (written)
        {
            LogSaved(LogLevel.Debug, "мутация", draft);
        }

        return result;
    }

    private void LogSaved(LogLevel level, string operation, T state)
    {
        var logger = _logger;

        if (logger?.IsEnabled(level) != true)
        {
            return;
        }

        if (_describe == null)
        {
            logger.Log(level, "Сохранён {Store} ({Operation}) → {FilePath}", typeof(T).Name, operation, _filePath);
            return;
        }

        string description;

        try
        {
            description = _describe(state);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Описание {Store} не построено", typeof(T).Name);
            description = "описание недоступно";
        }

        logger.Log(level, "Сохранён {Store} ({Operation}): {Description}", typeof(T).Name, operation, description);
    }

    private bool TryWrite(string json, out Exception? failure)
    {
        Exception? captured = null;

        void Write()
        {
            try
            {
                AtomicFile.Save(_filePath, json, _logger, keepBackup: _backupRedactor == null);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception,
                    "Ошибка записи {FilePath}, в памяти осталась последняя записанная на диск версия",
                    _filePath);

                captured = exception;
            }
        }

        bool allowed;

        if (_gate == null)
        {
            Write();
            allowed = true;
        }
        else
        {
            allowed = _gate.TryWrite(_filePath, Write);
        }

        failure = captured;

        return allowed;
    }

    private void LogBlocked()
    {
        _logger?.LogWarning(
            "Файл {FilePath} не переписан: право записи снято внешней записью в него. Изменения приняты в памяти и действуют до перезапуска приложения",
            _filePath);
    }

    [DoesNotReturn]
    private void ReportFailure(Exception failure)
    {
        var handler = WriteFailed;

        if (handler != null)
        {
            try
            {
                handler(this, new(_filePath, failure));
            }
            catch (Exception handlerException)
            {
                _logger?.LogError(handlerException,
                    "Подписчик на сбой записи {FilePath} сам завершился ошибкой",
                    _filePath);
            }
        }

        ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private T ReadFile()
    {
        if (!File.Exists(_filePath))
        {
            _logger?.LogDebug("Файл {FilePath} не найден, используются дефолты", _filePath);
            return new();
        }

        try
        {
            var json = File.ReadAllText(_filePath);

            var state = _parser == null
                ? JsonSerializer.Deserialize<T>(json, JsonStoreOptions.Default)
                : _parser(json);

            return state ?? new();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Ошибка чтения {FilePath}, применяются дефолты", _filePath);
            JsonStoreBackup.CreateBackup(_filePath, "invalid", _logger, _backupRedactor);
            return new();
        }
    }
}
