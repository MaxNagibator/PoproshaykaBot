using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class PeakStreamCommand(StreamSessionHistoryStore historyStore) : IChatCommand
{
    public string Canonical => "рекордстрима";
    public IReadOnlyCollection<string> Aliases => ["peak", "recordstream"];
    public string Description => "стрим с пиковым числом зрителей";

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

        var record = sessions
            .OrderByDescending(s => s.PeakViewers)
            .ThenByDescending(s => s.StartedAt)
            .First();

        if (record.PeakViewers <= 0)
        {
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply("Данных о пике зрителей пока нет", context.MessageId));
        }

        var title = string.IsNullOrWhiteSpace(record.Title) ? "Без названия" : record.Title;
        var game = string.IsNullOrWhiteSpace(record.Game) ? "Без категории" : record.Game;
        var when = FormattingUtils.FormatDateTime(record.StartedAt.UtcDateTime);

        var text = $"🏆 Рекорд: 👥 {record.PeakViewers} – {title} | {game} | {when}";
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(text, context.MessageId));
    }
}
