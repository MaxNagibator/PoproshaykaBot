using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Server.Images;
using PoproshaykaBot.Core.Settings.Obs;

namespace PoproshaykaBot.Core.Server;

public static class DtoMapper
{
    public static object ToServerMessage(ChatMessageData chatMessage, ObsChatSettings obsChatSettings)
    {
        var images = MessageImagePolicy.BuildImages(chatMessage, obsChatSettings);

        return new
        {
            messageId = chatMessage.MessageId,
            userId = chatMessage.UserId,
            username = chatMessage.DisplayName,
            displayName = chatMessage.DisplayName,
            message = chatMessage.Message,
            timestamp = chatMessage.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            messageType = chatMessage.MessageType.ToString(),
            isFirstTime = chatMessage.IsFirstTime,
            status = chatMessage.Status,
            color = chatMessage.Color,
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
            images = images.Select(i => i.Url).ToArray(),
            imageLinks = images.Select(i => new
                {
                    startIndex = i.StartIndex,
                    endIndex = i.EndIndex,
                })
                .ToArray(),
        };
    }
}
