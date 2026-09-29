using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels;

public static class UserStatisticsRanking
{
    public const double MinimumShare = 0.02;

    public static string DescribePlace(int position, int total)
    {
        return position > 0 && total > 0
            ? string.Create(UiCulture.Russian, $"{position:N0}-е место из {total:N0}")
            : string.Empty;
    }

    public static string DescribeVisibleCount(int visible, int total)
    {
        return string.Create(UiCulture.Russian, $"Показано {visible:N0} из {total:N0}");
    }

    public static List<UserStatisticsRowViewModel> Arrange(
        IReadOnlyList<UserStatisticsRowViewModel> rows,
        UserStatisticsSortKey sortKey,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var ordered = new List<UserStatisticsRowViewModel>(rows);

        ordered.Sort((left, right) => Compare(left, right, sortKey, descending));

        var leader = 0L;

        foreach (var row in ordered)
        {
            leader = Math.Max(leader, row.Points);
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var row = ordered[index];

            row.Position = index + 1;
            row.Share = leader > 0
                ? Math.Clamp((double)row.Points / leader, MinimumShare, 1)
                : MinimumShare;
        }

        return ordered;
    }

    private static int Compare(
        UserStatisticsRowViewModel left,
        UserStatisticsRowViewModel right,
        UserStatisticsSortKey sortKey,
        bool descending)
    {
        var primary = sortKey switch
        {
            UserStatisticsSortKey.Messages => left.MessageCount.CompareTo(right.MessageCount),
            UserStatisticsSortKey.Name => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase),
            UserStatisticsSortKey.Rank => CompareRank(left, right),
            _ => left.Points.CompareTo(right.Points),
        };

        if (primary != 0)
        {
            return descending ? -primary : primary;
        }

        return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    private static int CompareRank(UserStatisticsRowViewModel left, UserStatisticsRowViewModel right)
    {
        var byRank = left.RankOrder.CompareTo(right.RankOrder);

        return byRank != 0 ? byRank : left.Points.CompareTo(right.Points);
    }
}
