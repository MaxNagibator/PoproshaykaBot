using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Infrastructure;

namespace PoproshaykaBot.Core.Twitch.GameArt;

public static class GameArtServiceCollectionExtensions
{
    public const string CacheDirectorySegment = "cache";
    public const string BoxArtDirectorySegment = "box-art";

    public static IServiceCollection AddGameBoxArt(this IServiceCollection services, bool cacheOnly = false)
    {
        services.AddHttpClient(TwitchEndpoints.BoxArtClient);

        services.AddSingleton(_ => new GameBoxArtCacheOptions
        {
            Directory = AppPaths.Combine(CacheDirectorySegment, BoxArtDirectorySegment),
            CacheOnly = cacheOnly,
        });

        services.AddSingleton<IGameBoxArtCache, GameBoxArtCache>();

        return services;
    }
}
