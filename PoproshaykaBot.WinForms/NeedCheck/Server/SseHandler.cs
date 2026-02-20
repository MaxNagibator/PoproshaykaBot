using System.Net;

namespace PoproshaykaBot.WinForms.Services.Http.Handlers;

public sealed class SseHandler(SseService sseService) : IHttpHandler
{
    public Task HandleAsync(HttpListenerContext context)
    {
        sseService.AddClient(context.Response);
        return Task.CompletedTask;
    }
}
