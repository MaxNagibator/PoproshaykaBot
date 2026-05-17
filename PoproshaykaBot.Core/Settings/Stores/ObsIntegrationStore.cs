using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Obs;
using System.Text.Json;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class ObsIntegrationStore
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ObsIntegrationStore>? _logger;
    private readonly string _filePath;
    private readonly object _syncLock = new();

    private ObsIntegrationSettings _state;

    public ObsIntegrationStore(IEventBus eventBus, ILogger<ObsIntegrationStore>? logger = null, string? filePath = null)
    {
        _eventBus = eventBus;
        _logger = logger;
        _filePath = filePath ?? AppPaths.SettingsFile("obs-integration.json");
        _state = ReadFile();

        _logger?.LogDebug("ObsIntegrationStore инициализирован из {FilePath}", _filePath);
    }

    public ObsIntegrationSettings Load()
    {
        lock (_syncLock)
        {
            return JsonStoreClone.DeepClone(_state);
        }
    }

    public void Save(ObsIntegrationSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ObsIntegrationSettings broadcastCopy;

        lock (_syncLock)
        {
            _state = JsonStoreClone.DeepClone(value);
            var json = JsonSerializer.Serialize(_state, JsonStoreOptions.Default);
            AtomicFile.Save(_filePath, json, _logger);

            broadcastCopy = JsonStoreClone.DeepClone(_state);

            _logger?.LogInformation("ObsIntegrationStore: настройки OBS-интеграции сохранены в {FilePath}", _filePath);
        }

        _ = _eventBus.PublishAsync(new ObsIntegrationSettingsChangedEvent(broadcastCopy));
    }

    private ObsIntegrationSettings ReadFile()
    {
        if (!File.Exists(_filePath))
        {
            _logger?.LogDebug("ObsIntegrationStore: файл {FilePath} не найден, используются дефолты", _filePath);
            return new();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ObsIntegrationSettings>(json, JsonStoreOptions.Default) ?? new();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Ошибка чтения {FilePath}, применяются дефолты", _filePath);
            JsonStoreBackup.CreateBackup(_filePath, "invalid", _logger);
            return new();
        }
    }
}
