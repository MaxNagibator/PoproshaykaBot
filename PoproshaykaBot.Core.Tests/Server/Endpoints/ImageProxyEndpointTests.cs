using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Server.Endpoints;
using PoproshaykaBot.Core.Server.Images;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Users;
using System.Net;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Server.Endpoints;

[TestFixture]
public sealed class ImageProxyEndpointTests
{
    private const string Source = "https://i.imgur.com/abc.png";

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "poproshayka-image-proxy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _bus = new(NullLogger<InMemoryEventBus>.Instance);
        _store = new(_bus, NullLogger<ObsChatStore>.Instance, Path.Combine(_tempDir, "obs-chat.json"));
        _logger = new();
        _upstream = new();
    }

    [TearDown]
    public void TearDown()
    {
        _upstream.Dispose();

        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private string _tempDir = null!;
    private InMemoryEventBus _bus = null!;
    private ObsChatStore _store = null!;
    private RecordingLogger<ImageProxyEndpoint> _logger = null!;
    private StubUpstreamHandler _upstream = null!;

    [Test]
    public async Task Get_FeatureDisabled_Returns404AndNeverTouchesNetwork()
    {
        _upstream.Respond(Png);

        var result = await GetAsync(Source);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(_upstream.Requests, Is.Empty,
            "Выключенная настройка обязана гасить эндпоинт до похода наружу.");
    }

    [Test]
    public async Task Get_AllowedSource_ReturnsImageBytes()
    {
        Enable();
        _upstream.Respond(Png);

        var result = await GetAsync(Source);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result.MediaType, Is.EqualTo("image/png"));
            Assert.That(result.Body, Has.Length.EqualTo(4));
        }
    }

    [TestCase("http://i.imgur.com/abc.png", TestName = "Get_PlainHttp_Returns404")]
    [TestCase("https://evil.example/abc.png", TestName = "Get_HostOutsideWhitelist_Returns404")]
    [TestCase("https://127.0.0.1/abc.png", TestName = "Get_IpLiteralHost_Returns404")]
    [TestCase("https://evil.example@i.imgur.com/abc.png", TestName = "Get_UserInfoInHost_Returns404")]
    [TestCase("https://i.imgur.com/abc.svg", TestName = "Get_NonImageExtension_Returns404")]
    public async Task Get_RefusedUrl_Returns404WithoutNetwork(string url)
    {
        Enable();
        _upstream.Respond(Png);

        var result = await GetAsync(url);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(_upstream.Requests, Is.Empty);
        AssertRefusedQuietly();
    }

    [Test]
    public async Task Get_HtmlResponse_Returns404()
    {
        Enable();
        _upstream.Respond(() => new(HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>", Encoding.UTF8, "text/html"),
        });

        var result = await GetAsync(Source);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.NotFound));
        AssertRefusedQuietly();
    }

    [Test]
    public async Task Get_BodyOverCap_Returns404()
    {
        Enable();
        _upstream.Respond(() =>
        {
            var content = new ByteArrayContent(new byte[ImageProxyEndpoint.MaxContentBytes + 1024]);
            content.Headers.ContentType = new("image/png");
            return new(HttpStatusCode.OK) { Content = content };
        });

        var result = await GetAsync(Source);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.NotFound));
        AssertRefusedQuietly();
    }

    [Test]
    public async Task Get_RedirectToAnotherHost_Returns404()
    {
        Enable();
        _upstream.Respond(() =>
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new("https://evil.example/abc.png");
            return redirect;
        });

        var result = await GetAsync(Source);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(_upstream.Requests, Has.Count.EqualTo(1),
            "Редирект на чужой хост отклоняется до второго запроса.");

        AssertRefusedQuietly();
    }

    [Test]
    public async Task Get_RedirectWithinSameHost_IsFollowed()
    {
        Enable();
        _upstream.Respond(() =>
        {
            if (_upstream.Requests.Count == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new("https://i.imgur.com/moved.png");
                return redirect;
            }

            return Png();
        });

        var result = await GetAsync(Source);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_upstream.Requests[1], Is.EqualTo("https://i.imgur.com/moved.png"));
    }

    [Test]
    public async Task Get_HostRemovedFromWhitelist_Returns404()
    {
        Enable(settings => settings.MessageImageAllowedHosts = ["cdn.7tv.app"]);
        _upstream.Respond(Png);

        var result = await GetAsync(Source);

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.NotFound),
            "Белый список проверяется в самом прокси, иначе подделанный u= обходит детектор.");

        Assert.That(_upstream.Requests, Is.Empty);
    }

    [Test]
    public async Task ImageRoute_IsMappedThroughAssembledEndpointList()
    {
        Enable();

        using var server = await EndpointTestServer.CreateAllAsync(services =>
        {
            services.AddRouting();
            services.AddLogging();
            services.AddHttpClient();
            services.AddSingleton<IEventBus>(_bus);
            services.AddSingleton(_store);
            services.AddSingleton(BuildSettingsManager());
            services.AddSingleton(sp => new ChatHistoryManager(sp.GetRequiredService<SettingsManager>(), sp.GetRequiredService<IEventBus>()));
            services.AddSingleton(Substitute.For<ITwitchOAuthService>());
            services.AddSingleton(sp => new UserProfileImageProvider(sp, NullLogger<UserProfileImageProvider>.Instance));
            services.AddHttpServer();
        });

        using var client = server.CreateClient();
        using var response = await client.GetAsync("/api/image?u=" + Uri.EscapeDataString("https://evil.example/abc.png"));

        Assert.That(response.Headers.TryGetValues(EndpointTestServer.MatchedEndpointHeader, out var matched), Is.True,
            "Маршрут /api/image не поднят собранным списком IEndpointMapper.");

        Assert.That(string.Join(",", matched!), Does.Contain(MessageImagePolicy.ProxyPath));
    }

    private static SettingsManager BuildSettingsManager()
    {
        var manager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        manager.Current.Returns(new AppSettings());
        return manager;
    }

    private static HttpResponseMessage Png()
    {
        var content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        content.Headers.ContentType = new("image/png");

        return new(HttpStatusCode.OK) { Content = content };
    }

    private void Enable(Action<ObsChatSettings>? tweak = null)
    {
        var settings = _store.Load();
        settings.ShowMessageImages = true;
        tweak?.Invoke(settings);
        _store.Save(settings);
    }

    private Task<EndpointTestServer> CreateServerAsync()
    {
        return EndpointTestServer.CreateAsync(services =>
            {
                services.AddRouting();
                services.AddSingleton(_store);
                services.AddSingleton<ILogger<ImageProxyEndpoint>>(_logger);
                services.AddHttpClient(ImageProxyEndpoint.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => _upstream);
            },
            sp => new ImageProxyEndpoint(
                sp.GetRequiredService<ObsChatStore>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<ImageProxyEndpoint>>()));
    }

    private async Task<ProxyResult> GetAsync(string url)
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/api/image?u=" + Uri.EscapeDataString(url));

        var body = await response.Content.ReadAsByteArrayAsync();

        return new(response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
    }

    private void AssertRefusedQuietly()
    {
        Assert.That(_logger.Entries.Any(entry => entry.Level == LogLevel.Debug), Is.True,
            "Об отказе должна остаться строка Debug.");

        Assert.That(_logger.Entries.Any(entry => entry.Level >= LogLevel.Warning), Is.False,
            "Отказ по ссылке из чата – рутина, а не повод для Warning в журнале пользователя.");
    }

    private sealed record ProxyResult(HttpStatusCode Status, string? MediaType, byte[] Body);

    private sealed class StubUpstreamHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _factory = () => new(HttpStatusCode.NotFound);

        public List<string> Requests { get; } = [];

        public void Respond(Func<HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
            return Task.FromResult(_factory());
        }
    }
}
