namespace PoproshaykaBot.Core.Twitch.GameArt;

public sealed class GameBoxArtCacheOptions
{
    public const int ImageWidth = 104;
    public const int ImageHeight = 144;

    public const long DefaultMaxTotalBytes = 16L * 1024 * 1024;

    public static readonly TimeSpan DefaultMissLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan DefaultImageLifetime = TimeSpan.FromDays(90);
    public static readonly TimeSpan DefaultFailureBackoff = TimeSpan.FromMinutes(5);

    public required string Directory { get; init; }

    public bool CacheOnly { get; init; }

    public TimeSpan MissLifetime { get; init; } = DefaultMissLifetime;

    public TimeSpan ImageLifetime { get; init; } = DefaultImageLifetime;

    public long MaxTotalBytes { get; init; } = DefaultMaxTotalBytes;

    public TimeSpan FailureBackoff { get; init; } = DefaultFailureBackoff;

    public int MaxImageBytes { get; init; } = 2 * 1024 * 1024;
}
