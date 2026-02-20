using PoproshaykaBot.WinForms.Settings;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Services.Http.Handlers;

public sealed class ApiHistoryHandler(ChatHistoryManager historyManager, SettingsManager settingsManager) : IHttpHandler
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
            var obsSettings = settingsManager.Current.Twitch.ObsChat;
            var maxMessages = obsSettings.MaxMessages;
            var history = historyManager.GetHistory();

            if (obsSettings.EnableMessageFadeOut)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-obsSettings.MessageLifetimeSeconds);
                history = history.Where(x => x.Timestamp >= cutoff).ToList();
            }

            var finalHistory = history
                .TakeLast(maxMessages)
                .Select(DtoMapper.ToServerMessage);

            var json = JsonSerializer.Serialize(finalHistory, JsonSerializerOptions);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch
        {
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }

        return Task.CompletedTask;
    }
}
