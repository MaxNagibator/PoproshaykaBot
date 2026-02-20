using System.Net;

namespace PoproshaykaBot.WinForms.Services.Http.Handlers;

public sealed class StaticContentHandler(string resourceName, string contentType) : IHttpHandler
{
    public Task HandleAsync(HttpListenerContext context)
    {
        var bytes = ResourceLoader.LoadResourceBytes(resourceName);

        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
        return Task.CompletedTask;
    }
}
