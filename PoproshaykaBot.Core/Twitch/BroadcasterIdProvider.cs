using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Twitch;

public sealed class BroadcasterIdProvider(
    [FromKeyedServices(TwitchEndpoints.HelixBotClient)]
    ITwitchHelixClient helix,
    ITargetChannelProvider targetChannelProvider,
    ILogger<BroadcasterIdProvider> logger)
    : IBroadcasterIdProvider
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedChannel;
    private string? _cachedId;

    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        var channel = targetChannelProvider.Current.Login;

        if (string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (string.Equals(_cachedChannel, channel, StringComparison.OrdinalIgnoreCase) && _cachedId != null)
            {
                return _cachedId;
            }

            UserInfo? user;
            try
            {
                user = await helix.GetUserByLoginAsync(channel, cancellationToken);
            }
            catch (TwitchAuthorizationMissingException ex)
            {
                logger.LogDebug(ex, "Broadcaster id не получен для канала {Channel}: токен Bot отсутствует. Ожидание авторизации.", channel);
                return null;
            }

            if (user == null)
            {
                logger.LogWarning("Broadcaster id не получен для канала {Channel}", channel);
                return null;
            }

            _cachedChannel = channel;
            _cachedId = user.Id;
            return _cachedId;
        }
        finally
        {
            _lock.Release();
        }
    }
}
