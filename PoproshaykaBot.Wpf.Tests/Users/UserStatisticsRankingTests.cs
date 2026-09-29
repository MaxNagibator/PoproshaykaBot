using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;

namespace PoproshaykaBot.Wpf.Tests.Users;

[TestFixture]
public class UserStatisticsRankingTests
{
    private static readonly PointTerm Term = new();

    private static readonly List<UserRank> Ranks =
    [
        new("♟", "ПЕШКА", 0),
        new("♞", "КОНЬ", 300),
        new("♜", "ЛАДЬЯ", 1000),
    ];

    private static UserStatisticsRowViewModel Row(string name, ulong messages, ulong bonus = 0, ulong penalty = 0)
    {
        var source = new UserStatistics
        {
            UserId = name + "-id",
            Name = name,
            MessageCount = messages,
            BonusPoints = bonus,
            PenaltyPoints = penalty,
        };

        var current = Ranks
            .Where(rank => source.Points >= (long)rank.MinMessages)
            .OrderByDescending(rank => rank.MinMessages)
            .FirstOrDefault() ?? Ranks[0];

        var standing = UserRankStanding.Create(source.Points, $"{current.Emoji} {current.DisplayName}", current, Ranks);

        return new(source, standing, Term);
    }

    private static List<UserStatisticsRowViewModel> Sample()
    {
        return
        [
            Row("borland", 100),
            Row("Anna", 300, bonus: 200),
            Row("zed", 50, penalty: 500),
        ];
    }

    [TestCase(UserStatisticsSortKey.Points, true, "Anna,borland,zed")]
    [TestCase(UserStatisticsSortKey.Points, false, "zed,borland,Anna")]
    [TestCase(UserStatisticsSortKey.Messages, true, "Anna,borland,zed")]
    [TestCase(UserStatisticsSortKey.Messages, false, "zed,borland,Anna")]
    [TestCase(UserStatisticsSortKey.Name, false, "Anna,borland,zed")]
    [TestCase(UserStatisticsSortKey.Name, true, "zed,borland,Anna")]
    [TestCase(UserStatisticsSortKey.Rank, true, "Anna,borland,zed")]
    [TestCase(UserStatisticsSortKey.Rank, false, "zed,borland,Anna")]
    public void Orders_rows_by_key_and_direction(UserStatisticsSortKey sortKey, bool descending, string expected)
    {
        var ordered = UserStatisticsRanking.Arrange(Sample(), sortKey, descending);

        Assert.That(string.Join(',', ordered.Select(row => row.Name)), Is.EqualTo(expected));
    }

