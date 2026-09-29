using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Broadcast.Profiles;

public sealed class GameCategoryResolver(
    ITwitchSearchApi searchApi,
    RecentCategoriesStore recentCategoriesStore,
    ILogger<GameCategoryResolver> logger) : IGameCategoryResolver
{
    private const int SearchPageSize = 10;

    public async Task<IReadOnlyList<GameSuggestion>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            return await searchApi.SearchCategoriesAsync(query, SearchPageSize, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Поиск категорий '{Query}' не удался", query);
            return [];
        }
    }

    public async Task<GameCategoryResolution> ResolveAsync(string query, CancellationToken cancellationToken)
    {
        var results = await SearchAsync(query, cancellationToken);
        var match = PickMatch(results, query);

        if (match != null)
        {
            await RememberAsync(match);
        }

        return new(match, results);
    }

    internal static GameSuggestion? PickMatch(IReadOnlyList<GameSuggestion> results, string query)
    {
        var trimmed = query.Trim();

        var exact = results.FirstOrDefault(x => string.Equals(x.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
        {
            return exact;
        }

        var words = SplitWords(trimmed);

        if (words.Length == 0)
        {
            return null;
        }

        return results.FirstOrDefault(x =>
        {
            var nameWords = SplitWords(x.Name);
            return words.All(word => nameWords.Any(nameWord => nameWord.StartsWith(word, StringComparison.OrdinalIgnoreCase)));
        });
    }

    private static string[] SplitWords(string text)
    {
        return text.Split(text.Where(c => !char.IsLetterOrDigit(c)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries);
    }

    public Task RememberAsync(GameSuggestion suggestion)
    {
        recentCategoriesStore.Remember(suggestion);
        return Task.CompletedTask;
    }
}
