using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings.Debugging;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class DebugChannelStore
{
    public const int MaxRecentChannels = 10;

    private readonly JsonStore<DebugChannelSettings> _store;

    public DebugChannelStore(ILogger<DebugChannelStore>? logger = null, string? filePath = null)
    {
        var path = filePath ?? AppPaths.SettingsFile("debug-channel.json");
        _store = new(path, logger, describe: SettingsDescriber.Describe);

        logger?.LogDebug("DebugChannelStore инициализирован из {FilePath}", path);
    }

    public DebugChannelSettings Load()
    {
        return _store.Load();
    }

    public bool Save(DebugChannelSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return _store.Mutate(state =>
        {
            state.IsEnabled = value.IsEnabled;
            state.Channel = value.Channel;
            state.AllowSending = value.AllowSending;
        });
    }

    public IReadOnlyList<RecentDebugChannel> LoadRecent()
    {
        return NormalizeRecent(_store.Load().RecentChannels);
    }

    public bool RecordRecent(string login, DateTimeOffset usedAt)
    {
        if (!ChannelLogin.TryNormalize(login, out var normalized))
        {
            return false;
        }

        return _store.Mutate(state =>
        {
            var recent = NormalizeRecent(state.RecentChannels)
                .Where(channel => !string.Equals(channel.Login, normalized, StringComparison.OrdinalIgnoreCase))
                .Prepend(new() { Login = normalized, LastUsedAt = usedAt })
                .Take(MaxRecentChannels)
                .ToList();

            state.RecentChannels = recent;
        });
    }

    public bool RemoveRecent(string login)
    {
        if (!ChannelLogin.TryNormalize(login, out var normalized))
        {
            return false;
        }

        return _store.MutateIf(state =>
        {
            var removed = state.RecentChannels.RemoveAll(channel =>
                channel is null
                || !ChannelLogin.TryNormalize(channel.Login, out var stored)
                || string.Equals(stored, normalized, StringComparison.Ordinal));

            return removed > 0;
        });
    }

    private static List<RecentDebugChannel> NormalizeRecent(IEnumerable<RecentDebugChannel?> channels)
    {
        var result = new List<RecentDebugChannel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in channels.OfType<RecentDebugChannel>().OrderByDescending(channel => channel.LastUsedAt))
        {
            if (!ChannelLogin.TryNormalize(channel.Login, out var normalized) || !seen.Add(normalized))
            {
                continue;
            }

            result.Add(new() { Login = normalized, LastUsedAt = channel.LastUsedAt });

            if (result.Count == MaxRecentChannels)
            {
                break;
            }
        }

        return result;
    }
}
