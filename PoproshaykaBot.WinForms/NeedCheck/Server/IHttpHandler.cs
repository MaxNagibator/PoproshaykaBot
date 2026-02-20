using System.Net;

namespace PoproshaykaBot.WinForms.Services.Http;

public interface IHttpHandler
{
    Task HandleAsync(HttpListenerContext context);
}
