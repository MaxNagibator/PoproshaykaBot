using System.Net;

namespace PoproshaykaBot.Core.Twitch.Helix;

public sealed class HelixMessageDroppedException(string reasonCode, string reasonMessage)
    : Exception($"Сообщение отклонено Twitch: {reasonCode} – {reasonMessage}")
{
    public string ReasonCode { get; } = reasonCode;
    public string ReasonMessage { get; } = reasonMessage;
}

public sealed class HelixRequestException(
    HttpMethod method,
    string requestPath,
    HttpStatusCode statusCode,
    string? twitchErrorMessage,
    string? responseBody,
    Exception? innerException = null)
    : Exception(BuildMessage(method, requestPath, statusCode, twitchErrorMessage), innerException)
{
    public HttpMethod Method { get; } = method;
    public string RequestPath { get; } = requestPath;
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? TwitchErrorMessage { get; } = twitchErrorMessage;
    public string? ResponseBody { get; } = responseBody;

    private static string BuildMessage(HttpMethod method, string path, HttpStatusCode code, string? twitchMessage)
    {
        return twitchMessage is null
            ? $"{method} {path} → {(int)code} {code}"
            : $"{method} {path} → {(int)code} {code}: {twitchMessage}";
    }
}
