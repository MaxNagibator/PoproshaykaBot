using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Users;

public sealed class UserRankService(SettingsManager settingsManager)
{
    public PointTerm PointTerm => settingsManager.Current.Ranks.PointTerm;
    private UserRank[] Ranks => [.. settingsManager.Current.Ranks.Ranks.OrderByDescending(x => x.MinMessages)];

    public UserRank GetRank(long messageCount)
    {
        if (messageCount < 0)
        {
            return new("⛓️", "КАТОРЖНИК", 0);
        }

        var ranks = Ranks;
        var match = ranks.FirstOrDefault(rank => messageCount >= (long)rank.MinMessages);

        if (match is not null)
        {
            return match;
        }

        return ranks.Length > 0 ? UserRank.Unranked : new("♟", "ПЕШКА", 0, 3);
    }

    public string GetRankDisplay(long messageCount)
    {
        var rank = GetRank(messageCount);
        return $"{rank.Emoji} {rank.DisplayName}";
    }

    public string FormatPoints(long count)
    {
        return $"{FormattingUtils.FormatNumber(count)} {PointTerm.ForCount(count)}";
    }
}
