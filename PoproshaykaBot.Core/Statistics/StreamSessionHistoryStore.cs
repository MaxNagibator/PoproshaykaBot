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

    public StreamSessionHistoryStore(ILogger<StreamSessionHistoryStore>? logger = null, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? Path.Combine(AppPaths.BaseDirectory, FileName);
        _state = ReadFile();

        _logger?.LogDebug("StreamSessionHistoryStore инициализирован из {FilePath} (сессий: {SessionCount})",
            _filePath,
            _state.Sessions.Count);
    }

    public virtual StreamSessionHistory Load()
    {
        lock (_syncLock)
        {
            return JsonStoreClone.DeepClone(_state);
        }
    }

    public virtual void Append(StreamSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_syncLock)
        {
            _state.Sessions.Add(JsonStoreClone.DeepClone(record));
            PersistInternal();

            _logger?.LogInformation("StreamSessionHistoryStore: добавлена сессия {SessionId} (всего сессий: {SessionCount})",
                record.Id,
                _state.Sessions.Count);
        }
    }

    private void PersistInternal()
    {
        var json = JsonSerializer.Serialize(_state, JsonStoreOptions.Default);
        AtomicFile.Save(_filePath, json, _logger);
    }

    private StreamSessionHistory ReadFile()
    {
        if (!File.Exists(_filePath))
        {
            _logger?.LogDebug("StreamSessionHistoryStore: файл {FilePath} не найден, используются дефолты", _filePath);
            return new();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<StreamSessionHistory>(json, JsonStoreOptions.Default) ?? new();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Ошибка чтения {FilePath}, применяются дефолты", _filePath);
            JsonStoreBackup.CreateBackup(_filePath, "invalid", _logger);
            return new();
        }
    }
}
