using QueryStringMasker = PoproshaykaBot.Core.Server.QueryStringMasker;

namespace PoproshaykaBot.Core.Tests.Server;

[TestFixture]
public sealed class QueryStringMaskerTests
{
    [Test]
    public void Mask_Empty_ReturnsEmpty()
    {
        Assert.That(QueryStringMasker.Mask(""), Is.EqualTo(""));
    }

    [Test]
    public void Mask_NoQuery_ReturnsAsIs()
    {
        Assert.That(QueryStringMasker.Mask("?foo=bar&baz=42"), Is.EqualTo("?foo=bar&baz=42"));
    }

    [TestCase("?code=abc123", "?code=***")]
    [TestCase("?code=abc123&state=xyz", "?code=***&state=xyz")]
    [TestCase("?state=xyz&code=abc123", "?state=xyz&code=***")]
    [TestCase("?error=foo&code=abc123&state=xyz", "?error=foo&code=***&state=xyz")]
    public void Mask_OAuthCode_IsRedacted(string input, string expected)
    {
        Assert.That(QueryStringMasker.Mask(input), Is.EqualTo(expected));
    }

    [TestCase("?access_token=secret", "?access_token=***")]
    [TestCase("?refresh_token=secret", "?refresh_token=***")]
    public void Mask_TokensInQuery_AreRedacted(string input, string expected)
    {
        Assert.That(QueryStringMasker.Mask(input), Is.EqualTo(expected));
    }

    [TestCase("?u=https%3A%2F%2Fi.imgur.com%2Fabc.png", "?u=***")]
    [TestCase("?u=https%3A%2F%2Fi.imgur.com%2Fabc.png&x=1", "?u=***&x=1")]
    [TestCase("?x=1&u=https%3A%2F%2Fi.imgur.com%2Fabc.png", "?x=1&u=***")]
    public void Mask_ImageProxyUrl_IsRedacted(string input, string expected)
    {
        Assert.That(QueryStringMasker.Mask(input), Is.EqualTo(expected));
    }

    [TestCase("?menu=main")]
    [TestCase("?value=u")]
    public void Mask_KeysEndingWithU_AreLeftAlone(string input)
    {
        Assert.That(QueryStringMasker.Mask(input), Is.EqualTo(input));
    }

    [Test]
    public void Mask_IsCaseInsensitiveOnKey()
    {
        Assert.That(QueryStringMasker.Mask("?Code=ABC"), Is.EqualTo("?Code=***"));
    }

    [Test]
    public void Mask_PreservesEmptyValue()
    {
        Assert.That(QueryStringMasker.Mask("?code=&state=xyz"), Is.EqualTo("?code=&state=xyz"));
    }
}
