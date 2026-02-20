using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Settings;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TwitchLib.Api;

namespace PoproshaykaBot.WinForms;

public sealed class TwitchOAuthService(
    SettingsManager settingsManager,
    IHttpClientFactory httpClientFactory,
    ILogger<TwitchOAuthService> logger,
    TwitchAPI twitchApi)
    : IDisposable
{
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _authSemaphore = new(1, 1);
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    private TaskCompletionSource<string>? _authTcs;
    private string? _currentState;
    private bool _isDisposed;

    public event Action<string>? StatusChanged;

    public async Task<string> StartOAuthFlowAsync(string clientId, string clientSecret, string[]? scopes = null, string? redirectUri = null, CancellationToken ct = default)
    {
        logger.LogDebug("Начало процесса OAuth авторизации для ClientId: {ClientId}", clientId);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogWarning("Попытка запуска OAuth с пустым ClientId");
            throw new ArgumentException("ID клиента не может быть пустым", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            logger.LogWarning("Попытка запуска OAuth с пустым ClientSecret для ClientId: {ClientId}", clientId);
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

            logger.LogDebug("Сгенерирован state параметр и сформирован URL авторизации для ClientId: {ClientId}", clientId);
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
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true,
                });

                logger.LogInformation("Браузер для авторизации успешно открыт (ClientId: {ClientId})", clientId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Сбой при открытии браузера для авторизации (ClientId: {ClientId})", clientId);
                throw new InvalidOperationException($"Не удалось открыть браузер: {ex.Message}", ex);
            }

            ReportStatus($"Ожидание авторизации пользователя ({AuthTimeout.TotalMinutes} мин)...");

            logger.LogDebug("Ожидание callback-ответа от пользователя (таймаут {TimeoutMinutes} минут)", AuthTimeout.TotalMinutes);

            string authorizationCode;
            try
            {
                authorizationCode = await _authTcs.Task.WaitAsync(AuthTimeout, ct);
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Истекло время ожидания авторизации пользователя ({TimeoutMinutes} минут) (ClientId: {ClientId})", AuthTimeout.TotalMinutes, clientId);
                throw new OperationCanceledException($"Время ожидания авторизации истекло ({AuthTimeout.TotalMinutes} мин)", ex);
            }

            if (string.IsNullOrEmpty(authorizationCode))
            {
                logger.LogError("Получен пустой код авторизации (ClientId: {ClientId})", clientId);
                throw new InvalidOperationException("Не удалось получить код авторизации");
            }

            ReportStatus("Обмен кода на токен доступа...");
            logger.LogDebug("Выполнение обмена authorization_code на токены");

            var accessToken = await ExchangeCodeForTokenAsync(clientId, clientSecret, authorizationCode, redirectUri, ct);

            ReportStatus("Авторизация завершена успешно!");
            logger.LogInformation("Процесс OAuth авторизации успешно завершен (ClientId: {ClientId})", clientId);

            return accessToken;
        }
        finally
        {
            _authTcs?.TrySetCanceled(ct);
            _authTcs = null;
            _currentState = null;

            _authSemaphore.Release();
            logger.LogDebug("Семафор авторизации освобожден и состояние TCS очищено");
        }
    }

    public void SetAuthResult(string code, string? state)
    {
        logger.LogDebug("Получен callback авторизации (State: {State})", state);

        if (_currentState != null && _currentState != state)
        {
            logger.LogWarning("Несовпадение параметра state. Ожидался: {ExpectedState}, Получен: {ActualState}. Возможна CSRF атака", _currentState, state);

            var securityEx = new SecurityException("Неверный параметр state. Авторизация отклонена в целях безопасности.");
            _authTcs?.TrySetException(securityEx);
            return;
        }

        logger.LogInformation("Callback авторизации успешно прошел проверку state");
        _authTcs?.TrySetResult(code);
    }

    public void SetAuthError(Exception exception)
    {
        logger.LogError(exception, "Получена ошибка в процессе обработки callback-ответа авторизации");
        _authTcs?.TrySetException(exception);
    }

    public async Task<bool> IsTokenValidAsync(string token)
    {
        logger.LogDebug("Запуск проверки валидности Access-токена");

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Передан пустой токен для проверки валидности");
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            var validateUrl = "https://id.twitch.tv/oauth2/validate";
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            var response = await client.GetAsync(validateUrl);
            var isValid = response.IsSuccessStatusCode;

            logger.LogInformation("Результат проверки токена: {IsValid} (StatusCode: {StatusCode})", isValid, response.StatusCode);
            return isValid;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Сетевая или внутренняя ошибка при проверке токена Twitch API");
            return false;
        }
    }

    public async Task<string> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct = default)
    {
        logger.LogDebug("Запрос на обновление токена (refresh_token) для ClientId: {ClientId}", clientId);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            logger.LogError("Попытка обновления токена с пустым параметром refresh_token");
            throw new ArgumentException("Refresh token не может быть пустым", nameof(refreshToken));
        }

        await _refreshSemaphore.WaitAsync(ct);
        try
        {
            return await RefreshTokenInternalAsync(clientId, clientSecret, refreshToken, ct);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        logger.LogDebug("Запрошен Access-токен приложения");
        var settings = settingsManager.Current.Twitch;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            logger.LogError("Не настроены базовые параметры OAuth: отсутствуют ClientId или ClientSecret");
            throw new InvalidOperationException("OAuth настройки не настроены (ClientId/ClientSecret).");
        }

        var token = await GetValidTokenOrRefreshAsync(ct);
        if (token != null)
        {
            logger.LogDebug("Возвращен существующий или обновленный токен");
            return token;
        }

        logger.LogInformation("Валидный токен не найден, инициируется новый OAuth-поток");
        var accessToken = await StartOAuthFlowAsync(settings.ClientId,
            settings.ClientSecret,
            settings.Scopes,
            settings.RedirectUri,
            ct);

        return accessToken;
    }

    public async Task<string?> GetValidTokenOrRefreshAsync(CancellationToken ct = default)
    {
        await _refreshSemaphore.WaitAsync(ct);
        try
        {
            var settings = settingsManager.Current.Twitch;

            if (!string.IsNullOrWhiteSpace(settings.AccessToken))
            {
                ReportStatus("Проверка действительности токена...");
                logger.LogDebug("Проверка существующего Access-токена");

                if (await IsTokenValidAsync(settings.AccessToken))
                {
                    SyncTwitchApi(settings.AccessToken);
                    logger.LogInformation("Существующий токен доступа валиден и будет использован");
                    return settings.AccessToken;
                }

                logger.LogInformation("Текущий токен недействителен, попытка использования Refresh-токена");

                if (!string.IsNullOrWhiteSpace(settings.RefreshToken))
                {
                    try
                    {
                        var tokenResponse = await RefreshTokenInternalAsync(settings.ClientId, settings.ClientSecret, settings.RefreshToken, ct);
                        return tokenResponse;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Критический сбой при попытке обновить токен доступа (ClientId: {ClientId})", settings.ClientId);
                    }
                }
                else
                {
                    logger.LogWarning("Refresh-токен отсутствует, обновление невозможно");
                }
            }
            else
            {
                logger.LogDebug("Сохраненный Access-токен отсутствует");
            }

            return null;
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    public void UpdateSettings(string accessToken, string refreshToken)
    {
        logger.LogDebug("Обновление токенов в настройках приложения");

        var settings = settingsManager.Current;
        settings.Twitch.AccessToken = accessToken;
        settings.Twitch.RefreshToken = refreshToken;
        settingsManager.SaveSettings(settings);

        SyncTwitchApi(accessToken);
        logger.LogInformation("Настройки приложения успешно обновлены новыми токенами");
    }

    public void ClearTokens()
    {
        logger.LogDebug("Запрос на очистку сохраненных токенов");

        var settings = settingsManager.Current;
        settings.Twitch.AccessToken = string.Empty;
        settings.Twitch.RefreshToken = string.Empty;
        settingsManager.SaveSettings(settings);
        twitchApi.Settings.AccessToken = string.Empty;

        ReportStatus("Токены очищены.");
        logger.LogInformation("Токены Twitch API успешно удалены из настроек");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        logger.LogDebug("Освобождение ресурсов TwitchOAuthService");

        StatusChanged = null;

        _authSemaphore.Dispose();
        _refreshSemaphore.Dispose();

        _isDisposed = true;
    }

    private async Task<string> RefreshTokenInternalAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
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
        logger.LogInformation("Токен доступа успешно обновлен через RefreshToken (ClientId: {ClientId})", clientId);

        return tokenResponse.AccessToken;
    }

    private void SyncTwitchApi(string accessToken)
    {
        logger.LogDebug("Синхронизация ClientId и Access-токена с инстансом TwitchAPI");
        var settings = settingsManager.Current.Twitch;
        twitchApi.Settings.ClientId = settings.ClientId;
        twitchApi.Settings.AccessToken = accessToken;
    }

    private void ReportStatus(string status)
    {
        logger.LogInformation("Изменение UI статуса OAuth: {Status}", status);
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
        logger.LogDebug("Отправка POST-запроса на получение токена в Twitch API (GrantType: {GrantType})", formData.GetValueOrDefault("grant_type", "unknown"));

        var client = httpClientFactory.CreateClient();
        var tokenUrl = "https://id.twitch.tv/oauth2/token";

        using var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync(tokenUrl, content, ct);
        var jsonResponse = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Ошибка HTTP-запроса при получении токена. StatusCode: {StatusCode}, ErrorPayload: {ErrorResponse}", response.StatusCode, jsonResponse);
            throw new InvalidOperationException($"Ошибка запроса токена: {jsonResponse}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

        if (tokenResponse == null)
        {
            logger.LogError("Не удалось десериализовать успешный ответ от Twitch API. Payload: {JsonResponse}", jsonResponse);
            throw new InvalidOperationException("Не удалось десериализовать ответ сервера");
        }

        logger.LogDebug("Ответ Twitch API успешно десериализован");
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
