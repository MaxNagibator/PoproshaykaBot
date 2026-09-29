using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Settings.Obs;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class ObsIntegrationStore
{
    private readonly IEventBus _eventBus;
    private readonly JsonStore<ObsIntegrationSettings> _store;

    public ObsIntegrationStore(IEventBus eventBus, ILogger<ObsIntegrationStore>? logger = null, string? filePath = null, SettingsWriteGate? gate = null)
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        var path = filePath ?? AppPaths.SettingsFile("obs-integration.json");

        _eventBus = eventBus;
        _store = new(path, logger, describe: SettingsDescriber.Describe, gate: gate);

        logger?.LogDebug("ObsIntegrationStore инициализирован из {FilePath}", path);
    }

    public ObsIntegrationSettings Load()
    {
        return _store.Load();
    }

    public void Save(ObsIntegrationSettings value)
    {
        var committed = JsonStoreClone.DeepClone(value);
        _store.Save(committed);

        _ = _eventBus.PublishAsync(new ObsIntegrationSettingsChangedEvent(committed));
    }
}
