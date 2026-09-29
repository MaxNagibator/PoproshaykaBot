using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Twitch.Helix;

public abstract class TwitchAuthHandlerBase(
    ITwitchOAuthService oauthService,
    SettingsManager settingsManager,
    AccountsStore accountsStore,
    ILogger logger)
    : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<bool> KeepTokenOnUnauthorized = new("poproshayka.keep-token-on-401");

    protected abstract TwitchOAuthRole Role { get; }

    protected abstract string MissingTokenHint { get; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(oauthService, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new TwitchAuthorizationMissingException($"Токен Twitch для роли {Role} отсутствует или недействителен. {MissingTokenHint}");
        }

        request.Headers.Authorization = new("Bearer", token);
        request.Headers.TryAddWithoutValidation("Client-Id", settingsManager.Current.Twitch.ClientId);

        var response = await base.SendAsync(request, cancellationToken);

        if ((int)response.StatusCode != 401)
        {
            return response;
        }

        if (request.Options.TryGetValue(KeepTokenOnUnauthorized, out var keepToken) && keepToken)
        {
            logger.LogDebug("Helix запрос {Method} {Path} (роль {Role}) вернул 401 – отказ относится к самой операции, токен не очищается",
                request.Method,
                request.RequestUri?.AbsolutePath,
                Role);

            return response;
        }

        logger.LogWarning("Helix запрос {Method} {Path} (роль {Role}) вернул 401 – пробуем очистить сохранённый токен",
            request.Method,
            request.RequestUri?.AbsolutePath,
            Role);

        var cleared = accountsStore.TryClearAccessToken(Role, token);

        if (!cleared)
        {
            logger.LogDebug("Helix 401 (роль {Role}): токен в хранилище уже сменился, оставляем без изменений",
                Role);
        }

        return response;
    }

    protected abstract Task<string?> GetTokenAsync(ITwitchOAuthService oauthService, CancellationToken cancellationToken);
}
