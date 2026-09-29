using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class StreamInfoCommand(IStreamStatus streamStatusManager) : IChatCommand
{
    public string Canonical => "стрим";
    public IReadOnlyCollection<string> Aliases => ["stream", "онлайн", "он"];
    public string Description => "информация о текущем стриме";

    public bool CanExecute(CommandContext context)
    {
        return true;
    }

    public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var info = streamStatusManager.CurrentStream;

        if (info == null || info.IsBare)
        {
            var text = streamStatusManager.CurrentStatus == StreamStatus.Online
                ? "Стрим онлайн, детали загружаются"
                : "Сейчас стрим офлайн";

            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(text, context.MessageId));
        }

        var duration = DateTime.UtcNow - info.StartedAt;
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;

        var title = string.IsNullOrWhiteSpace(info.Title) ? "Без названия" : info.Title;
        var game = string.IsNullOrWhiteSpace(info.GameName) ? "Без категории" : info.GameName;

        var textFull = $"🔴 Стрим: {title} | {game} | 👥 {info.ViewerCount} | ⏱ {hours:0}ч {minutes:00}м";
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(textFull, context.MessageId));
    }
}
