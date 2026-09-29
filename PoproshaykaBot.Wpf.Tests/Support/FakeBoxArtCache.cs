using PoproshaykaBot.Core.Twitch.GameArt;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Support;

internal sealed class FakeBoxArtCache : IGameBoxArtCache
{
    private readonly Lock _sync = new();

    public Dictionary<string, string> Cached { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Downloadable { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string[]> CachedRequests { get; } = [];

    public List<string[]> NetworkRequests { get; } = [];

    public List<string> Invalidated { get; } = [];

    public List<int> RequestThreads { get; } = [];

    public TaskCompletionSource Gate { get; } = new();

    public bool WaitForGate { get; set; }

    public IReadOnlyDictionary<string, string> GetCachedPaths(IReadOnlyCollection<string> gameNames)
    {
        lock (_sync)
        {
            CachedRequests.Add([.. gameNames]);
            RequestThreads.Add(Environment.CurrentManagedThreadId);

            return Pick(gameNames, Cached);
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetPathsAsync(
        IReadOnlyCollection<string> gameNames,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            NetworkRequests.Add([.. gameNames]);
            RequestThreads.Add(Environment.CurrentManagedThreadId);
        }

        if (WaitForGate)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var found = new Dictionary<string, string>(Pick(gameNames, Cached), StringComparer.OrdinalIgnoreCase);

            foreach (var (game, path) in Pick(gameNames, Downloadable))
            {
                found[game] = path;
                Cached[game] = path;
            }

            return found;
        }
    }

    public void Invalidate(string gameName)
    {
        lock (_sync)
        {
            Invalidated.Add(gameName);
            Cached.Remove(gameName);
        }
    }

    public GameBoxArtCacheStatus GetStatus()
    {
        lock (_sync)
        {
            return new(Path.GetTempPath(), Cached.Count, 0, GameBoxArtCacheOptions.DefaultMissLifetime, CacheOnly: false);
        }
    }

    private static Dictionary<string, string> Pick(IReadOnlyCollection<string> gameNames, Dictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in gameNames)
        {
            if (source.TryGetValue(game, out var path))
            {
                result[game] = path;
            }
        }

        return result;
    }
}
