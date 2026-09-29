using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [TestCase(0, "сегодня")]
    [TestCase(-1, "сегодня")]
    [TestCase(1, "вчера")]
    [TestCase(2, "2 дн. назад")]
    [TestCase(30, "30 дн. назад")]
    [TestCase(31, "19.08.2026")]
    [TestCase(45, "05.08.2026")]
    public void Names_the_day_relative_to_now(int daysAgo, string expected)
    {
        var value = Now.AddDays(-daysAgo);

        Assert.That(RelativeTime.Describe(value, Now), Is.EqualTo(expected));
    }

    [TestCase(DateTimeKind.Utc)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Reads_a_bare_DateTime_as_utc(DateTimeKind kind)
    {
        var value = DateTime.SpecifyKind(new(2026, 9, 17, 12, 0, 0), kind);

        Assert.That(RelativeTime.Describe(value, Now), Is.EqualTo("2 дн. назад"));
    }

    [TestCase(0, "только что")]
    [TestCase(1, "1 мин. назад")]
    [TestCase(59, "59 мин. назад")]
    [TestCase(60, "1 ч. назад")]
    [TestCase(23 * 60, "23 ч. назад")]
    [TestCase(25 * 60, "вчера")]
    [TestCase(3 * 24 * 60, "3 дн. назад")]
    public void Names_the_moment_down_to_minutes(int minutesAgo, string expected)
    {
        var value = Now.AddMinutes(-minutesAgo);

        Assert.That(RelativeTime.DescribeMoment(value, Now), Is.EqualTo(expected));
    }

    [Test]
    public void Reads_a_moment_from_the_future_as_just_now()
    {
        Assert.That(RelativeTime.DescribeMoment(Now.AddMinutes(5), Now), Is.EqualTo("только что"));
    }

    [Test]
    public void Formats_a_date_by_the_russian_pattern()
    {
        Assert.That(RelativeTime.FormatDate(Now), Is.EqualTo("19.09.2026"));
    }
}
