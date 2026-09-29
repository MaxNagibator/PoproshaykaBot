using PoproshaykaBot.Core.Debugging;

namespace PoproshaykaBot.Core.Tests.Debugging;

[TestFixture]
public class ChannelLoginTests
{
    [TestCase("MrBeast", "mrbeast")]
    [TestCase("  @MrBeast ", "mrbeast")]
    [TestCase("https://www.twitch.tv/MrBeast", "mrbeast")]
    [TestCase("twitch.tv/mrbeast/videos", "mrbeast")]
    [TestCase("https://m.twitch.tv/mrbeast?tt_content=home", "mrbeast")]
    [TestCase("bob_217", "bob_217")]
    public void TryNormalize_WithAcceptedForms_ReturnsLogin(string raw, string expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChannelLogin.TryNormalize(raw, out var login), Is.True);
            Assert.That(login, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("боб")]
    [TestCase("bob 217")]
    [TestCase("bob-217")]
    [TestCase("thischannelnameiswaytoolongfortwitch")]
    public void TryNormalize_WithRejectedForms_ReturnsFalse(string raw)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChannelLogin.TryNormalize(raw, out var login), Is.False);
            Assert.That(login, Is.Null);
        });
    }
}
