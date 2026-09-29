namespace PoproshaykaBot.Core.Chat.Commands;

[Flags]
public enum CommandResponseTarget
{
    None = 0,
    Chat = 1,
    Overlay = 2,
    Caller = 4,
    Whisper = 8,
}
