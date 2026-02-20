using System.Net;
using System.Text;

namespace PoproshaykaBot.WinForms.Services.Http.Handlers;

public sealed class OverlayHandler : IHttpHandler
{
    private static readonly string Content = ResourceLoader.LoadResourceText("PoproshaykaBot.WinForms.Assets.ObsOverlay.html");

    public Task HandleAsync(HttpListenerContext context)
    {
        var buffer = Encoding.UTF8.GetBytes(Content);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.Close();
        return Task.CompletedTask;
    }
}
