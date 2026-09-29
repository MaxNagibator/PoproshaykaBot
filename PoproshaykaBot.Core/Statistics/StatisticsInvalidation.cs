namespace PoproshaykaBot.Core.Statistics;

public readonly record struct StatisticsInvalidation(bool Users, bool Bot, bool StreamHistory, bool PollHistory);
