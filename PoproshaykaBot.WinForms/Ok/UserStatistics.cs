using System.Text.Json.Serialization;

namespace PoproshaykaBot.WinForms.Models;

public class UserStatistics
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ulong MessageCount { get; set; }
    public ulong ShtrafMessageCount { get; set; }
    public ulong BonusMessageCount { get; set; }

    [JsonIgnore]
    public long TotalMessageCount => (long)MessageCount + (long)BonusMessageCount - (long)ShtrafMessageCount;

    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public static UserStatistics Create(string userId, string name)
    {
        var now = DateTime.UtcNow;

        return new()
        {
            UserId = userId,
            Name = name,
            MessageCount = 0,
            FirstSeen = now,
            LastSeen = now,
        };
    }

    public void IncrementMessageCount()
    {
        MessageCount++;
        LastSeen = DateTime.UtcNow;
    }

    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || Name == newName)
        {
            return;
        }

        Name = newName;
    }

    public UserStatistics Clone()
    {
        return new()
        {
            UserId = UserId,
            Name = Name,
            MessageCount = MessageCount,
            ShtrafMessageCount = ShtrafMessageCount,
            BonusMessageCount = BonusMessageCount,
            FirstSeen = FirstSeen,
            LastSeen = LastSeen,
        };
    }
}
