using PoproshaykaBot.Core.Server.Images;
using PoproshaykaBot.Core.Settings.Obs;

namespace PoproshaykaBot.Core.Tests.Server.Images;

[TestFixture]
public sealed class MessageImageLinkDetectorTests
{
    private static readonly string[] AllowedHosts = ["i.imgur.com", "cdn.7tv.app"];

    [TestCase("https://i.imgur.com/abc.png", "https://i.imgur.com/abc.png")]
    [TestCase("смотри https://i.imgur.com/abc.JPG вот", "https://i.imgur.com/abc.JPG")]
    [TestCase("https://i.imgur.com/abc.png?width=200", "https://i.imgur.com/abc.png?width=200")]
    [TestCase("(https://i.imgur.com/abc.gif)", "https://i.imgur.com/abc.gif")]
    [TestCase("https://i.imgur.com/abc.webp.", "https://i.imgur.com/abc.webp")]
    [TestCase("https://evil.example/x.png https://cdn.7tv.app/ok.png", "https://cdn.7tv.app/ok.png")]
    public void TryFindFirst_AllowedLink_IsFound(string message, string expected)
    {
        Assert.That(MessageImageLinkDetector.TryFindFirst(message, AllowedHosts, out var link), Is.True);
        Assert.That(link.Uri.AbsoluteUri, Is.EqualTo(expected));
    }

    [TestCase("https://i.imgur.com/abc.png", "https://i.imgur.com/abc.png")]
    [TestCase("смотри https://i.imgur.com/abc.JPG вот", "https://i.imgur.com/abc.JPG")]
    [TestCase("(https://i.imgur.com/abc.gif)", "https://i.imgur.com/abc.gif")]
    [TestCase("«https://i.imgur.com/abc.webp».", "https://i.imgur.com/abc.webp")]
    [TestCase("  https://i.imgur.com/a.png  ", "https://i.imgur.com/a.png")]
    [TestCase("https://evil.example/x.png https://cdn.7tv.app/ok.png", "https://cdn.7tv.app/ok.png")]
    public void TryFindFirst_RangeCoversLinkTextWithoutNoise(string message, string linkText)
    {
        Assert.That(MessageImageLinkDetector.TryFindFirst(message, AllowedHosts, out var link), Is.True);
        Assert.That(message[link.StartIndex..(link.EndIndex + 1)], Is.EqualTo(linkText),
            "Оверлей прячет ровно этот отрезок текста, когда картинка загрузилась.");
    }

    [TestCase("")]
    [TestCase("просто текст без ссылок")]
    [TestCase("http://i.imgur.com/abc.png")]
    [TestCase("https://evil.example/abc.png")]
    [TestCase("https://i.imgur.com/abc.svg")]
    [TestCase("https://i.imgur.com/abc")]
    [TestCase("https://user@i.imgur.com/abc.png")]
    [TestCase("https://127.0.0.1/abc.png")]
    [TestCase("https://i.imgur.com:8443/abc.png")]
    [TestCase("ftp://i.imgur.com/abc.png")]
    public void TryFindFirst_RefusedLink_IsNotFound(string message)
    {
        Assert.That(MessageImageLinkDetector.TryFindFirst(message, AllowedHosts, out _), Is.False);
    }

    [Test]
    public void TryFindFirst_EmptyWhitelist_FindsNothing()
    {
        Assert.That(MessageImageLinkDetector.TryFindFirst("https://i.imgur.com/abc.png", [], out _), Is.False,
            "Пустой белый список означает «ничего не разрешено», а не «разрешено всё».");
    }

    [Test]
    public void TryFindFirst_TakesFirstAllowedLinkOnly()
    {
        var found = MessageImageLinkDetector.TryFindFirst(
            "https://i.imgur.com/one.png https://i.imgur.com/two.png",
            AllowedHosts,
            out var link);

        Assert.That(found, Is.True);
        Assert.That(link.Uri.AbsoluteUri, Is.EqualTo("https://i.imgur.com/one.png"));
    }

    [Test]
    public void DefaultAllowedHosts_SurviveNormalization()
    {
        var normalized = MessageImageHosts.Normalize(ObsChatSettings.DefaultMessageImageAllowedHosts);

        Assert.That(normalized, Is.EquivalentTo(ObsChatSettings.DefaultMessageImageAllowedHosts),
            "Список по умолчанию обязан проходить собственную нормализацию, иначе он молча обнуляется при первом сохранении.");
    }

    [TestCase("i.imgur.com", true)]
    [TestCase("  I.Imgur.com  ", true)]
    [TestCase("i.imgur.com.", true)]
    [TestCase("localhost", false)]
    [TestCase("127.0.0.1", false)]
    [TestCase("https://i.imgur.com", false)]
    [TestCase("i.imgur.com/path", false)]
    [TestCase("i.imgur.com:443", false)]
    [TestCase("evil@i.imgur.com", false)]
    [TestCase("*.imgur.com", false)]
    [TestCase("", false)]
    public void TryNormalizeHost_AcceptsOnlyPlainDomainNames(string raw, bool expected)
    {
        Assert.That(MessageImageHosts.TryNormalizeHost(raw, out _), Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_DropsDuplicatesAndKeepsOrder()
    {
        var normalized = MessageImageHosts.Normalize(["i.imgur.com", "I.IMGUR.COM", "cdn.7tv.app", "мусор"]);

        Assert.That(normalized, Is.EqualTo(new[] { "i.imgur.com", "cdn.7tv.app" }));
    }
}
