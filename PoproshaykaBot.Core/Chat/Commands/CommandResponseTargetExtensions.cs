namespace PoproshaykaBot.Core.Chat.Commands;

public static class CommandResponseTargetExtensions
{
    public static bool GoesToChat(this CommandResponseTarget target)
    {
        return !target.WhispersToCaller()
               && (target & (CommandResponseTarget.Chat | CommandResponseTarget.Caller)) != CommandResponseTarget.None;
    }

    public static bool RepliesToCaller(this CommandResponseTarget target)
    {
        return target.HasFlag(CommandResponseTarget.Caller);
    }

    public static bool WhispersToCaller(this CommandResponseTarget target)
    {
        return target.HasFlag(CommandResponseTarget.Whisper);
    }

    public static bool GoesToOverlay(this CommandResponseTarget target)
    {
        return target.HasFlag(CommandResponseTarget.Overlay);
    }
}
