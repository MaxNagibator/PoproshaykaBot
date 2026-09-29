using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Wpf.ViewModels;

public static class ChatMessageTerm
{
    public static PointTerm Instance { get; } = new()
    {
        Singular = "сообщение",
        Few = "сообщения",
        Many = "сообщений",
    };
}
