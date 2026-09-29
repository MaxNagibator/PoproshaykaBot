namespace PoproshaykaBot.Wpf.Infrastructure;

public static class RelativeTime
{
    public const int MaxRelativeDays = 30;

    public static string Describe(DateTimeOffset value, DateTimeOffset now)
    {
        var days = (now.ToLocalTime().Date - value.ToLocalTime().Date).Days;

        return days switch
        {
            <= 0 => "сегодня",
            1 => "вчера",
            <= MaxRelativeDays => string.Create(UiCulture.Russian, $"{days} дн. назад"),
            _ => FormatDate(value),
        };
    }

    public static string Describe(DateTime value, DateTimeOffset now)
    {
        return Describe(ToOffset(value), now);
    }

    public static string DescribeMoment(DateTimeOffset value, DateTimeOffset now)
    {
        var elapsed = now - value;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "только что";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return string.Create(UiCulture.Russian, $"{(int)elapsed.TotalMinutes} мин. назад");
        }

        if (elapsed < TimeSpan.FromHours(24))
        {
            return string.Create(UiCulture.Russian, $"{(int)elapsed.TotalHours} ч. назад");
        }

        return Describe(value, now);
    }

    public static string FormatMoment(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", UiCulture.Russian);
    }

    public static string FormatDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd.MM.yyyy", UiCulture.Russian);
    }

    public static string FormatDate(DateTime value)
    {
        return FormatDate(ToOffset(value));
    }

    private static DateTimeOffset ToOffset(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => new(value),
            _ => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
        };
    }
}
