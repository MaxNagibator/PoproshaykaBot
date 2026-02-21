using PoproshaykaBot.WinForms.Models;

namespace PoproshaykaBot.WinForms;

// TODO:Удалить и заменить на Twitch-чат
public partial class ChatDisplay : UserControl, IChatDisplay
{
    private const int MaxChatLines = 200;

    public ChatDisplay()
    {
        InitializeComponent();
    }

    public void AddChatMessage(ChatMessageData chatMessage)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<ChatMessageData>(AddChatMessage), chatMessage);
            return;
        }

        if (_chatRichTextBox.Lines.Length > MaxChatLines)
        {
            int charIndex = _chatRichTextBox.GetFirstCharIndexFromLine(_chatRichTextBox.Lines.Length - MaxChatLines);
            if (charIndex > 0)
            {
                _chatRichTextBox.Select(0, charIndex);
                _chatRichTextBox.SelectedText = "";
            }
        }

        var shouldAutoScroll = _chatRichTextBox.SelectionStart >= _chatRichTextBox.Text.Length - 1;

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(chatMessage.Timestamp, TimeZoneInfo.Local);
        var timeString = localTime.ToString("HH:mm:ss");

        string messageText;
        Color messageColor;

        switch (chatMessage.MessageType)
        {
            case ChatMessageType.UserMessage:
                var timestampPart = $"[{timeString}] ";
                var usernamePart = $"{chatMessage.DisplayName}: ";
                var messagePart = chatMessage.Message;

                _chatRichTextBox.SelectionColor = Color.Gray;
                _chatRichTextBox.AppendText(timestampPart);

                var usernameColor = GetUsernameColor(chatMessage);
                _chatRichTextBox.SelectionColor = usernameColor;
                _chatRichTextBox.AppendText(usernamePart);

                _chatRichTextBox.SelectionColor = Color.Black;
                _chatRichTextBox.AppendText(messagePart + Environment.NewLine);

                _chatRichTextBox.Select(_chatRichTextBox.Text.Length, 0);
                _chatRichTextBox.SelectionColor = Color.Black;

                if (shouldAutoScroll)
                {
                    _chatRichTextBox.SelectionStart = _chatRichTextBox.Text.Length;
                    _chatRichTextBox.ScrollToCaret();
                }

                return;

            case ChatMessageType.BotResponse:
                messageText = $"[{timeString}] {chatMessage.DisplayName}: {chatMessage.Message}";
                messageColor = Color.DarkGreen;
                break;

            case ChatMessageType.SystemNotification:
                messageText = $"[{timeString}] *** {chatMessage.Message} ***";
                messageColor = Color.DarkOrange;
                break;

            default:
                messageText = $"[{timeString}] {chatMessage.DisplayName}: {chatMessage.Message}";
                messageColor = Color.Black;
                break;
        }

        _chatRichTextBox.SelectionColor = messageColor;
        _chatRichTextBox.AppendText(messageText + Environment.NewLine);

        _chatRichTextBox.Select(_chatRichTextBox.Text.Length, 0);
        _chatRichTextBox.SelectionColor = Color.Black;

        if (shouldAutoScroll)
        {
            _chatRichTextBox.SelectionStart = _chatRichTextBox.Text.Length;
            _chatRichTextBox.ScrollToCaret();
        }
    }

    public void ClearChat()
    {
        if (InvokeRequired)
        {
            Invoke(ClearChat);
            return;
        }

        _chatRichTextBox.Clear();
    }

    private void OnLoad(object sender, EventArgs e)
    {
        var welcomeMessage = new ChatMessageData
        {
            Timestamp = DateTime.UtcNow,
            DisplayName = "Система",
            Message = "Чат готов к отображению сообщений. Подключите бота для начала работы.",
            MessageType = ChatMessageType.SystemNotification,
            Status = UserStatus.None,
        };

        AddChatMessage(welcomeMessage);
    }

    private static Color GetUsernameColor(ChatMessageData chatMessage)
    {
        if (chatMessage.MessageType != ChatMessageType.UserMessage)
        {
            return Color.DarkBlue;
        }

        if (chatMessage.Status.HasFlag(UserStatus.Broadcaster))
        {
            return Color.FromArgb(147, 39, 143);
        }

        if (chatMessage.Status.HasFlag(UserStatus.Moderator))
        {
            return Color.FromArgb(0, 128, 0);
        }

        if (chatMessage.Status.HasFlag(UserStatus.Vip))
        {
            return Color.FromArgb(255, 140, 0);
        }

        if (chatMessage.Status.HasFlag(UserStatus.Subscriber))
        {
            return Color.FromArgb(30, 144, 255);
        }

        return Color.DarkBlue;
    }
}
