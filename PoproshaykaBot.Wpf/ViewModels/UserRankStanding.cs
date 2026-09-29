using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed record UserRankStanding(string Display, ulong Order, string? NextDisplay, long PointsToNext, double Progress, bool IsTopRank)
{
    public UserRank Current { get; init; } = UserRank.Unranked;

    public IReadOnlyList<UserRank> Ranks { get; init; } = [];

    public static UserRankStanding Create(long points, string display, UserRank current, IReadOnlyList<UserRank>? ranks)
    {
        ArgumentNullException.ThrowIfNull(current);

        var next = FindNext(points, ranks);

        if (next is null)
        {
            return new(display, current.MinMessages, null, 0, 1, true) { Current = current, Ranks = ranks ?? [] };
        }

        var floor = Math.Min((long)current.MinMessages, points);
        var ceiling = (long)next.MinMessages;
        var span = ceiling - floor;
        var progress = span > 0 ? Math.Clamp((double)(points - floor) / span, 0, 1) : 0;

        return new(display, current.MinMessages, $"{next.Emoji} {next.DisplayName}", Math.Max(ceiling - points, 0), progress, false)
        {
            Current = current,
            Ranks = ranks ?? [],
        };
    }

    private static UserRank? FindNext(long points, IReadOnlyList<UserRank>? ranks)
    {
        if (ranks is null)
        {
            return null;
        }

        UserRank? next = null;

        foreach (var rank in ranks)
        {
            if ((long)rank.MinMessages <= points)
            {
                continue;
            }

            if (next is null || rank.MinMessages < next.MinMessages)
            {
                next = rank;
            }
        }

        return next;
    }
}
