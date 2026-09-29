using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class MyProfileCommand(IUserStatisticsRepository statistics, UserRankService rankService) : IChatCommand
{
    public string Canonical => "мойпрофиль";
    public IReadOnlyCollection<string> Aliases => ["profile", "профиль"];
    public string Description => "твоя статистика";

    public bool CanExecute(CommandContext context)
    {
        return true;
    }

    public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var targetUserId = context.UserId;
        var targetDisplayName = "твоя";

        if (context.Arguments.Count > 0)
        {
            var username = context.Arguments[0];
            var otherUserStats = statistics.GetByName(username);
            if (otherUserStats != null)
            {
                targetUserId = otherUserStats.UserId;
                targetDisplayName = $"статистика {otherUserStats.Name}";
            }
            else
            {
                return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply($"Пользователь {username} не найден", context.MessageId));
            }
        }

        var userStats = statistics.GetById(targetUserId);

        if (userStats == null)
        {
            var msg = targetUserId == context.UserId ? "У тебя пока нет статистики" : "У этого пользователя пока нет статистики";
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(msg, context.MessageId));
        }

        var messages = FormattingUtils.FormatNumber((long)userStats.MessageCount);
        var points = rankService.FormatPoints(userStats.Points);
        var firstSeen = FormattingUtils.FormatDateTime(userStats.FirstSeen);
        var lastSeen = FormattingUtils.FormatDateTime(userStats.LastSeen);
        var rankDisplay = rankService.GetRankDisplay(userStats.Points);

        var text = $"👤 {targetDisplayName} {rankDisplay} | 💬 {messages} мсг | 🏆 {points} | С нами с: {firstSeen} | В чате: {lastSeen}";
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(text, context.MessageId));
    }
}