    [Test]
    public void Numbers_positions_from_one_in_display_order()
    {
        var ordered = UserStatisticsRanking.Arrange(Sample(), UserStatisticsSortKey.Name, false);

        Assert.That(ordered.Select(row => row.Position), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Shares_are_measured_against_the_richest_row_of_the_set()
    {
        var ordered = UserStatisticsRanking.Arrange(Sample(), UserStatisticsSortKey.Name, false);

        Assert.Multiple(() =>
        {
            Assert.That(ordered[0].Share, Is.EqualTo(1).Within(0.0001));
            Assert.That(ordered[1].Share, Is.EqualTo(0.2).Within(0.0001));
            Assert.That(ordered[2].Share, Is.EqualTo(UserStatisticsRanking.MinimumShare));
        });
    }

    [Test]
    public void Recounts_positions_and_shares_after_filtering()
    {
        var filtered = Sample().Where(row => row.Name != "Anna").ToList();

        var ordered = UserStatisticsRanking.Arrange(filtered, UserStatisticsSortKey.Points, true);

        Assert.Multiple(() =>
        {
            Assert.That(ordered.Select(row => row.Name), Is.EqualTo(new[] { "borland", "zed" }));
            Assert.That(ordered.Select(row => row.Position), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(ordered[0].Share, Is.EqualTo(1).Within(0.0001));
        });
    }

    [Test]
    public void Keeps_the_bar_visible_when_nobody_has_points()
    {
        var ordered = UserStatisticsRanking.Arrange([Row("empty", 0)], UserStatisticsSortKey.Points, true);

        Assert.That(ordered[0].Share, Is.EqualTo(UserStatisticsRanking.MinimumShare));
    }

    [Test]
    public void Breaks_equal_ranks_by_points_and_then_by_name()
    {
        var rows = new List<UserStatisticsRowViewModel>
        {
            Row("bob", 10),
            Row("alice", 250),
            Row("carol", 250),
        };

        var ordered = UserStatisticsRanking.Arrange(rows, UserStatisticsSortKey.Rank, true);

        Assert.That(ordered.Select(row => row.Name), Is.EqualTo(new[] { "alice", "carol", "bob" }));
    }

    private static UserStatisticsRowViewModel LadderRow(IReadOnlyList<UserRank> ranks, ulong messages, ulong penalty = 0)
    {
        var source = new UserStatistics
        {
            UserId = "ladder-id",
            Name = "ladder",
            MessageCount = messages,
            PenaltyPoints = penalty,
        };

        var points = source.Points;

        var current = points < 0
            ? new("⛓️", "КАТОРЖНИК", 0)
            : ranks.Where(rank => points >= (long)rank.MinMessages).OrderByDescending(rank => rank.MinMessages).FirstOrDefault()
              ?? UserRank.Unranked;

        var standing = UserRankStanding.Create(points, $"{current.Emoji} {current.DisplayName}", current, ranks);

        return new(source, standing, Term);
    }

    private static string Shape(IEnumerable<UserRankLadderStep> steps)
    {
        return string.Join(' ', steps.Select(step => step.IsGap
            ? $"[{step.HiddenCount}]"
            : step.IsCurrent ? $">{step.Name}" : step.IsReached ? $"+{step.Name}" : step.Name));
    }

    [Test]
    public void Ladder_lists_every_rank_from_the_top_and_marks_the_way_up()
    {
        var ladder = LadderRow(Ranks, 400).RankLadder!;
        var steps = ladder.Arrange(false);

        Assert.Multiple(() =>
        {
            Assert.That(Shape(steps), Is.EqualTo("ЛАДЬЯ >КОНЬ +ПЕШКА"));
            Assert.That(steps.Select(step => step.ThresholdText.Replace(' ', ' ')), Is.EqualTo(new[] { "1 000", "300", "0" }));
            Assert.That(ladder.Current.HasProgress, Is.True);
            Assert.That(ladder.Current.ProgressText.Replace(' ', ' '), Is.EqualTo("ещё 600 баллов · 400 из 1 000"));
            Assert.That(ladder.Current.ProgressTrack.Value, Is.EqualTo(100d / 700).Within(0.0001));
            Assert.That(ladder.Summary, Is.EqualTo("Путь по рангам: ранг КОНЬ, до ранга ЛАДЬЯ ещё 600 баллов"));
            Assert.That(steps.Select(step => step.IsAboveReached), Is.EqualTo(new[] { false, false, true }));
            Assert.That(steps[0].HasAbove, Is.False);
            Assert.That(steps[^1].HasBelow, Is.False);
        });
    }

    [Test]
    public void Ladder_counts_a_step_reached_on_its_exact_threshold()
    {
        var ladder = LadderRow(Ranks, 300).RankLadder!;

        Assert.Multiple(() =>
        {
            Assert.That(Shape(ladder.Arrange(false)), Is.EqualTo("ЛАДЬЯ >КОНЬ +ПЕШКА"));
            Assert.That(ladder.Current.ProgressText.Replace(' ', ' '), Is.EqualTo("ещё 700 баллов · 300 из 1 000"));
            Assert.That(ladder.Current.ProgressTrack.Value, Is.Zero);
        });
    }

    [Test]
    public void Ladder_says_the_top_rank_has_nowhere_left_to_grow()
    {
        var ladder = LadderRow(Ranks, 1000).RankLadder!;

        Assert.Multiple(() =>
        {
            Assert.That(Shape(ladder.Arrange(false)), Is.EqualTo(">ЛАДЬЯ +КОНЬ +ПЕШКА"));
            Assert.That(ladder.Current.HasProgress, Is.False);
            Assert.That(ladder.Current.ProgressText.Replace(' ', ' '), Is.EqualTo("Максимальный ранг"));
            Assert.That(ladder.Summary, Is.EqualTo("Путь по рангам: ранг ЛАДЬЯ, это максимальный ранг"));
        });
    }

    [Test]
    public void Ladder_puts_a_negative_balance_below_every_step()
    {
        var ladder = LadderRow(Ranks, 10, penalty: 60).RankLadder!;

        Assert.Multiple(() =>
        {
            Assert.That(Shape(ladder.Arrange(false)), Is.EqualTo("ЛАДЬЯ КОНЬ ПЕШКА >КАТОРЖНИК"));
            Assert.That(ladder.Current.ThresholdText, Is.Empty);
            Assert.That(ladder.Current.ProgressText.Replace(' ', ' '), Is.EqualTo($"ещё 50 баллов · {UiCulture.Russian.NumberFormat.NegativeSign}50 из 0"));
            Assert.That(ladder.Current.ProgressTrack.Value, Is.Zero);
            Assert.That(ladder.Summary, Is.EqualTo("Путь по рангам: ранг КАТОРЖНИК, до ранга ПЕШКА ещё 50 баллов"));
        });
    }

    [Test]
    public void Ladder_shows_an_unranked_user_under_the_lowest_step_of_a_hand_edited_list()
    {
        var row = LadderRow([new("♟", "ПЕШКА", 500)], 10);
        var ladder = row.RankLadder!;

        Assert.Multiple(() =>
        {
            Assert.That(row.RankDisplay, Is.EqualTo("🌱 БЕЗ РАНГА"));
            Assert.That(row.RankOrder, Is.Zero);
            Assert.That(Shape(ladder.Arrange(false)), Is.EqualTo("ПЕШКА >БЕЗ РАНГА"));
            Assert.That(ladder.Current.ProgressText.Replace(' ', ' '), Is.EqualTo("ещё 490 баллов · 10 из 500"));
            Assert.That(ladder.Current.ProgressTrack.Value, Is.EqualTo(10d / 500).Within(0.0001));
        });
    }

    [Test]
    public void Ladder_is_absent_when_the_list_of_ranks_is_empty()
    {
        Assert.That(LadderRow([], 10).HasRankLadder, Is.False);
    }

    private static readonly List<UserRank> LongRanks =
    [
        .. Enumerable.Range(0, 16).Select(index => new UserRank("♟", $"R{index:00}", (ulong)(index * 100))),
    ];

    [TestCase(0UL, "R15 [12] R02 R01 >R00")]
    [TestCase(750UL, "R15 [5] R09 R08 >R07 +R06 +R05 [4] +R00")]
    [TestCase(1100UL, "R15 R14 R13 R12 >R11 +R10 +R09 [8] +R00")]
    [TestCase(1400UL, "R15 >R14 +R13 +R12 [11] +R00")]
    [TestCase(1500UL, ">R15 +R14 +R13 [12] +R00")]
    public void Long_ladder_folds_distant_steps_around_the_current_one(ulong messages, string expected)
    {
        var ladder = LadderRow(LongRanks, messages).RankLadder!;

        Assert.Multiple(() =>
        {
            Assert.That(Shape(ladder.Arrange(false)), Is.EqualTo(expected));
            Assert.That(ladder.Arrange(true), Has.Count.EqualTo(16));
            Assert.That(ladder.CanCollapse, Is.True);
        });
    }

    [Test]
    public void Ladder_that_folds_nothing_offers_no_expand_link()
    {
        var ladder = LadderRow([.. LongRanks.Take(9)], 400).RankLadder!;

        Assert.Multiple(() =>
        {
            Assert.That(ladder.CanCollapse, Is.False);
            Assert.That(ladder.Arrange(false), Has.Count.EqualTo(9));
        });
    }

    [Test]
    public void Shows_a_plain_zero_instead_of_a_signed_one()
    {
        var row = Row("quiet", 10);

        Assert.Multiple(() =>
        {
            Assert.That(row.BonusDisplay, Is.EqualTo("0"));
            Assert.That(row.PenaltyDisplay, Is.EqualTo("0"));
            Assert.That(row.HasBonus, Is.False);
            Assert.That(row.HasPenalty, Is.False);
        });
    }

    [TestCase(1, 421, "1-е место из 421")]
    [TestCase(12, 12, "12-е место из 12")]
    [TestCase(0, 421, "")]
    [TestCase(3, 0, "")]
    public void Names_the_place_of_a_row_in_the_visible_set(int position, int total, string expected)
    {
        Assert.That(UserStatisticsRanking.DescribePlace(position, total), Is.EqualTo(expected));
    }

    [Test]
    public void Counts_what_the_filter_left_on_screen()
    {
        Assert.That(UserStatisticsRanking.DescribeVisibleCount(12, 421), Is.EqualTo("Показано 12 из 421"));
    }

    [Test]
    public void Marks_only_the_first_three_rows_as_leaders()
    {
        var rows = new List<UserStatisticsRowViewModel>
        {
            Row("a", 400),
            Row("b", 300),
            Row("c", 200),
            Row("d", 100),
        };

        var ordered = UserStatisticsRanking.Arrange(rows, UserStatisticsSortKey.Points, true);

        Assert.That(ordered.Select(row => row.IsTopThree), Is.EqualTo(new[] { true, true, true, false }));
    }
}
