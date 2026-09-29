using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Server.Endpoints;
using System.Net;

namespace PoproshaykaBot.Core.Tests.Server.Endpoints;

[TestFixture]
public sealed class StaticAssetsEndpointTests
{
    private static Task<EndpointTestServer> CreateServerAsync()
    {
        return EndpointTestServer.CreateAsync(services => services.AddRouting(),
            _ => new StaticAssetsEndpoint());
    }

    [TestCase("/chat", "text/html")]
    [TestCase("/assets/obs.css", "text/css")]
    [TestCase("/assets/obs.js", "application/javascript")]
    [TestCase("/favicon.ico", "image/x-icon")]
    public async Task GetStaticAsset_ReturnsExpectedContentType(string path, string expectedContentTypePrefix)
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync(path);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo(expectedContentTypePrefix));
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes, Is.Not.Empty,
            "Embedded-asset не должен быть пустым – это значит, что <EmbeddedResource> в Core.csproj отвалился");
    }

    [Test]
    public async Task GetUnknownPath_Returns404()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/assets/unknown.css");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
