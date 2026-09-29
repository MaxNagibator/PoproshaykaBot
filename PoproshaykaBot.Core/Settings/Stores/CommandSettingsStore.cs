using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Infrastructure;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class CommandSettingsStore
{
    private readonly JsonStore<CommandSettings> _store;

    public CommandSettingsStore(ILogger<CommandSettingsStore>? logger = null, string? filePath = null)
    {
        _store = new(filePath ?? AppPaths.SettingsFile("commands.json"),
            logger,
            parser: json => Parse(json, logger),
            describe: SettingsDescriber.Describe);
    }

    public CommandSettings Load()
    {
        return _store.Load();
    }

    public void Mutate(Action<CommandSettings> mutator)
    {
        _store.Mutate(mutator);
    }

    private static CommandSettings? Parse(string json, ILogger? logger)
    {
        var settings = JsonSerializer.Deserialize<CommandSettings>(json, JsonStoreOptions.Default);

        if (settings is null)
        {
            return null;
        }

        foreach (var (from, to) in CommandRenames.MoveRenamed(settings.Commands))
        {
            logger?.LogInformation("Настройки команды {From} перенесены к её новому имени {To}", from, to);
        }

        return settings;
    }
}
