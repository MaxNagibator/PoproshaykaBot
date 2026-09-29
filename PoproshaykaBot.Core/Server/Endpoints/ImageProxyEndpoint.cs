using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Server.Images;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using System.Net;

namespace PoproshaykaBot.Core.Server.Endpoints;

internal sealed class ImageProxyEndpoint(
    ObsChatStore obsChatStore,
    IHttpClientFactory httpClientFactory,
    ILogger<ImageProxyEndpoint>? logger = null) : IEndpointMapper
{
    public const string HttpClientName = "message-image-proxy";
    public const int MaxContentBytes = 5 * 1024 * 1024;
    public const int MaxRedirects = 3;

    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(MessageImagePolicy.ProxyPath, async (HttpContext ctx) =>
        {
            var settings = obsChatStore.Load();

            if (!settings.ShowMessageImages)
            {
                Refuse("картинки в сообщениях выключены");
                return Results.NotFound();
            }

            var requested = ctx.Request.Query[MessageImagePolicy.UrlQueryKey].FirstOrDefault();

            if (!MessageImageUrlValidator.IsAllowed(requested, settings.MessageImageAllowedHosts, out var uri))
            {
                Refuse("ссылка не прошла проверку (схема, хост или расширение)");
                return Results.NotFound();
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            timeout.CancelAfter(RequestTimeout);

            try
            {
                var image = await FetchAsync(uri, settings, timeout.Token);

                if (image == null)
                {
                    return Results.NotFound();
                }

                ctx.Response.Headers.CacheControl = "public, max-age=3600";
                ctx.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.Bytes(image.Value.Content, image.Value.MediaType);
            }
            catch (OperationCanceledException) when (!ctx.RequestAborted.IsCancellationRequested)
            {
                Refuse($"превышен таймаут {RequestTimeout.TotalSeconds} с");
                return Results.NotFound();
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                Refuse($"запрос не удался: {exception.Message}");
                return Results.NotFound();
            }
        });
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static async Task<byte[]?> ReadCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();

        var chunk = new byte[16 * 1024];

        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);

            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaxContentBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }

    private async Task<(byte[] Content, string MediaType)?> FetchAsync(Uri uri, ObsChatSettings settings, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var current = uri;

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                if (!MessageImageUrlValidator.TryParseRedirect(current, response.Headers.Location, out var next)
                    || !MessageImageHosts.IsAllowed(next.Host, settings.MessageImageAllowedHosts))
                {
                    Refuse("редирект уводит на другой хост");
                    return null;
                }

                current = next;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                Refuse($"источник ответил {(int)response.StatusCode}");
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (!MessageImageUrlValidator.IsAllowedMediaType(mediaType))
            {
                Refuse($"тип ответа {mediaType ?? "неизвестен"}, а не картинка");
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxContentBytes)
            {
                Refuse($"тело больше {MaxContentBytes} байт");
                return null;
            }

            var content = await ReadCappedAsync(response.Content, cancellationToken);

            if (content == null)
            {
                Refuse($"тело больше {MaxContentBytes} байт");
                return null;
            }

            return (content, mediaType!);
        }

        Refuse($"больше {MaxRedirects} редиректов подряд");
        return null;
    }

    private void Refuse(string reason)
    {
        logger?.LogDebug("GET {Path}: картинка не отдана – {Reason}", MessageImagePolicy.ProxyPath, reason);
    }
}
