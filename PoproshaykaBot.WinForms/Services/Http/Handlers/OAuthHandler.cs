using System.Net;
using System.Text;

namespace PoproshaykaBot.WinForms.Services.Http.Handlers;

public sealed class OAuthHandler(TwitchOAuthService twitchOAuthService) : IHttpHandler
{
    public Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var code = request.QueryString["code"];
            var state = request.QueryString["state"];
            var error = request.QueryString["error"];

            if (!string.IsNullOrEmpty(code))
            {
                twitchOAuthService.SetAuthResult(code, state);

                var successHtml =
                    """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Авторизация успешна</title>
                        <style>
                            body { font-family: Arial, sans-serif; text-align: center; margin-top: 50px; }
                            .success { color: green; font-size: 24px; }
                        </style>
                    </head>
                    <body>
                        <div class='success'>✓ Авторизация успешна!</div>
                        <p>Вы можете закрыть это окно и вернуться к приложению.</p>
                    </body>
                    </html>
                    """;

                ServeHtml(response, successHtml);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                var ex = new InvalidOperationException($"OAuth ошибка: {error}");
                twitchOAuthService.SetAuthError(ex);

                var errorHtml =
                    $$"""
                      <!DOCTYPE html>
                      <html>
                      <head>
                          <title>Ошибка авторизации</title>
                          <style>
                              body { font-family: Arial, sans-serif; text-align: center; margin-top: 50px; }
                              .error { color: red; font-size: 24px; }
                          </style>
                      </head>
                      <body>
                          <div class='error'>✗ Ошибка авторизации</div>
                          <p>Ошибка: {{error}}</p>
                          <p>Попробуйте еще раз.</p>
                      </body>
                      </html>
                      """;

                ServeHtml(response, errorHtml);
            }
            else
            {
                response.StatusCode = 400;
                response.Close();
            }
        }
        catch (Exception ex)
        {
            twitchOAuthService.SetAuthError(ex);
            response.StatusCode = 500;
            response.Close();
        }

        return Task.CompletedTask;
    }

    private static void ServeHtml(HttpListenerResponse response, string html)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }
}
