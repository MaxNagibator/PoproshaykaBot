using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Infrastructure.Events.Chat;

public sealed record ChatMessageReceived(
    string Channel,
    string MessageId,
    string UserId,
    string Username,
    string DisplayName,
    string Text,
    UserStatus Status,
    bool IsFirstTime,
    ChatMessageData HistoryEntry,
    bool IsBot = false,
    CommandResponseMark? CommandResponse = null) : EventBase;
