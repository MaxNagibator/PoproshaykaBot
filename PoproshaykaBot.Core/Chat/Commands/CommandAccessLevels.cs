namespace PoproshaykaBot.Core.Chat.Commands;

public static class CommandAccessLevels
{
    public static CommandAccessLevel Clamp(CommandAccessLevel level)
    {
        if (level < CommandAccessLevel.Everyone)
        {
            return CommandAccessLevel.Everyone;
        }

        return level > CommandAccessLevel.Broadcaster ? CommandAccessLevel.Broadcaster : level;
    }

    public static CommandAccessLevel Stricter(CommandAccessLevel first, CommandAccessLevel second)
    {
        return first >= second ? first : second;
    }

    public static bool Allows(this CommandAccessLevel level, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Clamp(level) switch
        {
            CommandAccessLevel.Broadcaster => context.IsBroadcaster,
            CommandAccessLevel.Moderators => context.IsBroadcaster || context.IsModerator,
            _ => true,
        };
    }
}
