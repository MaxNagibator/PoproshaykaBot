using PoproshaykaBot.WinForms.Settings;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Services.Http.Handlers;

public sealed class ApiChatSettingsHandler(SettingsManager settingsManager) : IHttpHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public Task HandleAsync(HttpListenerContext context)
    {
        var response = context.Response;

        try
        {
            var settings = settingsManager.Current.Twitch.ObsChat;
            var cssSettings = ObsChatCssSettings.FromObsChatSettings(settings);

            var json = JsonSerializer.Serialize(cssSettings, JsonSerializerOptions);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache");
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception)
        {
            response.StatusCode = 500;
            WriteErrorResponse(response, "Внутренняя ошибка сервера");
        }
        finally
        {
            response.Close();
        }

        return Task.CompletedTask;
    }

    private static void WriteErrorResponse(HttpListenerResponse response, string errorMessage)
    {
        try
        {
            var errorObj = new { error = errorMessage };
            var json = JsonSerializer.Serialize(errorObj);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch
        {
        }
    }
}
