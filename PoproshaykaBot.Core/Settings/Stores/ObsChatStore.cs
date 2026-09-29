using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Settings.Obs;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class ObsChatStore
{
    private readonly IEventBus _eventBus;
    private readonly JsonStore<ObsChatSettings> _store;

    public ObsChatStore(IEventBus eventBus, ILogger<ObsChatStore>? logger = null, string? filePath = null, SettingsWriteGate? gate = null)
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        var path = filePath ?? AppPaths.SettingsFile("obs-chat.json");

        _eventBus = eventBus;
        _store = new(path, logger, describe: SettingsDescriber.Describe, gate: gate);

        logger?.LogDebug("ObsChatStore инициализирован из {FilePath}", path);
    }

    public ObsChatSettings Load()
    {
        return _store.Load();
    }

    public void Save(ObsChatSettings value)
    {
        var committed = JsonStoreClone.DeepClone(value);
        _store.Save(committed);

        _ = _eventBus.PublishAsync(new ChatSettingsChangedEvent(committed));
    }
}
