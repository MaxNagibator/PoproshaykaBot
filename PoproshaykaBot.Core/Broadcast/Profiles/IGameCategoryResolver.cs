namespace PoproshaykaBot.Core.Broadcast.Profiles;

public interface IGameCategoryResolver
{
    Task<IReadOnlyList<GameSuggestion>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<GameCategoryResolution> ResolveAsync(string query, CancellationToken cancellationToken);
    Task RememberAsync(GameSuggestion suggestion);
}
