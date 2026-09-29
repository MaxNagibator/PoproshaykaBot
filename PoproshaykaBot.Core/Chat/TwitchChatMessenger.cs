using PoproshaykaBot.Core.Twitch.Chat;

namespace PoproshaykaBot.Core.Chat;

public interface IChatMessenger
{
    void Send(string text);
    void Reply(string replyToMessageId, string text);
}

public sealed class TwitchChatMessenger(ChatSender chatSender) : IChatMessenger
{
    public void Send(string text)
    {
        Send(text, null);
    }

    public void Reply(string replyToMessageId, string text)
    {
        Reply(replyToMessageId, text, null);
    }

    public void Send(string text, CommandResponseMark? commandResponse)
    {
        _ = chatSender.EnqueueAsync(text, null, commandResponse, CancellationToken.None);
    }

    public void Reply(string replyToMessageId, string text, CommandResponseMark? commandResponse)
    {
        _ = chatSender.EnqueueAsync(text, replyToMessageId, commandResponse, CancellationToken.None);
    }

    public void Whisper(string toUserId, string fallbackReplyToMessageId, string text, CommandResponseMark? commandResponse)
    {
        _ = chatSender.EnqueueWhisperAsync(text, toUserId, fallbackReplyToMessageId, commandResponse, CancellationToken.None);
    }
}
