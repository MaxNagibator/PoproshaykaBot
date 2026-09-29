using PoproshaykaBot.Core.Chat.Commands;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed class CommandResponseTargetOption
{
    private CommandResponseTargetOption(CommandResponseTarget? target, string title, string hint)
    {
        Target = target;
        Title = title;
        Hint = hint;
    }

    public static CommandResponseTargetOption Inherit { get; } = new(null, "Как в настройках", "Команда берёт общую цель ответа со страницы");

    public static CommandResponseTargetOption Chat { get; } = new(CommandResponseTarget.Chat, "Только в чат", "Ответ уходит в чат Twitch, в оверлее его нет");

    public static CommandResponseTargetOption Caller { get; } = new(CommandSettings.CallerOnly, "Ответом на сообщение", "Ответ уходит в общий чат Twitch ответом на сообщение вызвавшего – его видят все зрители");

    public static CommandResponseTargetOption Whisper { get; } = new(CommandSettings.WhisperToCaller, "Лично (шёпотом)", "Ответ приходит вызвавшему личным сообщением Twitch, в общем чате его нет. У аккаунта бота должен быть подтверждён телефон. Если Twitch откажет, ответ уйдёт ответом на сообщение в чате");

    public static CommandResponseTargetOption Overlay { get; } = new(CommandResponseTarget.Overlay, "Только в оверлей", "Ответ виден в оверлее и в чате приложения, в чат Twitch не уходит");

    public static CommandResponseTargetOption Both { get; } = new(CommandSettings.ChatAndOverlay, "В чат и оверлей", "Ответ уходит и в чат Twitch, и в оверлей");

    public static CommandResponseTargetOption Silent { get; } = new(CommandResponseTarget.None, "Молча", "Команда выполняется, но ответа не показывает нигде");

    public static IReadOnlyList<CommandResponseTargetOption> ForCommand { get; } = [Inherit, Chat, Caller, Whisper, Overlay, Both, Silent];

    public static IReadOnlyList<CommandResponseTargetOption> ForDefault { get; } = [Chat, Caller, Whisper, Overlay, Both, Silent];

    public CommandResponseTarget? Target { get; }

    public string Title { get; }

    public string Hint { get; }

    public static CommandResponseTargetOption ForCommandTarget(CommandResponseTarget? target)
    {
        if (target is not { } value)
        {
            return Inherit;
        }

        return Resolve(value);
    }

    public static CommandResponseTargetOption ForDefaultTarget(CommandResponseTarget target)
    {
        return Resolve(target);
    }

    public static string Describe(CommandResponseTarget target)
    {
        return Resolve(target).Title;
    }

    private static CommandResponseTargetOption Resolve(CommandResponseTarget target)
    {
        var known = target & CommandSettings.KnownTargets;

        if (known.WhispersToCaller())
        {
            return Whisper;
        }

        if (known.RepliesToCaller())
        {
            return Caller;
        }

        return known switch
        {
            CommandResponseTarget.Chat => Chat,
            CommandResponseTarget.Overlay => Overlay,
            CommandSettings.ChatAndOverlay => Both,
            _ => Silent,
        };
    }
}
