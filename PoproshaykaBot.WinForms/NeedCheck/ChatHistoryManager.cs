using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

// TODO: Шляпа. Переделать на события
public class ChatHistoryManager(SettingsManager settingsManager)
{
    private readonly object _sync = new();
    private readonly LinkedList<ChatMessageData> _chatHistory = [];
    private readonly List<IChatDisplay> _chatDisplays = [];
    private readonly object _displaysSync = new();

    public void AddMessage(ChatMessageData chatMessage)
    {
        lock (_sync)
        {
            _chatHistory.AddLast(chatMessage);

            var max = Math.Max(1, settingsManager.Current.Twitch.Infrastructure.ChatHistoryMaxItems);

            if (_chatHistory.Count > max)
            {
                _chatHistory.RemoveFirst();
            }
        }

        List<IChatDisplay> displays;
        lock (_displaysSync)
        {
            displays = _chatDisplays.ToList();
        }

        foreach (var display in displays)
        {
            try
            {
                display.AddChatMessage(chatMessage);
            }
            catch
            {
                UnregisterChatDisplay(display);
            }
        }
    }

    public void RegisterChatDisplay(IChatDisplay chatDisplay)
    {
        lock (_displaysSync)
        {
            if (_chatDisplays.Contains(chatDisplay) == false)
            {
                _chatDisplays.Add(chatDisplay);
            }
        }
    }

    public void UnregisterChatDisplay(IChatDisplay chatDisplay)
    {
        lock (_displaysSync)
        {
            _chatDisplays.Remove(chatDisplay);
        }
    }

    public IEnumerable<ChatMessageData> GetHistory()
    {
        lock (_sync)
        {
            return _chatHistory.ToList();
        }
    }

    public void ClearHistory()
    {
        lock (_sync)
        {
            _chatHistory.Clear();
        }

        List<IChatDisplay> displays;
        lock (_displaysSync)
        {
            displays = _chatDisplays.ToList();
        }

        foreach (var display in displays)
        {
            try
            {
                display.ClearChat();
            }
            catch
            {
                UnregisterChatDisplay(display);
            }
        }
    }
}
