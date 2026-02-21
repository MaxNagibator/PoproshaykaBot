namespace PoproshaykaBot.WinForms.Models;

public class BotStatistics
{
    public ulong TotalMessagesProcessed { get; set; }
    public DateTime BotStartTime { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalUptime { get; set; } = TimeSpan.Zero;

    public static BotStatistics Create()
    {
        var now = DateTime.UtcNow;

        return new()
        {
            TotalMessagesProcessed = 0,
            BotStartTime = now,
            LastUpdated = now,
            TotalUptime = TimeSpan.Zero,
        };
    }

    public void UpdateTimestamp()
    {
        LastUpdated = DateTime.UtcNow;
    }

    public void IncrementMessagesProcessed()
    {
        TotalMessagesProcessed++;
        UpdateTimestamp();
    }

    public void UpdateUptime()
    {
        TotalUptime = DateTime.UtcNow - BotStartTime;
        UpdateTimestamp();
    }

    public void ResetStartTime()
    {
        BotStartTime = DateTime.UtcNow;
        TotalUptime = TimeSpan.Zero;
        UpdateTimestamp();
    }

    public BotStatistics Clone()
    {
        return new()
        {
            TotalMessagesProcessed = TotalMessagesProcessed,
            BotStartTime = BotStartTime,
            LastUpdated = LastUpdated,
            TotalUptime = TotalUptime,
        };
    }
}
