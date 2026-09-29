using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text.Json;

namespace PoproshaykaBot.Core.Statistics;

public class StreamSessionHistoryStore
{
    private const string FileName = "stream_sessions.json";

    private readonly ILogger<StreamSessionHistoryStore>? _logger;
    private readonly string _filePath;
    private readonly object _syncLock = new();

    private readonly StreamSessionHistory _state;

    private bool _isLoaded;
    private bool _rewrittenExternally;
    private bool _hasUnsavedChanges;

    public StreamSessionHistoryStore(ILogger<StreamSessionHistoryStore>? logger = null, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? Path.Combine(AppPaths.BaseDirectory, FileName);

        var read = ReadFile();
        _state = read.Value ?? new();
        _isLoaded = !read.Failed;

        if (_isLoaded)
        {
            MergeInterruptedSessions();
        }

        _logger?.LogDebug("StreamSessionHistoryStore инициализирован из {FilePath} (сессий: {SessionCount}, чтение удалось: {IsLoaded})",
            _filePath,
            _state.Sessions.Count,
            _isLoaded);
    }

    public bool IsLoaded
    {
        get
        {
            lock (_syncLock)
            {
                return _isLoaded;
            }
        }
    }

    public virtual bool Invalidate()
    {
        lock (_syncLock)
        {
            if (!_isLoaded)
            {
                return false;
            }

            _isLoaded = false;
            _rewrittenExternally = true;

            _logger?.LogInformation(
                "StreamSessionHistoryStore: файл {FilePath} меняют мимо приложения, история этого сеанса до перезапуска не сохраняется",
                _filePath);

            return true;
        }
    }

    public virtual void Restore(bool wasLoaded)
    {
        if (!wasLoaded)
        {
            return;
        }

        lock (_syncLock)
        {
            _isLoaded = true;
            _rewrittenExternally = false;

            _logger?.LogInformation("StreamSessionHistoryStore: файл {FilePath} никто не менял, история снова сохраняется", _filePath);

            FlushUnsavedChanges();
        }
    }

    public virtual bool TryFlush()
    {
        lock (_syncLock)
        {
            return _isLoaded ? FlushUnsavedChanges() : !_hasUnsavedChanges;
        }
    }

    public virtual StreamSessionHistory Load()
    {
        lock (_syncLock)
        {
            return JsonStoreClone.DeepClone(_state);
        }
    }

    public virtual StreamSessionHistory LoadVisible()
    {
        lock (_syncLock)
        {
            var visible = JsonStoreClone.DeepClone(_state);
            visible.Sessions.RemoveAll(session => session.IsHidden);
            return visible;
        }
    }

