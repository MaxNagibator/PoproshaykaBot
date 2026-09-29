using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class WasThereCommand(StreamSessionHistoryStore historyStore) : IChatCommand
{
    public string Canonical => "ятамбыл";
    public IReadOnlyCollection<string> Aliases => ["iwasthere", "attended"];
    public string Description => "на скольких стримах ты был";

    public bool CanExecute(CommandContext context)
    {
        return true;
    }

    public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var sessions = historyStore.LoadVisible().Sessions;

        if (sessions.Count == 0)
        {
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply("Истории стримов пока нет", context.MessageId));
        }

        var attended = sessions
            .Where(s => s.Chatters.Exists(c => string.Equals(c.UserId, context.UserId, StringComparison.Ordinal)))
            .OrderByDescending(s => s.StartedAt)
            .ToList();

        if (attended.Count == 0)
        {
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply("Ты ещё ни на одном завершённом стриме не был", context.MessageId));
        }

        var lastDate = FormattingUtils.FormatDateTime(attended[0].StartedAt.UtcDateTime);
        var text = $"🎟 Ты был на {attended.Count} стримах из {sessions.Count} | Последний: {lastDate}";
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(text, context.MessageId));
    }
}
