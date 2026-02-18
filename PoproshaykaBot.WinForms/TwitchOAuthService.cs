using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Settings;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoproshaykaBot.WinForms;

public class TwitchOAuthService(
    SettingsManager settingsManager,
    IHttpClientFactory httpClientFactory,
    ILogger<TwitchOAuthService> logger)
{
    private readonly SemaphoreSlim _authSemaphore = new(1, 1);
    private TaskCompletionSource<string>? _authTcs;
    private string? _currentState;

    public event Action<string>? StatusChanged;

    public async Task<string> StartOAuthFlowAsync(string clientId, string clientSecret, string[]? scopes = null, string? redirectUri = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("ID клиента не может быть пустым", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new ArgumentException("Секрет клиента не может быть пустым", nameof(clientSecret));
        }

        await _authSemaphore.WaitAsync(ct);

        try
        {
            var settings = settingsManager.Current.Twitch;
            scopes ??= settings.Scopes;
            redirectUri ??= settings.RedirectUri;

            var scopeString = string.Join(" ", scopes);
            _currentState = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

            ReportStatus("Открытие браузера для авторизации...");

            var authUrl = $"https://id.twitch.tv/oauth2/authorize"
                          + $"?response_type=code"
                          + $"&client_id={clientId}"
                          + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                          + $"&scope={Uri.EscapeDataString(scopeString)}"
                          + $"&state={Uri.EscapeDataString(_currentState)}";

            _authTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true,
                });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Не удалось открыть браузер для авторизации");
                throw new InvalidOperationException($"Не удалось открыть браузер: {exception.Message}", exception);
            }

            ReportStatus("Ожидание авторизации пользователя (5 мин)...");

            string authorizationCode;
            try
            {
                authorizationCode = await _authTcs.Task.WaitAsync(TimeSpan.FromMinutes(5), ct);
            }
            catch (TimeoutException)
            {
                throw new OperationCanceledException("Время ожидания авторизации истекло");
            }

            if (string.IsNullOrEmpty(authorizationCode))
            {
                throw new InvalidOperationException("Не удалось получить код авторизации");
            }

            ReportStatus("Обмен кода на токен доступа...");
            var accessToken = await ExchangeCodeForTokenAsync(clientId, clientSecret, authorizationCode, redirectUri, ct);

            ReportStatus("Авторизация завершена успешно!");
            return accessToken;
        }
        finally
        {
            _authSemaphore.Release();
        }
    }

    public void SetAuthResult(string code, string? state)
    {
        if (_currentState != null && _currentState != state)
        {
            logger.LogWarning("Получен некорректный параметр state. Возможна CSRF атака.");
            _authTcs?.TrySetException(new SecurityException("Неверный параметр state. Авторизация отклонена в целях безопасности."));
            return;
        }

        _authTcs?.TrySetResult(code);
    }

    public void SetAuthError(Exception exception)
    {
        _authTcs?.TrySetException(exception);
    }

    public async Task<bool> IsTokenValidAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            var validateUrl = "https://id.twitch.tv/oauth2/validate";
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var response = await client.GetAsync(validateUrl);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ошибка при проверке токена");
            return false;
        }
    }

    public async Task<string> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token не может быть пустым", nameof(refreshToken));
        }

        ReportStatus("Обновление токена доступа...");

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" },
        };

        var tokenResponse = await PostTokenRequestAsync(formData, ct);

        UpdateSettings(tokenResponse.AccessToken, tokenResponse.RefreshToken);

        ReportStatus("Токен доступа обновлен успешно!");
        return tokenResponse.AccessToken;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var settings = settingsManager.Current.Twitch;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw new InvalidOperationException("OAuth настройки не настроены (ClientId/ClientSecret).");
        }

        var token = await GetValidTokenOrRefreshAsync(ct);
        if (token != null)
        {
            return token;
        }

        var accessToken = await StartOAuthFlowAsync(settings.ClientId,
            settings.ClientSecret,
            settings.Scopes,
            settings.RedirectUri,
            ct);

        logger.LogInformation("OAuth авторизация завершена успешно.");
        return accessToken;
    }

    public async Task<string?> GetValidTokenOrRefreshAsync(CancellationToken ct = default)
    {
        var settings = settingsManager.Current.Twitch;

        if (!string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            ReportStatus("Проверка действительности токена...");
            if (await IsTokenValidAsync(settings.AccessToken))
            {
                logger.LogInformation("Используется сохранённый токен доступа.");
                return settings.AccessToken;
            }

            if (!string.IsNullOrWhiteSpace(settings.RefreshToken))
            {
                try
                {
                    var tokenResponse = await RefreshTokenAsync(settings.ClientId, settings.ClientSecret, settings.RefreshToken, ct);
                    logger.LogInformation("Токен доступа обновлён.");
                    return tokenResponse;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Не удалось обновить токен доступа");
                }
            }
        }

        return null;
    }

    public void UpdateSettings(string accessToken, string refreshToken)
    {
        var settings = settingsManager.Current;
        settings.Twitch.AccessToken = accessToken;
        settings.Twitch.RefreshToken = refreshToken;
        settingsManager.SaveSettings(settings);
    }

    public void ClearTokens()
    {
        var settings = settingsManager.Current;
        settings.Twitch.AccessToken = string.Empty;
        settings.Twitch.RefreshToken = string.Empty;
        settingsManager.SaveSettings(settings);
        ReportStatus("Токены очищены.");
    }

    private void ReportStatus(string status)
    {
        logger.LogInformation("OAuth Status: {Status}", status);
        StatusChanged?.Invoke(status);
    }

    private async Task<string> ExchangeCodeForTokenAsync(string clientId, string clientSecret, string authorizationCode, string redirectUri, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", authorizationCode },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirectUri },
        };

        var tokenResponse = await PostTokenRequestAsync(formData, ct);

        UpdateSettings(tokenResponse.AccessToken, tokenResponse.RefreshToken);

        return tokenResponse.AccessToken;
    }

    private async Task<TokenResponse> PostTokenRequestAsync(Dictionary<string, string> formData, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        var tokenUrl = "https://id.twitch.tv/oauth2/token";

        using var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync(tokenUrl, content, ct);
        var jsonResponse = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Ошибка запроса токена. Статус: {StatusCode}, Ответ: {Response}", response.StatusCode, jsonResponse);
            throw new InvalidOperationException($"Ошибка запроса токена: {jsonResponse}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Не удалось десериализовать ответ сервера");
        }

        return tokenResponse;
    }
}

public record TokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    [property: JsonPropertyName("refresh_token")]
    string RefreshToken,
    [property: JsonPropertyName("scope")]
    List<string> Scope,
    [property: JsonPropertyName("token_type")]
    string TokenType
);