    public virtual void Append(StreamSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_syncLock)
        {
            var duplicate = _state.Sessions.Find(session =>
                string.Equals(session.Channel, record.Channel, StringComparison.OrdinalIgnoreCase)
                && session.StartedAt == record.StartedAt);

            if (duplicate != null)
            {
                _logger?.LogWarning(
                    "StreamSessionHistoryStore: сессия канала {Channel} со стартом {StartedAt:O} уже лежит в истории как {StoredId}, повторная запись {SessionId} отклонена",
                    record.Channel,
                    record.StartedAt,
                    duplicate.Id,
                    record.Id);

                return;
            }

            var stored = JsonStoreClone.DeepClone(record);
            stored.EnsureSegments();
            _state.Sessions.Add(stored);
            _hasUnsavedChanges = true;

            if (!_isLoaded)
            {
                _logger?.LogWarning(
                    "StreamSessionHistoryStore: {Reason} ({FilePath}), файл не переписывается – сессия {SessionId} остаётся в памяти и попадёт в файл, только если право записи вернётся до перезапуска",
                    DescribeLostWriteRight(),
                    _filePath,
                    record.Id);

                return;
            }

            PersistInternal();

            _logger?.LogInformation("StreamSessionHistoryStore: добавлена сессия {SessionId} (всего сессий: {SessionCount})",
                record.Id,
                _state.Sessions.Count);
        }
    }

    public virtual bool TrySetHidden(Guid id, bool isHidden)
    {
        lock (_syncLock)
        {
            var session = _state.Sessions.Find(item => item.Id == id);

            if (session == null)
            {
                _logger?.LogWarning("StreamSessionHistoryStore: сессия {SessionId} в истории не найдена, признак скрытия не изменён", id);
                return false;
            }

            if (session.IsHidden == isHidden)
            {
                return true;
            }

            var previous = session.IsHidden;
            var hadUnsavedChanges = _hasUnsavedChanges;

            session.IsHidden = isHidden;
            _hasUnsavedChanges = true;

            if (!_isLoaded)
            {
                _logger?.LogWarning(
                    "StreamSessionHistoryStore: {Reason} ({FilePath}), файл не переписывается – признак скрытия сессии {SessionId} остаётся в памяти и попадёт в файл, только если право записи вернётся до перезапуска",
                    DescribeLostWriteRight(),
                    _filePath,
                    id);

                return true;
            }

            try
            {
                PersistInternal();
            }
            catch (Exception exception)
            {
                session.IsHidden = previous;
                _hasUnsavedChanges = hadUnsavedChanges;

                _logger?.LogError(exception,
                    "Не удалось записать {FilePath}: признак скрытия сессии {SessionId} возвращён к прежнему",
                    _filePath,
                    id);

                return false;
            }

            _logger?.LogInformation("StreamSessionHistoryStore: сессия {SessionId} {HiddenState}",
                id,
                isHidden ? "скрыта из статистики" : "возвращена в статистику");

            return true;
        }
    }

    private string DescribeLostWriteRight()
    {
        return _rewrittenExternally ? "файл переписан мимо приложения" : "чтение файла сорвалось";
    }

    private void MergeInterruptedSessions()
    {
        var result = StreamSessionMerge.Merge(_state.Sessions);

        if (result.MergedPairs == 0)
        {
            return;
        }

        JsonStoreBackup.CreateBackup(_filePath, "premerge", _logger);
        _state.Sessions = [.. result.Sessions];
        _hasUnsavedChanges = true;

        try
        {
            PersistInternal();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception,
                "Не удалось записать склеенную историю в {FilePath}: склейка осталась только в памяти до ближайшей удачной записи",
                _filePath);

            return;
        }

        _logger?.LogInformation(
            "StreamSessionHistoryStore: склеено {MergedPairs} пар записей одного эфира, в истории осталось {SessionCount} сессий",
            result.MergedPairs,
            _state.Sessions.Count);
    }

    private bool FlushUnsavedChanges()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        try
        {
            PersistInternal();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception,
                "Не удалось записать {FilePath}: несохранённое осталось в памяти до следующей попытки",
                _filePath);

            return false;
        }

        _logger?.LogInformation(
            "StreamSessionHistoryStore: несохранённое дописано в {FilePath} (всего сессий: {SessionCount})",
            _filePath,
            _state.Sessions.Count);

        return true;
    }

    private void PersistInternal()
    {
        var json = JsonSerializer.Serialize(_state, JsonStoreOptions.Default);
        AtomicFile.Save(_filePath, json, _logger);
        _hasUnsavedChanges = false;
    }

    private StatisticsReadResult<StreamSessionHistory> ReadFile()
    {
        if (!File.Exists(_filePath))
        {
            _logger?.LogDebug("StreamSessionHistoryStore: файл {FilePath} не найден, используются дефолты", _filePath);
            return StatisticsReadResult<StreamSessionHistory>.NoFile;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var history = JsonSerializer.Deserialize<StreamSessionHistory>(json, JsonStoreOptions.Default);

            if (history == null)
            {
                _logger?.LogError("Файл {FilePath} разобран в пустое значение, история этого сеанса не сохраняется", _filePath);
                JsonStoreBackup.CreateBackup(_filePath, "invalid", _logger);
                return StatisticsReadResult<StreamSessionHistory>.Failure;
            }

            foreach (var session in history.Sessions)
            {
                session.EnsureSegments();
            }

            return StatisticsReadResult<StreamSessionHistory>.FromFile(history);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Ошибка чтения {FilePath}, история этого сеанса не сохраняется", _filePath);
            JsonStoreBackup.CreateBackup(_filePath, "invalid", _logger);
            return StatisticsReadResult<StreamSessionHistory>.Failure;
        }
    }
}
