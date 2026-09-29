using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Server.Endpoints;
using PoproshaykaBot.Core.Twitch.Auth;
using System.Net;

namespace PoproshaykaBot.Core.Tests.Server.Endpoints;

[TestFixture]
public sealed class OAuthCallbackEndpointTests
{
    [SetUp]
    public void SetUp()
    {
        _oauth = Substitute.For<ITwitchOAuthService>();
    }

    private ITwitchOAuthService _oauth = null!;

    private Task<EndpointTestServer> CreateServerAsync()
    {
        return EndpointTestServer.CreateAsync(services =>
            {
                services.AddRouting();
                services.AddSingleton(_oauth);
            },
            sp => new OAuthCallbackEndpoint(sp.GetRequiredService<ITwitchOAuthService>()));
    }

    [Test]
    public async Task GetCallback_WithCode_ForwardsCodeAndReturnsSuccessHtml()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/?code=auth-code-123&state=stateToken");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        }

        _oauth.Received().SetAuthResult("auth-code-123", "stateToken");
        _oauth.DidNotReceive().SetAuthError(Arg.Any<Exception>());
    }

    [Test]
    public async Task GetCallback_WithError_ForwardsErrorAndReturnsHtmlWithEncodedReason()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/?error=access_denied");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        }

        _oauth.Received().SetAuthError(Arg.Is<Exception>(e => e.Message.Contains("access_denied", StringComparison.Ordinal)));
        _oauth.DidNotReceive().SetAuthResult(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Test]
    public async Task GetCallback_NoQuery_Returns400()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        _oauth.DidNotReceive().SetAuthResult(Arg.Any<string>(), Arg.Any<string?>());
        _oauth.DidNotReceive().SetAuthError(Arg.Any<Exception>());
    }

    [Test]
    public async Task GetCallback_ErrorWithSpecialCharacters_AreHtmlEncoded()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/?error=%3Cscript%3E");

        var body = await response.Content.ReadAsStringAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(body, Does.Not.Contain("<script>"),
                "Сырой error из query не должен попадать в HTML – иначе XSS на странице авторизации");

            Assert.That(body, Does.Contain("&lt;script&gt;").Or.Contain("&#x3C;script&#x3E;"));
        }
    }
}
