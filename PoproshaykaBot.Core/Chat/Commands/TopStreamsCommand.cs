using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class TopStreamsCommand(StreamSessionHistoryStore historyStore) : IChatCommand
{
    private static readonly HashSet<string> MessagesKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "сообщения", "сообщ", "сообщ.", "msg", "messages", "m",
    };

    private static readonly HashSet<string> ViewersKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "зрители", "зр", "viewers", "v", "peak", "пик",
    };

    public string Canonical => "топстримов";
    public IReadOnlyCollection<string> Aliases => ["topstreams"];
    public string Description => "топ стримов по сообщениям (аргумент 'зрители' переключает на пик зрителей, число задаёт длину 1–5)";

    public bool CanExecute(CommandContext context)
    {
        return true;
    }

    public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var count = 3;
        var byViewers = false;

        foreach (var arg in context.Arguments)
        {
            if (int.TryParse(arg, out var requestedCount))
            {
                count = Math.Clamp(requestedCount, 1, 5);
            }
            else if (ViewersKeywords.Contains(arg))
            {
                byViewers = true;
            }
            else if (MessagesKeywords.Contains(arg))
            {
                byViewers = false;
            }
        }

        var sessions = historyStore.LoadVisible().Sessions;

        if (sessions.Count == 0)
        {
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Normal("Истории стримов пока нет"));
        }

        var ordered = byViewers
            ? sessions.OrderByDescending(s => s.PeakViewers).ThenByDescending(s => s.StartedAt)
            : sessions.OrderByDescending(s => s.MessageCount).ThenByDescending(s => s.StartedAt);

        var top = ordered.Take(count).ToList();

        var parts = top.Select((s, i) =>
        {
            var title = string.IsNullOrWhiteSpace(s.Title) ? "Без названия" : Truncate(s.Title, 30);
            var value = byViewers
                ? $"👥 {s.PeakViewers}"
                : $"💬 {FormattingUtils.FormatNumber(s.MessageCount)}";

            return $"{i + 1}. {title} ({value})";
        });

        var header = byViewers ? $"👥 Топ-{top.Count} по пику: " : $"💬 Топ-{top.Count} по сообщениям: ";
        var text = header + string.Join(", ", parts);
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Normal(text));
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}
