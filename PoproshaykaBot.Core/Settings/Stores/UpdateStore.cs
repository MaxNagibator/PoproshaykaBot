using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings.Update;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class UpdateStore
{
    private readonly JsonStore<UpdateSettings> _store;

    public UpdateStore(ILogger<UpdateStore>? logger = null, string? filePath = null, SettingsWriteGate? gate = null)
    {
        var path = filePath ?? AppPaths.SettingsFile("update.json");
        _store = new(path, logger, describe: SettingsDescriber.Describe, gate: gate);

        logger?.LogDebug("UpdateStore инициализирован из {FilePath}", path);
    }

    public UpdateSettings Load()
    {
        return _store.Load();
    }

    public void Save(UpdateSettings value)
    {
        _store.Save(value);
    }
}
