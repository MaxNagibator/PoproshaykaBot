using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Wpf.Tests.Diagnostics;

public static class DiagnosticsSnapshots
{
    public static readonly DateTimeOffset CapturedAt = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);

    public const long Gigabyte = 1024L * 1024 * 1024;

    public static DiagnosticsSnapshot Empty()
    {
        return new(CapturedAt, null, [], null, null, null, null, [], null);
    }

    public static DiagnosticsSnapshot Full()
    {
        return new(CapturedAt,
            Memory(512L * 1024 * 1024, 256L * 1024 * 1024),
            [new(TwitchOAuthRole.Bot, ConnectionState.Connected, CapturedAt.AddMinutes(-3), 0, 5)],
            new(ConnectionState.Connected, "bobito217", CapturedAt.AddHours(-1), CapturedAt.AddMinutes(-2)),
            new(ConnectionState.Connected, CapturedAt.AddSeconds(-10)),
            new(true, 2, 0),
            new(4, 1000, 120, 0, CapturedAt.AddMinutes(-1)),
            [new(ScheduledJob.Broadcast, TimeSpan.FromMinutes(15), CapturedAt.AddMinutes(5), CapturedAt.AddMinutes(-10), null)],
            new(1200, 0, 8, 0, [new("StreamWentOnline", 3, 0, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(80), CapturedAt.AddMinutes(-20))]));
    }

    public static MemoryUsage Memory(long selfBytes, long childBytes)
    {
        return new(CapturedAt.AddSeconds(-1),
            selfBytes,
            childBytes,
            [new("msedgewebview2", 4, childBytes)],
            Gigabyte,
            2 * Gigabyte);
    }
}
