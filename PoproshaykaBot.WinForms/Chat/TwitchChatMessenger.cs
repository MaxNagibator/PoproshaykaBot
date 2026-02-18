using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using System.Text;
using TwitchLib.Client;

namespace PoproshaykaBot.WinForms.Chat;

public sealed class TwitchChatMessenger(TwitchClient client, SettingsManager settingsManager)
{
    private const int MaxMessageLength = 500;

    public event Action<ChatMessageData>? MessageSent;

    public void Send(string channel, string text)
    {
        foreach (var part in SplitByLimit(text, MaxMessageLength))
        {
            client.SendMessage(channel, part);
            RaiseMessageSent(part);
        }
    }

    public void Reply(string channel, string replyToMessageId, string text)
    {
        foreach (var part in SplitByLimit(text, MaxMessageLength))
        {
            client.SendReply(channel, replyToMessageId, part);
            RaiseMessageSent(part);
        }
    }

    private static IEnumerable<string> SplitByLimit(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            yield return text;

            yield break;
        }

        var parts = text.Split(" | ");
        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            var isFirstInChunk = builder.Length == 0;
            var candidate = isFirstInChunk ? part : " | " + part;

            if (builder.Length + candidate.Length <= maxLength)
            {
                builder.Append(candidate);
            }
            else
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();

                    builder.Clear();
                }

                if (part.Length > maxLength)
                {
                    for (var i = 0; i < part.Length; i += maxLength)
                    {
                        var length = Math.Min(maxLength, part.Length - i);
                        yield return part.Substring(i, length);
                    }
                }
                else
                {
                    builder.Append(part);
                }
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private void RaiseMessageSent(string text)
    {
        var settings = settingsManager.Current;

        MessageSent?.Invoke(new()
        {
            Timestamp = DateTime.UtcNow,
            DisplayName = settings.Twitch.BotUsername,
            Message = text,
            MessageType = ChatMessageType.BotResponse,
            Status = UserStatus.None,
        });
    }
}
