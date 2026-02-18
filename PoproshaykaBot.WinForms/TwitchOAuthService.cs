using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Settings;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoproshaykaBot.WinForms;

public class TwitchOAuthService(
    SettingsManager settingsManager,
    IHttpClientFactory httpClientFactory,
    ILogger<TwitchOAuthService> logger)
{
    private TaskCompletionSource<string>? _authTcs;

    public event Action<string>? StatusChanged;

    public async Task<string> StartOAuthFlowAsync(string clientId, string clientSecret, string[]? scopes = null, string? redirectUri = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("ID клиента не может быть пустым", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new ArgumentException("Секрет клиента не может быть пустым", nameof(clientSecret));
        }

        var settings = settingsManager.Current.Twitch;
        scopes ??= settings.Scopes;
        redirectUri ??= settings.RedirectUri;

        var scopeString = string.Join(" ", scopes);

        ReportStatus("Открытие браузера для авторизации...");

        var authUrl = $"https://id.twitch.tv/oauth2/authorize"
                      + $"?response_type=code"
                      + $"&client_id={clientId}"
                      + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                      + $"&scope={Uri.EscapeDataString(scopeString)}";

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

        ReportStatus("Ожидание авторизации пользователя...");

        var authorizationCode = await _authTcs.Task;

        if (string.IsNullOrEmpty(authorizationCode))
        {
            throw new InvalidOperationException("Не удалось получить код авторизации");
        }

        ReportStatus("Обмен кода на токен доступа...");
        var accessToken = await ExchangeCodeForTokenAsync(clientId, clientSecret, authorizationCode, redirectUri);

        ReportStatus("Авторизация завершена успешно!");
        return accessToken;
    }

    public void SetAuthResult(string code)
    {
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

    public async Task<TokenResponse> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token не может быть пустым", nameof(refreshToken));
        }

        ReportStatus("Обновление токена доступа...");

        var client = httpClientFactory.CreateClient();
        var tokenUrl = "https://id.twitch.tv/oauth2/token";

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" },
        };

        using var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync(tokenUrl, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Ошибка обновления токена. Статус: {StatusCode}, Ответ: {Response}", response.StatusCode, jsonResponse);
            throw new InvalidOperationException($"Ошибка обновления токена: {jsonResponse}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Не удалось десериализовать ответ сервера");
        }

        ReportStatus("Токен доступа обновлен успешно!");
        return tokenResponse;
    }

    public async Task<string> GetValidTokenAsync(string clientId, string clientSecret, string currentToken, string refreshToken)
    {
        ReportStatus("Проверка действительности токена...");

        if (await IsTokenValidAsync(currentToken))
        {
            ReportStatus("Токен действителен");
            return currentToken;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Токен недействителен и refresh token отсутствует. Требуется повторная авторизация.");
        }

        var tokenResponse = await RefreshTokenAsync(clientId, clientSecret, refreshToken);
        return tokenResponse.AccessToken;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var settings = settingsManager.Current.Twitch;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            MessageBox.Show("OAuth настройки не настроены (ClientId/ClientSecret).", "Ошибка конфигурации OAuth", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            if (await IsTokenValidAsync(settings.AccessToken))
            {
                logger.LogInformation("Используется сохранённый токен доступа.");
                return settings.AccessToken;
            }

            if (!string.IsNullOrWhiteSpace(settings.RefreshToken))
            {
                try
                {
                    logger.LogInformation("Обновление токена доступа...");

                    var validToken = await GetValidTokenAsync(settings.ClientId,
                        settings.ClientSecret,
                        settings.AccessToken,
                        settings.RefreshToken);

                    logger.LogInformation("Токен доступа обновлён.");
                    return validToken;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Не удалось обновить токен доступа");
                    MessageBox.Show($"Не удалось обновить токен доступа: {ex.Message}", "Ошибка обновления токена", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        try
        {
            var accessToken = await StartOAuthFlowAsync(settings.ClientId,
                settings.ClientSecret,
                settings.Scopes,
                settings.RedirectUri);

            logger.LogInformation("OAuth авторизация завершена успешно.");
            return accessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth авторизация не удалась");
            MessageBox.Show($"OAuth авторизация не удалась: {ex.Message}", "Ошибка OAuth авторизации", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }

    private void ReportStatus(string status)
    {
        logger.LogInformation("OAuth Status: {Status}", status);
        StatusChanged?.Invoke(status);
    }

    private async Task<string> ExchangeCodeForTokenAsync(string clientId, string clientSecret, string authorizationCode, string redirectUri)
    {
        var client = httpClientFactory.CreateClient();
        var tokenUrl = "https://id.twitch.tv/oauth2/token";

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", authorizationCode },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirectUri },
        };

        using var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync(tokenUrl, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Ошибка получения токена. Статус: {StatusCode}, Ответ: {Response}", response.StatusCode, jsonResponse);
            throw new InvalidOperationException($"Ошибка получения токена: {jsonResponse}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Не удалось десериализовать ответ сервера");
        }

        var settings = settingsManager.Current;
        settings.Twitch.AccessToken = tokenResponse.AccessToken;
        settings.Twitch.RefreshToken = tokenResponse.RefreshToken;
        settingsManager.SaveSettings(settings);

        return tokenResponse.AccessToken;
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
