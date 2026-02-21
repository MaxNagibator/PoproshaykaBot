using PoproshaykaBot.WinForms.Models;

namespace PoproshaykaBot.WinForms.Services.Http;

public static class DtoMapper
{
    public static object ToServerMessage(ChatMessageData chatMessage)
    {
        return new
        {
            username = chatMessage.DisplayName,
            displayName = chatMessage.DisplayName,
            message = chatMessage.Message,
            timestamp = chatMessage.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            messageType = chatMessage.MessageType.ToString(),
            isFirstTime = chatMessage.IsFirstTime,
            status = chatMessage.Status,
            emotes = chatMessage.Emotes.Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    imageUrl = e.ImageUrl,
                    startIndex = e.StartIndex,
                    endIndex = e.EndIndex,
                })
                .ToArray(),
            badges = chatMessage.Badges.Select(b => new
                {
                    type = b.Key,
                    version = b.Value,
                    imageUrl = chatMessage.BadgeUrls.GetValueOrDefault($"{b.Key}/{b.Value}", ""),
                })
                .ToArray(),
        };
    }
}
