using PoproshaykaBot.WinForms.Models;

namespace PoproshaykaBot.WinForms;

public interface IChatDisplay
{
    void AddChatMessage(ChatMessageData chatMessage);

    void ClearChat();
}
