using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Tests.Users;

[TestFixture]
public sealed class UserRankServiceTests
{
    [TestCase(0, "🌱 БЕЗ РАНГА")]
    [TestCase(299, "🌱 БЕЗ РАНГА")]
    [TestCase(300, "♞ КОНЬ")]
    [TestCase(1500, "♜ ЛАДЬЯ")]
    [TestCase(-1, "⛓️ КАТОРЖНИК")]
    public void GetRankDisplay_ListWithoutZeroThreshold_NeverGivesAnUnreachedRank(long points, string expected)
    {
        var service = CreateService([new("♜", "ЛАДЬЯ", 1000), new("♞", "КОНЬ", 300)]);

        Assert.That(service.GetRankDisplay(points), Is.EqualTo(expected));
    }

    [Test]
    public void GetRank_BelowEveryThreshold_ReturnsUnranked()
    {
        var service = CreateService([new("♞", "КОНЬ", 300)]);

        Assert.That(service.GetRank(10), Is.SameAs(UserRank.Unranked));
    }

    [Test]
    public void GetRank_ListWithZeroThreshold_ReturnsLowestRankFromStart()
    {
        var service = CreateService([new("♞", "КОНЬ", 300), new("♟", "ПЕШКА", 0)]);

        Assert.That(service.GetRank(0).Name, Is.EqualTo("ПЕШКА"));
    }

    [Test]
    public void GetRank_EmptyList_KeepsBuiltInPawn()
    {
        var service = CreateService([]);

        Assert.That(service.GetRankDisplay(0), Is.EqualTo("♟ ПЕШКА III"));
    }

    private static UserRankService CreateService(List<UserRank> ranks)
    {
        var settings = new AppSettings();
        settings.Ranks.Ranks = ranks;

        var settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        settingsManager.Current.Returns(settings);

        return new(settingsManager);
    }
}
