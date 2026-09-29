using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Twitch.Chat;

public sealed class BotUserIdProvider : IBotUserIdProvider, IDisposable
{
    private readonly ITwitchHelixClient _helix;
    private readonly ILogger<BotUserIdProvider> _logger;
    private readonly IDisposable _lifecycleSubscription;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private UserInfo? _cachedUser;

    public BotUserIdProvider(
        [FromKeyedServices(TwitchEndpoints.HelixBotClient)]
        ITwitchHelixClient helix,
        IEventBus eventBus,
        ILogger<BotUserIdProvider> logger)
    {
        _helix = helix;
        _logger = logger;
        _lifecycleSubscription = eventBus.Subscribe<BotLifecyclePhaseChanged>(OnLifecyclePhaseChanged);
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        return user?.Id;
    }

    public async Task<UserInfo?> GetUserAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_cachedUser != null)
            {
                return _cachedUser;
            }

            var user = await _helix.GetAuthenticatedUserAsync(cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Bot user не получен через /users – ответ пустой");
                return null;
            }

            _cachedUser = user;
            _logger.LogInformation("Bot user получен через токен: {UserId} ({Login})", user.Id, user.Login);
            return _cachedUser;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lifecycleSubscription.Dispose();
        _lock.Dispose();
    }

    private void OnLifecyclePhaseChanged(BotLifecyclePhaseChanged evt)
    {
        if (evt.Phase == BotLifecyclePhase.Disconnecting)
        {
            _cachedUser = null;
        }
    }
}
