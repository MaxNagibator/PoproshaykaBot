using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class RecentCategoriesStore
{
    private const int MaxCachedCategories = 20;

    private readonly JsonStore<RecentCategoriesFileDto> _store;

    public RecentCategoriesStore(ILogger<RecentCategoriesStore>? logger = null, string? filePath = null, SettingsWriteGate? gate = null)
    {
        _store = new(filePath ?? AppPaths.SettingsFile("recent-categories.json"), logger, gate: gate);
    }

    public IReadOnlyList<GameCategoryCacheEntry> Load()
    {
        return _store.Load().Items;
    }

    public void Remember(GameSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        _store.Mutate(state =>
        {
            state.Items.RemoveAll(entry => entry.Id == suggestion.Id);

            state.Items.Insert(0, new()
            {
                Id = suggestion.Id,
                Name = suggestion.Name,
                LastUsedAt = DateTimeOffset.UtcNow,
            });

            while (state.Items.Count > MaxCachedCategories)
            {
                state.Items.RemoveAt(state.Items.Count - 1);
            }
        });
    }

    public void Save()
    {
        _store.Mutate(_ => { });
    }

    private sealed class RecentCategoriesFileDto
    {
        public List<GameCategoryCacheEntry> Items { get; set; } = [];
    }
}
