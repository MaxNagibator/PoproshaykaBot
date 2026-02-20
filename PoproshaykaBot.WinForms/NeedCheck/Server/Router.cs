using System.Net;

namespace PoproshaykaBot.WinForms.Services.Http;

public sealed class Router
{
    private readonly Dictionary<string, IHttpHandler> _routes = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string path, IHttpHandler handler)
    {
        _routes[path] = handler;
    }

    public Task RouteAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (_routes.TryGetValue(path, out var handler))
        {
            return handler.HandleAsync(context);
        }

        context.Response.StatusCode = 404;
        context.Response.Close();

        return Task.CompletedTask;
    }
}
