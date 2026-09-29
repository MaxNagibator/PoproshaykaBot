namespace PoproshaykaBot.Core.Statistics;

public readonly record struct StatisticsExternalWrite(bool Statistics, bool StreamHistory, bool PollHistory)
{
    public static StatisticsExternalWrite None => new(false, false, false);
}
