using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Server.Endpoints;

internal sealed class ChatHistoryEndpoint(
    ChatHistoryManager chatHistoryManager,
    ObsChatStore obsChatStore)
    : IEndpointMapper
{
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/history", () =>
        {
            var obsSettings = obsChatStore.Load();
            var maxMessages = obsSettings.MaxMessages;
            var history = chatHistoryManager.GetHistory();

            if (obsSettings.EnableMessageFadeOut)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-obsSettings.MessageLifetimeSeconds);
                history = history.Where(x => x.Timestamp >= cutoff).ToList();
            }

            var finalHistory = history
                .TakeLast(maxMessages)
                .Select(message => DtoMapper.ToServerMessage(message, obsSettings));

            return Results.Json(finalHistory, ServerJsonOptions.Default);
        });
    }
}
