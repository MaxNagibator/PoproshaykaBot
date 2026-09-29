namespace PoproshaykaBot.Core.Statistics;

public readonly record struct StatisticsReadResult<T>(T? Value, bool Failed)
    where T : class
{
    public static StatisticsReadResult<T> NoFile => new(null, false);

    public static StatisticsReadResult<T> Failure => new(null, true);

    public static StatisticsReadResult<T> FromFile(T value)
    {
        return new(value, false);
    }
}
