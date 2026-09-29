using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class StreamsCountCommand(StreamSessionHistoryStore historyStore, TimeProvider timeProvider) : IChatCommand
{
    public string Canonical => "стримов";
    public IReadOnlyCollection<string> Aliases => ["streams", "streamscount"];
    public string Description => "всего стримов и суммарная длительность";

    public bool CanExecute(CommandContext context)
    {
        return true;
    }

    public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var sessions = historyStore.LoadVisible().Sessions;

        if (sessions.Count == 0)
        {
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Normal("Истории стримов пока нет"));
        }

        var monthAgo = timeProvider.GetUtcNow() - TimeSpan.FromDays(30);
        var totalDuration = TimeSpan.Zero;
        var monthCount = 0;

        foreach (var session in sessions)
        {
            totalDuration += session.Duration;

            if (session.StartedAt >= monthAgo)
            {
                monthCount++;
            }
        }

        var total = FormattingUtils.FormatNumber(sessions.Count);
        var totalDurationText = FormattingUtils.FormatTimeSpan(totalDuration);
        var month = FormattingUtils.FormatNumber(monthCount);

        var text = $"📺 Всего стримов: {total} | В эфире суммарно: {totalDurationText} | За 30 дней: {month}";
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Normal(text));
    }
}
