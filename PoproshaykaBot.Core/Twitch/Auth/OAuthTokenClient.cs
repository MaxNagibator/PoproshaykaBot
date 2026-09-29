using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Twitch.Auth;

public sealed class OAuthTokenClient(
    IHttpClientFactory httpClientFactory,
    ILogger<OAuthTokenClient> logger)
{
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";
    private const string ValidateUrl = "https://id.twitch.tv/oauth2/validate";

    public async Task<bool> IsTokenValidAsync(string token, CancellationToken ct = default)
    {
        var info = await ValidateAsync(token, ct);
        return info != null;
    }

    public async Task<TokenValidationInfo?> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, ValidateUrl);
            request.Headers.Authorization = new("Bearer", token);

            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Validate-эндпойнт Twitch отклонил токен (StatusCode={StatusCode})", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<ValidateResponse>(json);

            if (dto == null)
            {
                logger.LogWarning("Validate-эндпойнт Twitch вернул 200, но тело не парсится как ValidateResponse");
                return null;
            }

            return new(dto.Login ?? string.Empty,
                dto.UserId ?? string.Empty,
                dto.ClientId ?? string.Empty,
                dto.Scopes ?? new(),
                dto.ExpiresIn);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Сетевая или внутренняя ошибка при проверке токена Twitch (validate-эндпойнт)");
            return null;
        }
    }

    public async Task<TokenResponse> PostTokenRequestAsync(Dictionary<string, string> formData, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();

        using var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync(TokenUrl, content, ct);
        var jsonResponse = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorStatus = (int)response.StatusCode;
            string? errorMessage = null;
            try
            {
                var errorDto = JsonSerializer.Deserialize<OAuthErrorResponse>(jsonResponse);
                if (errorDto != null)
                {
                    errorStatus = errorDto.Status != 0 ? errorDto.Status : errorStatus;
                    errorMessage = errorDto.Message;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Не удалось распарсить тело OAuth-ответа об ошибке. HttpStatus={HttpStatus}, BodyLength={BodyLength}",
                    response.StatusCode,
                    jsonResponse.Length);
            }

            logger.LogError("OAuth token-эндпойнт ответил ошибкой. HttpStatus={HttpStatus}, OAuthStatus={OAuthStatus}, OAuthMessage={OAuthMessage}",
                response.StatusCode,
                errorStatus,
                errorMessage ?? "(нет описания)");

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new OAuthRefreshRejectedException(errorStatus, errorMessage);
            }

            throw new OAuthTokenRequestException(response.StatusCode, errorStatus, errorMessage);
        }

        TokenResponse? tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Не удалось десериализовать ответ сервера токенов. HttpStatus={HttpStatus}, BodyLength={BodyLength}",
                response.StatusCode,
                jsonResponse.Length);

            throw new InvalidOperationException("Не удалось десериализовать ответ сервера", ex);
        }

        if (tokenResponse == null)
        {
            logger.LogError("Десериализация ответа сервера токенов вернула null. HttpStatus={HttpStatus}, BodyLength={BodyLength}",
                response.StatusCode,
                jsonResponse.Length);

            throw new InvalidOperationException("Не удалось десериализовать ответ сервера");
        }

        return tokenResponse;
    }
}

public record TokenValidationInfo(
    string Login,
    string UserId,
    string ClientId,
    IReadOnlyList<string> Scopes,
    int ExpiresIn);

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

internal sealed record OAuthErrorResponse(
    [property: JsonPropertyName("status")]
    int Status,
    [property: JsonPropertyName("message")]
    string? Message);

public sealed class OAuthRefreshRejectedException(int httpStatus, string? errorCode)
    : Exception($"OAuth refresh отвергнут сервером (HTTP {httpStatus}, code={errorCode ?? "–"})")
{
    public int HttpStatus { get; } = httpStatus;
    public string? ErrorCode { get; } = errorCode;
}

public sealed class OAuthTokenRequestException(HttpStatusCode httpStatus, int oauthStatus, string? errorMessage)
    : InvalidOperationException($"OAuth error {oauthStatus}: {errorMessage ?? "нет описания"}")
{
    public HttpStatusCode HttpStatus { get; } = httpStatus;
    public int OAuthStatus { get; } = oauthStatus;
    public string? ErrorMessage { get; } = errorMessage;

    public bool IsCredentialsRejected => HttpStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

internal sealed record ValidateResponse(
    [property: JsonPropertyName("client_id")]
    string? ClientId,
    [property: JsonPropertyName("login")]
    string? Login,
    [property: JsonPropertyName("user_id")]
    string? UserId,
    [property: JsonPropertyName("scopes")]
    List<string>? Scopes,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn);
