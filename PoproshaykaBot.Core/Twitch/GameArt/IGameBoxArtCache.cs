namespace PoproshaykaBot.Core.Twitch.GameArt;

public interface IGameBoxArtCache
{
    IReadOnlyDictionary<string, string> GetCachedPaths(IReadOnlyCollection<string> gameNames);

    Task<IReadOnlyDictionary<string, string>> GetPathsAsync(
        IReadOnlyCollection<string> gameNames,
        CancellationToken cancellationToken = default);

    void Invalidate(string gameName);

    GameBoxArtCacheStatus GetStatus();
}

public sealed record GameBoxArtCacheStatus(
    string Directory,
    int ImageCount,
    int MissCount,
    TimeSpan MissLifetime,
    bool CacheOnly);
