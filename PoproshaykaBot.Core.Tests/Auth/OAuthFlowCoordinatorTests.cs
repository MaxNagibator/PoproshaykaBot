using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Tests.Auth;

[TestFixture]
public sealed class OAuthFlowCoordinatorTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "oauth-flow-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var settings = new AppSettings();
        settings.Twitch.RedirectUri = "http://localhost:3000/callback";

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(settings);

        _accountsStore = new(filePath: Path.Combine(_tempDir, "accounts.json"));
        _statusReporter = new(NullLogger<OAuthStatusReporter>.Instance);

        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());

        var tokenClient = new OAuthTokenClient(httpFactory, NullLogger<OAuthTokenClient>.Instance);
        var accountWriter = new OAuthAccountWriter(_accountsStore, Substitute.For<IEventBus>(), _statusReporter, NullLogger<OAuthAccountWriter>.Instance);

        _coordinator = new(tokenClient, accountWriter, _statusReporter, _accountsStore, _settingsManager, NullLogger<OAuthFlowCoordinator>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _coordinator.Dispose();

        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public async Task StartOAuthFlow_WhenSameRoleAlreadyRunning_ReportsWaitingInsteadOfSilentQueue()
    {
        using var firstUrlReady = new ManualResetEventSlim();
        using var secondUrlReady = new ManualResetEventSlim();
        using var waitingReported = new ManualResetEventSlim();
        using var cts = new CancellationTokenSource();

        _statusReporter.StatusChanged += (_, status) =>
        {
            if (status.Contains("уже идёт авторизация", StringComparison.OrdinalIgnoreCase))
            {
                waitingReported.Set();
            }
        };

        var first = StartFlowAsync(firstUrlReady, cts.Token);
        Assert.That(firstUrlReady.Wait(TimeSpan.FromSeconds(5)), Is.True, "первый поток не дошёл до выдачи URL");

        var second = StartFlowAsync(secondUrlReady, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(waitingReported.Wait(TimeSpan.FromSeconds(5)), Is.True, "второй запрос не сообщил об ожидании");
            Assert.That(secondUrlReady.IsSet, Is.False, "второй запрос построил URL, не дождавшись первого");
        });

        await cts.CancelAsync();
        await AssertCanceledAsync(first);
        await AssertCanceledAsync(second);
    }

    [Test]
    public async Task StartOAuthFlow_WhenOtherRoleRunning_DoesNotWait()
    {
        using var botUrlReady = new ManualResetEventSlim();
        using var broadcasterUrlReady = new ManualResetEventSlim();
        using var cts = new CancellationTokenSource();

        var bot = StartFlowAsync(botUrlReady, cts.Token);
        Assert.That(botUrlReady.Wait(TimeSpan.FromSeconds(5)), Is.True);

        var broadcaster = StartFlowAsync(broadcasterUrlReady, cts.Token, TwitchOAuthRole.Broadcaster);
        Assert.That(broadcasterUrlReady.Wait(TimeSpan.FromSeconds(5)), Is.True, "роль стримера ждала семафор бота");

        await cts.CancelAsync();
        await AssertCanceledAsync(bot);
        await AssertCanceledAsync(broadcaster);
    }

    private static async Task AssertCanceledAsync(Task flow)
    {
        try
        {
            await flow;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Task StartFlowAsync(ManualResetEventSlim urlReady, CancellationToken ct, TwitchOAuthRole role = TwitchOAuthRole.Bot)
    {
        return _coordinator.StartOAuthFlowToDraftAsync(role,
            "test-client-id",
            "test-client-secret",
            ["chat:read"],
            "http://localhost:3000/callback",
            _ => urlReady.Set(),
            false,
            ct);
    }

    private string _tempDir = null!;
    private SettingsManager _settingsManager = null!;
    private AccountsStore _accountsStore = null!;
    private OAuthStatusReporter _statusReporter = null!;
    private OAuthFlowCoordinator _coordinator = null!;
}
