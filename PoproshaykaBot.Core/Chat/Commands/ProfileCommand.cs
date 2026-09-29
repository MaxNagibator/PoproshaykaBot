using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class ProfileCommand(BroadcastProfilesManager manager, ILogger<ProfileCommand> logger) : IChatCommand
{
    public string Canonical => "пресет";
    public IReadOnlyCollection<string> Aliases => ["preset"];
    public string Description => "применить пресет – сохранённый профиль трансляции";

    public bool CanExecute(CommandContext context)
    {
        return context.IsBroadcaster || context.IsModerator;
    }

    public async Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count == 0)
        {
            return OutgoingMessage.Reply("Использование: !пресет <имя>", context.MessageId);
        }

        var name = string.Join(" ", context.Arguments);
        var profile = manager.FindByName(name);

        if (profile == null)
        {
            return OutgoingMessage.Reply($"Пресет «{name}» не найден", context.MessageId);
        }

        try
        {
            await manager.ApplyAsync(profile.Id, cancellationToken);
            return OutgoingMessage.Reply($"Пресет применён: {profile.Name}", context.MessageId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось применить профиль '{Name}' из чата", profile.Name);
            return OutgoingMessage.Reply($"Не удалось применить пресет: {profile.Name}", context.MessageId);
        }
    }
}
