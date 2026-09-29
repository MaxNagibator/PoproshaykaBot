namespace PoproshaykaBot.Core.Users;

public sealed record UserRank(string Emoji, string Name, ulong MinMessages, int Tier = 0)
{
    public static UserRank Unranked { get; } = new("🌱", "БЕЗ РАНГА", 0);

    public string DisplayName => Tier > 0 ? $"{Name} {GetRomanTier(Tier)}" : Name;

    private static string GetRomanTier(int tier)
    {
        return tier switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            _ => tier.ToString(),
        };
    }
}
