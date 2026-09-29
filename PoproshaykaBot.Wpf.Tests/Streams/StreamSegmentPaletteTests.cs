using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
public class StreamSegmentPaletteTests
{
    [TestCase("Just Chatting")]
    [TestCase("Software and Game Development")]
    [TestCase("Minecraft")]
    public void Одна_игра_получает_один_оттенок(string game)
    {
        Assert.That(StreamSegmentPalette.IndexOf(game), Is.EqualTo(StreamSegmentPalette.IndexOf(game)));
    }

    [TestCase("Minecraft", "minecraft")]
    [TestCase("Just Chatting", "JUST CHATTING")]
    public void Регистр_названия_оттенок_не_меняет(string first, string second)
    {
        Assert.That(StreamSegmentPalette.IndexOf(first), Is.EqualTo(StreamSegmentPalette.IndexOf(second)));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Сегмент_без_категории_идёт_нейтральным(string? game)
    {
        Assert.That(StreamSegmentPalette.IndexOf(game), Is.EqualTo(StreamSegmentPalette.NeutralSlot));
    }

    [Test]
    public void Названная_игра_нейтральный_слот_не_занимает()
    {
        string[] games =
        [
            "Just Chatting",
            "Software and Game Development",
            "Minecraft",
            "Escape from Tarkov",
            "Dota 2",
            "Counter-Strike 2",
            "Elden Ring",
            "Music",
        ];

        var slots = games.Select(StreamSegmentPalette.IndexOf).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(slots, Has.All.InRange(0, StreamSegmentPalette.NeutralSlot - 1));
            Assert.That(slots.Distinct().Count(), Is.GreaterThan(1),
                "восемь разных игр, слипшихся в один оттенок, делают полосу снова безликой");
        });
    }
}
