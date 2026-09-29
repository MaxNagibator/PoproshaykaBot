using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Debugging;

public sealed class ChannelLiveStatusReader(
    IHttpClientFactory httpClientFactory,
    SettingsManager settingsManager,
    TimeProvider timeProvider,
    ILogger<ChannelLiveStatusReader> logger)
    : IDisposable
{
    public const int MaxLoginsPerRequest = 100;

    public const string MissingCredentialsReason = "Не заданы Client ID и Client Secret приложения Twitch – их вводят в разделе «Авторизация».";
    public const string CredentialsRejectedReason = "Twitch не принял Client ID и Client Secret приложения – проверьте их в разделе «Авторизация».";
    public const string TokenRejectedReason = "Twitch отклонил ключ приложения. Нажмите «Обновить», чтобы запросить новый.";
    public const string NetworkFailedReason = "Twitch не ответил – проверьте подключение к интернету и нажмите «Обновить».";
    public const string UnexpectedResponseReason = "Twitch ответил в неожиданном виде. Нажмите «Обновить» чуть позже.";

    private static readonly TimeSpan TokenExpirySkew = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    private string? _appToken;
    private string? _appTokenOwner;
    private DateTimeOffset _appTokenExpiresAt;

    public async Task<ChannelLiveStatusReport> ReadAsync(IEnumerable<string> logins, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logins);

        var requested = logins
            .Select(login => ChannelLogin.TryNormalize(login, out var normalized) ? normalized : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxLoginsPerRequest)
            .ToArray();

        if (requested.Length == 0)
        {
            return new([], null);
        }

        var twitch = settingsManager.Current.Twitch;
        var clientId = twitch.ClientId?.Trim() ?? string.Empty;
        var clientSecret = twitch.ClientSecret?.Trim() ?? string.Empty;

        if (clientId.Length == 0 || clientSecret.Length == 0)
        {
            return Unknown(requested, MissingCredentialsReason);
        }

        try
        {
            var token = await GetAppTokenAsync(clientId, clientSecret, cancellationToken);

            return token is null
                ? Unknown(requested, CredentialsRejectedReason)
                : await QueryStreamsAsync(requested, clientId, token, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Twitch ответил в неожиданном виде на запрос состояния недавних каналов отладки");
            return Unknown(requested, UnexpectedResponseReason);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Состояние недавних каналов отладки не получено");
            return Unknown(requested, NetworkFailedReason);
        }
    }

    public void Dispose()
    {
        _tokenGate.Dispose();
    }

    private static ChannelLiveStatusReport Unknown(IEnumerable<string> logins, string reason)
    {
        return new(logins.Select(login => new ChannelLiveStatus(login, ChannelLiveState.Unknown)).ToArray(), reason);
    }

    private static ChannelLiveStatusReport Map(IReadOnlyList<string> requested, IReadOnlyList<HelixStreamDto?> streams)
    {
        var live = new Dictionary<string, HelixStreamDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var stream in streams)
        {
            if (stream is { UserLogin: { Length: > 0 } login }
                && string.Equals(stream.Type, "live", StringComparison.OrdinalIgnoreCase))
            {
                live.TryAdd(login, stream);
            }
        }

        var channels = requested
            .Select(login => live.TryGetValue(login, out var stream)
                ? ToLiveStatus(login, stream)
                : new(login, ChannelLiveState.Offline))
            .ToArray();

        return new(channels, null);
    }

    private static ChannelLiveStatus ToLiveStatus(string login, HelixStreamDto stream)
    {
        DateTimeOffset? startedAt = stream.StartedAt == default
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(stream.StartedAt, DateTimeKind.Utc));

        return new(login,
            ChannelLiveState.Live,
            Math.Max(0, stream.ViewerCount),
            NullIfBlank(stream.Title),
            NullIfBlank(stream.GameName),
            startedAt);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task<ChannelLiveStatusReport> QueryStreamsAsync(
        IReadOnlyList<string> requested,
        string clientId,
        string token,
        CancellationToken cancellationToken)
    {
        var query = string.Join('&', requested.Select(login => $"user_login={Uri.EscapeDataString(login)}"));

        using var client = httpClientFactory.CreateClient(TwitchEndpoints.HelixAppClient);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{TwitchEndpoints.HelixStreams}?{query}&first={MaxLoginsPerRequest}");
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.TryAddWithoutValidation("Client-Id", clientId);

        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await ForgetAppTokenAsync(token);
            logger.LogWarning("Helix не принял ключ приложения при запросе состояния недавних каналов отладки (401), ключ сброшен из памяти");
            return Unknown(requested, TokenRejectedReason);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Helix ответил {Status} на запрос состояния недавних каналов отладки", (int)response.StatusCode);
            return Unknown(requested, $"Twitch ответил ошибкой {(int)response.StatusCode}. Нажмите «Обновить» чуть позже.");
        }

        var envelope = await response.Content.ReadFromJsonAsync<HelixEnvelope<HelixStreamDto>>(JsonOptions, cancellationToken);

        if (envelope?.Data is not { } streams)
        {
            logger.LogWarning("Helix ответил без поля data на запрос состояния недавних каналов отладки");
            return Unknown(requested, UnexpectedResponseReason);
        }

        return Map(requested, streams);
    }

    private async Task<string?> GetAppTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var owner = $"{clientId}\n{clientSecret}";

        await _tokenGate.WaitAsync(cancellationToken);

        try
        {
            if (_appToken is { } cached
                && string.Equals(_appTokenOwner, owner, StringComparison.Ordinal)
                && _appTokenExpiresAt - timeProvider.GetUtcNow() > TokenExpirySkew)
            {
                return cached;
            }

            using var client = httpClientFactory.CreateClient(TwitchEndpoints.HelixAppClient);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials",
            });

            using var response = await client.PostAsync(TwitchEndpoints.OAuthToken, content, cancellationToken);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning("Twitch не выдал ключ приложения для запроса состояния каналов: {Status}", (int)response.StatusCode);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<AppTokenResponse>(JsonOptions, cancellationToken);

            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new JsonException("Ответ на client_credentials без access_token");
            }

            _appToken = token.AccessToken;
            _appTokenOwner = owner;
            _appTokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(0, token.ExpiresIn));

            return _appToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private async Task ForgetAppTokenAsync(string token)
    {
        await _tokenGate.WaitAsync();

        try
        {
            if (string.Equals(_appToken, token, StringComparison.Ordinal))
            {
                _appToken = null;
                _appTokenOwner = null;
            }
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private sealed record AppTokenResponse(
        [property: JsonPropertyName("access_token")]
        string? AccessToken,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn);
}
