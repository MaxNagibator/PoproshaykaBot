using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

public sealed class UserRankService(SettingsManager settingsManager)
{
    private UserRank[] Ranks => [.. settingsManager.Current.Ranks.Ranks.OrderByDescending(x => x.MinMessages)];

    public UserRank GetRank(long messageCount)
    {
        if (messageCount < 0)
        {
            return new("⛓️", "КАТОРЖНИК", 0);
        }

        var ranks = Ranks;
        foreach (var rank in ranks.Where(rank => messageCount >= (long)rank.MinMessages))
        {
            return rank;
        }

        return ranks.Length > 0 ? ranks[^1] : new("♟", "ПЕШКА", 0, 3);
    }

    public string GetRankDisplay(long messageCount)
    {
        var rank = GetRank(messageCount);
        return $"{rank.Emoji} {rank.DisplayName}";
    }
}
