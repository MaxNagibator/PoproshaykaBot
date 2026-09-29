using KeepShell.Services.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class EmbeddedTwitchAuthDialogTests
{
    private const string LeakedSecret = "abcdef0123456789";

    private static readonly EmbeddedTwitchAuthRequest Request = new(
        TwitchOAuthRole.Bot,
        "client-id",
        "client-secret",
        ["chat:read"],
        "http://localhost:8080/callback",
        true);

    [Test]
    public async Task Успешный_поток_отдаёт_токены_и_просит_закрыть_окно()
    {
        var oauth = new FakeOAuthService();
        using var viewModel = Create(oauth);
        viewModel.Configure(Request);

        var closeRequests = 0;
        viewModel.CloseRequested += (_, _) => closeRequests++;

        var flow = viewModel.RunFlowAsync();

        Assert.That(oauth.IsStarted, Is.True, "поток стартует синхронно и уводит вью-модель в ожидание");

        var expected = new OAuthFlowResult("access", "refresh", ["chat:read"], "bot", "42", 3600);
        oauth.Completion.SetResult(expected);
        await flow;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Result.Outcome, Is.EqualTo(EmbeddedTwitchAuthOutcome.Completed));
            Assert.That(viewModel.Result.Flow, Is.SameAs(expected));
            Assert.That(closeRequests, Is.EqualTo(1));
            Assert.That(oauth.HasStatusSubscribers, Is.False);
        }
    }

    [Test]
    public async Task Отмена_на_лету_гасит_поток_и_снимает_подписку_на_статус()
    {
        var oauth = new FakeOAuthService();
        using var viewModel = Create(oauth);
        viewModel.Configure(Request);

        var flow = viewModel.RunFlowAsync();

        Assert.That(oauth.HasStatusSubscribers, Is.True, "во время потока диалог слушает статус авторизации");

        viewModel.Cancel();
        await flow;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Result.Outcome, Is.EqualTo(EmbeddedTwitchAuthOutcome.Canceled));
            Assert.That(viewModel.Result.Flow, Is.Null);
            Assert.That(oauth.HasStatusSubscribers, Is.False, "отменённый поток не оставляет подписку на StatusChanged");
        }
    }

    [Test]
    public async Task Сбой_потока_не_выносит_наружу_текст_исключения()
    {
        var oauth = new FakeOAuthService();
        using var viewModel = Create(oauth);
        viewModel.Configure(Request);

        var closeRequests = 0;
        viewModel.CloseRequested += (_, _) => closeRequests++;

        var flow = viewModel.RunFlowAsync();

        oauth.Completion.SetException(new InvalidOperationException($"access_token={LeakedSecret}"));
        await flow;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Result.Outcome, Is.EqualTo(EmbeddedTwitchAuthOutcome.Failed));
            Assert.That(viewModel.Result.Message, Does.Not.Contain(LeakedSecret));
            Assert.That(viewModel.StatusMessage, Does.Not.Contain(LeakedSecret));
            Assert.That(viewModel.ErrorDescription, Does.Not.Contain(LeakedSecret));
            Assert.That(closeRequests, Is.Zero, "окно с ошибкой закрывает пользователь, а не поток");
            Assert.That(oauth.HasStatusSubscribers, Is.False);
        }
    }

    [Test]
    public void Отсутствие_WebView2_Runtime_переводит_диалог_в_состояние_недоступности()
    {
        var oauth = new FakeOAuthService();
        using var viewModel = Create(oauth);
        viewModel.Configure(Request);

        viewModel.ShowRuntimeMissing();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Result.Outcome, Is.EqualTo(EmbeddedTwitchAuthOutcome.Unavailable));
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.CanDownloadRuntime, Is.True, "пользователю предлагается ссылка на установку");
            Assert.That(viewModel.Result.Message, Does.Contain("браузере"));
        }
    }

    [Test]
    public async Task Статус_авторизации_доезжает_до_диалога_только_для_своей_роли()
    {
        var oauth = new FakeOAuthService();
        using var viewModel = Create(oauth);
        viewModel.Configure(Request);

        var flow = viewModel.RunFlowAsync();

        oauth.RaiseStatus(TwitchOAuthRole.Broadcaster, "статус чужой роли");
        oauth.RaiseStatus(TwitchOAuthRole.Bot, "Ожидание авторизации пользователя");

        Assert.That(viewModel.StatusMessage, Is.EqualTo("Ожидание авторизации пользователя"));

        viewModel.Cancel();
        await flow;
    }

    private static EmbeddedTwitchAuthDialogViewModel Create(FakeOAuthService oauth)
    {
        return new(oauth,
            new FakeShellLauncher(),
            new InlineUiDispatcher(),
            NullLogger<EmbeddedTwitchAuthDialogViewModel>.Instance);
    }

    private sealed class InlineUiDispatcher : IUiDispatcher
    {
        public bool HasAccess => true;

        public void Invoke(Action action)
        {
            action();
        }

        public IUiTimer CreateTimer(TimeSpan interval, Action tick)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeShellLauncher : IShellLauncher
    {
        public bool Open(string pathOrUrl)
        {
            return true;
        }

        public bool Reveal(string path)
        {
            return true;
        }

        public bool Start(string executable, params string[] arguments)
        {
            return true;
        }
    }

    private sealed class FakeOAuthService : ITwitchOAuthService
    {
        public event Action<TwitchOAuthRole, string>? StatusChanged;

        public bool IsStarted { get; private set; }

        public TaskCompletionSource<OAuthFlowResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? LastAuthUrl { get; private set; }

        public bool HasStatusSubscribers => StatusChanged is not null;

        public void RaiseStatus(TwitchOAuthRole role, string message)
        {
            StatusChanged?.Invoke(role, message);
        }

        public Task<OAuthFlowResult> StartOAuthFlowToDraftAsync(
            TwitchOAuthRole role,
            string clientId,
            string clientSecret,
            string[]? scopes,
            string? redirectUri,
            Action<string> onAuthUrlReady,
            bool checkBroadcasterChannel = true,
            CancellationToken ct = default)
        {
            LastAuthUrl = "https://id.twitch.tv/oauth2/authorize?client_id=client-id&state=stub";
            onAuthUrlReady(LastAuthUrl);
            IsStarted = true;

            return Completion.Task.WaitAsync(ct);
        }

        public Task<string> StartOAuthFlowAsync(
            TwitchOAuthRole role,
            string clientId,
            string clientSecret,
            string[]? scopes,
            string? redirectUri,
            Action<string> onAuthUrlReady,
            bool checkBroadcasterChannel = true,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public void SetAuthResult(string code, string? state)
        {
            throw new NotSupportedException();
        }

        public void SetAuthError(Exception exception)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsTokenValidAsync(string token, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<TokenValidationInfo?> ValidateAsync(string token, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> RefreshTokenAsync(
            TwitchOAuthRole role,
            string clientId,
            string clientSecret,
            string refreshToken,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetAccessTokenAsync(TwitchOAuthRole role, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public void UpdateSettings(
            TwitchOAuthRole role,
            string accessToken,
            string refreshToken,
            IEnumerable<string>? newScopes,
            string login,
            string userId,
            int expiresInSeconds = 0,
            bool publishAuthorizationRefreshed = false)
        {
            throw new NotSupportedException();
        }

        public void ClearTokens(TwitchOAuthRole role)
        {
            throw new NotSupportedException();
        }
    }
}
