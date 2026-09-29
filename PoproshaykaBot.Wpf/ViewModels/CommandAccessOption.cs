using PoproshaykaBot.Core.Chat.Commands;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed class CommandAccessOption
{
    private CommandAccessOption(CommandAccessLevel? level, string title, string hint)
    {
        Level = level;
        Title = title;
        Hint = hint;
    }

    public static CommandAccessOption Inherit { get; } = new(null,
        "Как в коде",
        "Права остаются такими, какими их задаёт сама команда");

    public static CommandAccessOption Moderators { get; } = new(CommandAccessLevel.Moderators,
        "Модераторы и стример",
        "Зрителю бот на команду не отвечает вовсе");

    public static CommandAccessOption Broadcaster { get; } = new(CommandAccessLevel.Broadcaster,
        "Только стример",
        "Команду вызывает только владелец канала – модераторам бот не отвечает");

    public static IReadOnlyList<CommandAccessOption> All { get; } = [Inherit, Moderators, Broadcaster];

    public CommandAccessLevel? Level { get; }

    public string Title { get; }

    public string Hint { get; }

    public static CommandAccessOption For(CommandAccessLevel? level)
    {
        return level switch
        {
            CommandAccessLevel.Broadcaster => Broadcaster,
            CommandAccessLevel.Moderators => Moderators,
            _ => Inherit,
        };
    }

    public static string Describe(CommandAccessLevel level)
    {
        return level switch
        {
            CommandAccessLevel.Broadcaster => "Стример",
            CommandAccessLevel.Moderators => "Модераторы",
            _ => "Все",
        };
    }

    public static string DescribeSource(CommandAccessLevel? level)
    {
        return level is { } value ? Describe(value) : "Никто";
    }
}
