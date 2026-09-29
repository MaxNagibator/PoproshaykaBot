using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using System.Net;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Auth;

[TestFixture]
public sealed class TwitchOAuthServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "twitch-oauth-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settings = new();
        _settings.Twitch.ClientId = "test-client-id";
        _settings.Twitch.ClientSecret = "test-client-secret";
        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(_settings);
        _accountsStore = new(filePath: Path.Combine(_tempDir, "accounts.json"));

        _httpFactory = Substitute.For<IHttpClientFactory>();
        _handler = new();
        _httpFactory.CreateClient(Arg.Any<string>()).Returns(_ => new(_handler, false));
        _eventBus = Substitute.For<IEventBus>();
        _clock = new() { UtcNow = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero) };

        var statusReporter = new OAuthStatusReporter(NullLogger<OAuthStatusReporter>.Instance);
        var tokenClient = new OAuthTokenClient(_httpFactory, NullLogger<OAuthTokenClient>.Instance);
        var accountWriter = new OAuthAccountWriter(_accountsStore, _eventBus, statusReporter, NullLogger<OAuthAccountWriter>.Instance);
        var flow = new OAuthFlowCoordinator(tokenClient, accountWriter, statusReporter, _accountsStore, _settingsManager, NullLogger<OAuthFlowCoordinator>.Instance);
        var refresher = new OAuthTokenRefresher(tokenClient, accountWriter, statusReporter, _accountsStore, _settingsManager, _eventBus, _clock, NullLogger<OAuthTokenRefresher>.Instance);

        _service = new(flow, refresher, tokenClient, accountWriter, statusReporter);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        _handler.Dispose();
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private string _tempDir = null!;
    private AppSettings _settings = null!;
    private SettingsManager _settingsManager = null!;
    private AccountsStore _accountsStore = null!;
    private IHttpClientFactory _httpFactory = null!;
    private FakeHttpMessageHandler _handler = null!;
    private IEventBus _eventBus = null!;
    private TestTimeProvider _clock = null!;
    private TwitchOAuthService _service = null!;

    [Test]
    public async Task UpdateSettings_DefaultsToNotPublishingAuthorizationRefreshed()
    {
        _service.UpdateSettings(TwitchOAuthRole.Bot,
            "access-token",
            "refresh-token",
            null,
            "bot-login",
            "user-id");

        await _eventBus.DidNotReceive()
            .PublishAsync(Arg.Any<TwitchAuthorizationRefreshed>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateSettings_WithFlag_PublishesAuthorizationRefreshedForRole()
    {
        _service.UpdateSettings(TwitchOAuthRole.Bot,
            "access-token",
            "refresh-token",
            null,
            "bot-login",
            "user-id",
            publishAuthorizationRefreshed: true);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<TwitchAuthorizationRefreshed>(e => e.Role == TwitchOAuthRole.Bot),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateSettings_WithFlagForBroadcaster_PublishesEventForBroadcaster()
    {
        _service.UpdateSettings(TwitchOAuthRole.Broadcaster,
            "access-token",
            "refresh-token",
            null,
            "streamer-login",
            "user-id",
            publishAuthorizationRefreshed: true);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<TwitchAuthorizationRefreshed>(e => e.Role == TwitchOAuthRole.Broadcaster),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAccessToken_WhenRefreshRejectedWith400_ClearsTokensAndPublishesStatusAndDoesNotLoopOnNextCall()
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(HttpStatusCode.BadRequest,
            "{\"status\":400,\"message\":\"Invalid refresh token\"}");

        var token = await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);

        Assert.That(token, Is.Null);

        var bot = _accountsStore.LoadBot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bot.AccessToken, Is.Empty);
            Assert.That(bot.RefreshToken, Is.Empty);
            Assert.That(bot.AccessTokenExpiresAt, Is.Null);
        }

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<BotConnectionStatusUpdated>(e => e.Message.Contains("повторная авторизация")),
                Arg.Any<CancellationToken>());

        var requestsAfterFirst = _handler.Requests.Count;
        Assert.That(requestsAfterFirst, Is.EqualTo(1));

        var second = await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.Null);
            Assert.That(_handler.Requests.Count, Is.EqualTo(requestsAfterFirst),
                "Повторный GetAccessTokenAsync не должен снова идти в Twitch – токены очищены");
        }
    }

    [Test]
    public void RefreshTokenAsync_WhenServerReturns400_RethrowsRejectionAndClearsTokens()
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(HttpStatusCode.BadRequest,
            "{\"status\":400,\"message\":\"Invalid refresh token\"}");

        Assert.ThrowsAsync<OAuthRefreshRejectedException>(async () =>
            await _service.RefreshTokenAsync(TwitchOAuthRole.Bot,
                _settings.Twitch.ClientId,
                _settings.Twitch.ClientSecret,
                "stale-refresh"));

        var bot = _accountsStore.LoadBot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bot.AccessToken, Is.Empty);
            Assert.That(bot.RefreshToken, Is.Empty);
        }
    }

    [Test]
    public async Task GetAccessToken_WhenRefreshFailsWith500_KeepsTokensForLaterRetry()
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(HttpStatusCode.InternalServerError,
            "{\"status\":500,\"message\":\"transient\"}");

        var token = await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);

        Assert.That(token, Is.Null);

        var bot = _accountsStore.LoadBot();
        Assert.That(bot.RefreshToken, Is.EqualTo("stale-refresh"),
            "Transient ошибки не должны сжигать refresh-токен");
    }

    [TestCase(HttpStatusCode.Forbidden, 300)]
    [TestCase(HttpStatusCode.InternalServerError, 15)]
    public async Task GetAccessToken_WhenRefreshFails_HoldsRetriesUntilBackoffElapses(HttpStatusCode status, int backoffSeconds)
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(status, $"{{\"status\":{(int)status},\"message\":\"boom\"}}");

        Assert.That(await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot), Is.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));

        _clock.UtcNow += TimeSpan.FromSeconds(backoffSeconds - 1);
        Assert.That(await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot), Is.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(1),
            "Пока выдержка не истекла, повторный запрос к Twitch не уходит");

        _clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.That(await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot), Is.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(2),
            "После выдержки попытка повторяется");
    }

    [Test]
    public async Task GetAccessToken_WhenTransientFailuresRepeat_BackoffGrows()
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(HttpStatusCode.InternalServerError, "{\"status\":500,\"message\":\"boom\"}");

        await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);
        _clock.UtcNow += TimeSpan.FromSeconds(15);
        await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);
        Assert.That(_handler.Requests, Has.Count.EqualTo(2));

        _clock.UtcNow += TimeSpan.FromSeconds(15);
        await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);
        Assert.That(_handler.Requests, Has.Count.EqualTo(2),
            "Вторая неудача подряд удваивает выдержку до 30 секунд");

        _clock.UtcNow += TimeSpan.FromSeconds(15);
        await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);
        Assert.That(_handler.Requests, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetAccessToken_WhenRefreshSucceedsAfterFailure_ClearsBackoff()
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(HttpStatusCode.InternalServerError, "{\"status\":500,\"message\":\"boom\"}");

        await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);

        _clock.UtcNow += TimeSpan.FromSeconds(15);
        _handler.Responder = request => request.RequestUri!.AbsoluteUri.Contains("validate", StringComparison.Ordinal)
            ? MakeJson(HttpStatusCode.OK, "{\"login\":\"bot-login\",\"user_id\":\"bot-id\",\"client_id\":\"test-client-id\",\"scopes\":[],\"expires_in\":3600}")
            : MakeJson(HttpStatusCode.OK, "{\"access_token\":\"fresh-access\",\"expires_in\":3600,\"refresh_token\":\"fresh-refresh\",\"scope\":[],\"token_type\":\"bearer\"}");

        Assert.That(await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot), Is.EqualTo("fresh-access"));

        _accountsStore.Mutate(TwitchOAuthRole.Bot, account => account.AccessTokenExpiresAt = _clock.UtcNow.AddMinutes(-1));
        _handler.Responder = _ => MakeJson(HttpStatusCode.InternalServerError, "{\"status\":500,\"message\":\"boom\"}");

        var requestsBefore = _handler.Requests.Count;
        await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);

        Assert.That(_handler.Requests, Has.Count.EqualTo(requestsBefore + 1),
            "Успешное обновление снимает выдержку, накопленную прошлыми сбоями");
    }

    [Test]
    public void RefreshTokenAsync_WhenServerRejectsCredentials_KeepsBackoffForAutomaticPath()
    {
        SeedExpiredBotTokens();
        _handler.Responder = _ => MakeJson(HttpStatusCode.Forbidden,
            "{\"status\":403,\"message\":\"invalid client secret\"}");

        Assert.ThrowsAsync<OAuthTokenRequestException>(async () =>
            await _service.RefreshTokenAsync(TwitchOAuthRole.Bot,
                _settings.Twitch.ClientId,
                _settings.Twitch.ClientSecret,
                "stale-refresh"));

        var requestsAfterManual = _handler.Requests.Count;

        Assert.That(_service.GetAccessTokenAsync(TwitchOAuthRole.Bot).Result, Is.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(requestsAfterManual),
            "Неудачная ручная попытка ставит роль на выдержку, а не снимает её");
    }

    [Test]
    public async Task Черновик_переавторизации_с_новым_правом_не_сбрасывает_токен_как_смену_набора()
    {
        string[] previous = [TwitchScopes.UserReadChat, TwitchScopes.UserWriteChat, TwitchScopes.UserBot];

        _accountsStore.Mutate(TwitchOAuthRole.Bot, account =>
        {
            account.AccessToken = "old-access";
            account.Scopes = previous;
            account.StoredScopes = previous;
        });

        var draft = _accountsStore.Load(TwitchOAuthRole.Bot);

        new OAuthFlowResult("fresh-access", "fresh-refresh", [..TwitchScopes.BotRequired, ..TwitchScopes.BotOptional], "bot-login", "bot-id", 3600)
            .ApplyTo(draft, _clock.UtcNow);

        _accountsStore.SaveAll(draft, _accountsStore.Load(TwitchOAuthRole.Broadcaster));

        var token = await _service.GetAccessTokenAsync(TwitchOAuthRole.Bot);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.EqualTo("fresh-access"),
                "Выданные права черновик несёт вместе с запрошенными – иначе первая же выдача токена приняла бы переавторизацию за смену набора");

            Assert.That(TwitchScopes.IsGranted(_accountsStore.LoadBot(), TwitchScopes.UserManageWhispers), Is.True);
        });

        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync(default(BotConnectionStatusUpdated)!, default);
    }

    private void SeedExpiredBotTokens()
    {
        _accountsStore.Mutate(TwitchOAuthRole.Bot, account =>
        {
            account.AccessToken = "stale-access";
            account.RefreshToken = "stale-refresh";
            account.AccessTokenExpiresAt = _clock.UtcNow.AddMinutes(-1);
            account.Login = "bot-login";
            account.UserId = "bot-id";
        });
    }

    private static HttpResponseMessage MakeJson(HttpStatusCode status, string body)
    {
        return new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = Responder?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NotImplemented);
            return Task.FromResult(response);
        }
    }
}
