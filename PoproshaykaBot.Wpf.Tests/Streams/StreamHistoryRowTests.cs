using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.ViewModels;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
public class StreamHistoryRowTests
{
    private static readonly DateTimeOffset Start = new(new DateTime(2026, 9, 6, 17, 37, 0, DateTimeKind.Local));

    private static StreamSessionRowViewModel Row(int peakViewers, string? title = "эфир")
    {
        return new(new()
        {
            StartedAt = Start,
            EndedAt = Start.AddHours(3),
            Title = title,
            PeakViewers = peakViewers,
        });
    }

    [TestCase(131, 100, 5, "+31 % к обычному", TrendTone.Up)]
    [TestCase(60, 100, 5, "−40 % к обычному", TrendTone.Down)]
    [TestCase(102, 100, 5, "как обычно", TrendTone.Flat)]
    [TestCase(96, 100, 5, "как обычно", TrendTone.Flat)]
    [TestCase(95, 100, 5, "−5 % к обычному", TrendTone.Down)]
    [TestCase(131, 100, 1, "", TrendTone.None)]
    [TestCase(131, 0, 5, "", TrendTone.None)]
    public void Compares_a_session_with_the_average(
        double value,
        double average,
        int sessionCount,
        string expectedText,
        TrendTone expectedTone)
    {
        var (text, tone) = StreamHistoryPageViewModel.DescribeDelta(value, average, sessionCount);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo(expectedText));
            Assert.That(tone, Is.EqualTo(expectedTone));
        });
    }

    [TestCase(20, 20, 1d)]
    [TestCase(10, 20, 0.5)]
    [TestCase(0, 20, StreamTrendBarViewModel.MinimumShare)]
    [TestCase(7, 0, StreamTrendBarViewModel.MinimumShare)]
    public void Scales_a_trend_bar_against_the_peak_leader(int peakViewers, int leader, double expectedShare)
    {
        var bar = new StreamTrendBarViewModel(Row(peakViewers), leader, StreamTrendMetric.PeakViewers);

        Assert.Multiple(() =>
        {
            Assert.That(bar.Share, Is.EqualTo(expectedShare).Within(1e-9));
            Assert.That(bar.FillTrack.Value + bar.RestTrack.Value, Is.EqualTo(1).Within(1e-9));
            Assert.That(bar.Label, Does.StartWith("06.09.2026"));
        });
    }

    private static IEnumerable<TestCaseData> TrendScaleCases()
    {
        yield return new TestCaseData(Array.Empty<long>(), 0L).SetName("Пустая полоса без шкалы");
        yield return new TestCaseData(new long[] { 0, 0, 0 }, 0L).SetName("Одни нули без шкалы");
        yield return new TestCaseData(new long[] { 0, 5, 9, 2543 }, 2543L).SetName("Три ненулевых меряются максимумом");
        yield return new TestCaseData(Enumerable.Repeat(7L, 40).ToArray(), 7L).SetName("Равные пики дают шкалу по себе");
        yield return new TestCaseData(Enumerable.Range(1, 10).Select(value => (long)value).ToArray(), 9L).SetName("Девяностый перцентиль по ближайшему рангу");
        yield return new TestCaseData(Enumerable.Range(0, 39).Select(index => 3L + index % 15).Append(2543).ToArray(), 16L)
            .SetName("Выброс не задаёт шкалу");
    }

    [TestCaseSource(nameof(TrendScaleCases))]
    public void Trend_scale_ignores_an_outlier(long[] values, long expectedScale)
    {
        Assert.That(StreamTrendBarViewModel.ScaleOf(values), Is.EqualTo(expectedScale));
    }

    [TestCase(2543, 16, 1d, true)]
    [TestCase(16, 16, 1d, false)]
    [TestCase(8, 16, 0.5, false)]
    public void A_bar_above_the_scale_is_clipped_and_marked(int peakViewers, long scale, double expectedShare, bool expectedClipped)
    {
        var bar = new StreamTrendBarViewModel(Row(peakViewers), scale, StreamTrendMetric.PeakViewers);

        Assert.Multiple(() =>
        {
            Assert.That(bar.Share, Is.EqualTo(expectedShare).Within(1e-9));
            Assert.That(bar.IsClipped, Is.EqualTo(expectedClipped));
            Assert.That(bar.Value, Is.EqualTo(peakViewers));
            Assert.That(bar.Label.Contains("выше шкалы", StringComparison.Ordinal), Is.EqualTo(expectedClipped));
        });
    }

    [TestCase(1, 54, 54, 1d, true)]
    [TestCase(3, 27, 54, 0.5, true)]
    [TestCase(4, 0, 54, UserStatisticsRanking.MinimumShare, false)]
    public void Ranks_a_chatter_against_the_loudest_one(
        int position,
        long messageCount,
        long leader,
        double expectedShare,
        bool expectedTopThree)
    {
        var row = new StreamSessionChatterRowViewModel(
            new() { UserId = "42", DisplayName = "qp_illson", MessageCount = messageCount },
            position,
            leader,
            canOpen: true);

        Assert.Multiple(() =>
        {
            Assert.That(row.Share, Is.EqualTo(expectedShare).Within(1e-9));
            Assert.That(row.IsTopThree, Is.EqualTo(expectedTopThree));
            Assert.That(row.Summary, Does.Contain("qp_illson"));
        });
    }

    [TestCase("эфир", "эфир", false)]
    [TestCase(null, "Без названия", true)]
    [TestCase("", "Без названия", true)]
    public void Names_a_session_without_a_title(string? title, string expected, bool expectedMissing)
    {
        var row = Row(10, title);

        Assert.Multiple(() =>
        {
            Assert.That(row.TitleFormatted, Is.EqualTo(expected));
            Assert.That(row.HasTitle, Is.EqualTo(!expectedMissing));
        });
    }

    [Test]
    public void Spells_a_period_inside_one_day_without_repeating_the_date()
    {
        var period = StreamHistoryPageViewModel.FormatPeriod(Start, Start.AddHours(3).AddMinutes(42));

        Assert.That(period, Is.EqualTo("06.09.2026, 17:37 – 21:19"));
    }

    [Test]
    public void Сегмент_с_новым_названием_при_той_же_игре_подписан_названием()
    {
        var segment = new StreamSessionSegmentRowViewModel(
            new() { StartedAt = Start, EndedAt = Start.AddHours(1), Game = "Just Chatting", Title = "вторая часть" },
            0.5,
            continuesGame: true);

        Assert.Multiple(() =>
        {
            Assert.That(segment.Caption, Is.EqualTo("вторая часть"));
            Assert.That(segment.ContinuesGame, Is.True);
            Assert.That(segment.TimelineText, Is.EqualTo("Just Chatting · вторая часть, 1 ч 0 мин · 0 сообщ. · пик 0"));
            Assert.That(segment.CanFilter, Is.True);
        });
    }

    [Test]
    public void Первый_сегмент_игры_подписан_игрой()
    {
        var segment = new StreamSessionSegmentRowViewModel(
            new() { StartedAt = Start, EndedAt = Start.AddHours(1), Game = "Just Chatting", Title = "эфир" },
            1,
            continuesGame: false);

        Assert.That(segment.Caption, Is.EqualTo("Just Chatting"));
    }

    [Test]
    public void Сегмент_без_категории_не_фильтрует_и_зовётся_прочерком()
    {
        var segment = new StreamSessionSegmentRowViewModel(
            new() { StartedAt = Start, EndedAt = Start.AddHours(1), Title = null },
            1,
            continuesGame: false);

        Assert.Multiple(() =>
        {
            Assert.That(segment.Game, Is.EqualTo("–"));
            Assert.That(segment.Title, Is.EqualTo("Без названия"));
            Assert.That(segment.CanFilter, Is.False);
        });
    }

    [TestCase(1, 1, "")]
    [TestCase(3, 1, "3 названия")]
    [TestCase(1, 2, "2 категории")]
    [TestCase(3, 2, "3 названия · 2 категории")]
    public void Шапка_называет_состав_сессии(int titles, int games, string expected)
    {
        var session = new StreamSessionRecord { StartedAt = Start, EndedAt = Start.AddHours(3) };

        for (var index = 0; index < Math.Max(titles, games); index++)
        {
            session.Segments.Add(new()
            {
                StartedAt = Start.AddHours(index),
                EndedAt = Start.AddHours(index + 1),
                Title = "название " + Math.Min(index, titles - 1),
                Game = "игра " + Math.Min(index, games - 1),
            });
        }

        Assert.That(StreamHistoryPageViewModel.DescribeComposition(session), Is.EqualTo(expected));
    }

    [Test]
    public void Колонка_игры_считает_категории_а_не_сегменты()
    {
        var session = new StreamSessionRecord
        {
            StartedAt = Start,
            EndedAt = Start.AddHours(3),
            Game = "Just Chatting",
            Segments =
            [
                new() { StartedAt = Start, EndedAt = Start.AddHours(1), Game = "Just Chatting", Title = "первая часть" },
                new() { StartedAt = Start.AddHours(1), EndedAt = Start.AddHours(2), Game = "Just Chatting", Title = "вторая часть" },
                new() { StartedAt = Start.AddHours(2), EndedAt = Start.AddHours(3), Game = "Minecraft", Title = "вторая часть" },
            ],
        };

        Assert.That(new StreamSessionRowViewModel(session).GameFormatted, Is.EqualTo("Just Chatting (+1)"));
    }
}
