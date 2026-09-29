using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;

namespace PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

public static class DiagnosticsFormat
{
    public const string Unset = "–";

    private const decimal SizeUnit = 1024m;

    private static readonly string[] SizeSuffixes = ["байт", "КБ", "МБ", "ГБ", "ТБ"];

    public static string Size(long bytes)
    {
        if (bytes < 0)
        {
            return Unset;
        }

        if (bytes == 0)
        {
            return "0 " + SizeSuffixes[0];
        }

        var index = 0;
        var value = (decimal)bytes;

        while (value >= SizeUnit && index < SizeSuffixes.Length - 1)
        {
            value /= SizeUnit;
            index++;
        }

        return string.Create(UiCulture.Russian, $"{Math.Round(value, index == 0 ? 0 : 1)} {SizeSuffixes[index]}");
    }

    public static string Number(long value)
    {
        return value.ToString("N0", UiCulture.Russian);
    }

    public static double? Share(long value, long limit)
    {
        return limit > 0 ? (double)value / limit : null;
    }

    public static string Percent(double share)
    {
        if (double.IsNaN(share) || double.IsInfinity(share))
        {
            return Unset;
        }

        return string.Create(UiCulture.Russian, $"{Math.Round(share * 100)} %");
    }

    public static string Word(long count, string one, string few, string many)
    {
        var tail = Math.Abs(count) % 100;

        if (tail is >= 11 and <= 14)
        {
            return many;
        }

        return (tail % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }

    public static string Count(long count, string one, string few, string many)
    {
        return $"{Number(count)} {Word(count, one, few, many)}";
    }

    public static string Ago(DateTimeOffset? moment, DateTimeOffset now)
    {
        return moment is { } value ? RelativeTime.DescribeMoment(value, now) : Unset;
    }

    public static string Moment(DateTimeOffset? moment)
    {
        return moment is { } value ? RelativeTime.FormatMoment(value) : Unset;
    }

    public static string Until(DateTimeOffset? moment, DateTimeOffset now)
    {
        if (moment is not { } value)
        {
            return "не запланировано";
        }

        var left = value - now;

        return left <= TimeSpan.Zero ? "вот-вот" : $"через {Duration(left)}";
    }

    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalMinutes < 1)
        {
            return string.Create(UiCulture.Russian, $"{(int)value.TotalSeconds} с");
        }

        if (value.TotalHours < 1)
        {
            return string.Create(UiCulture.Russian, $"{(int)value.TotalMinutes} мин.");
        }

        return string.Create(UiCulture.Russian, $"{(int)value.TotalHours} ч {value.Minutes:00} мин.");
    }

    public static string Milliseconds(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalMilliseconds < 1000
            ? string.Create(UiCulture.Russian, $"{Math.Round(value.TotalMilliseconds)} мс")
            : string.Create(UiCulture.Russian, $"{Math.Round(value.TotalSeconds, 1)} с");
    }

    public static string Describe(ConnectionState state)
    {
        return state switch
        {
            ConnectionState.Connecting => "подключается",
            ConnectionState.Connected => "подключено",
            ConnectionState.Reconnecting => "переподключается",
            ConnectionState.Disconnected => "отключено",
            ConnectionState.Failed => "отказ",
            _ => "нет данных",
        };
    }

    public static DiagnosticsCardState ToCardState(ConnectionState state)
    {
        return state switch
        {
            ConnectionState.Connected => DiagnosticsCardState.Ok,
            ConnectionState.Connecting or ConnectionState.Reconnecting => DiagnosticsCardState.Warning,
            ConnectionState.Failed => DiagnosticsCardState.Error,
            _ => DiagnosticsCardState.Unknown,
        };
    }

    public static DiagnosticsCardState Worst(DiagnosticsCardState first, DiagnosticsCardState second)
    {
        if (first == DiagnosticsCardState.Error || second == DiagnosticsCardState.Error)
        {
            return DiagnosticsCardState.Error;
        }

        if (first == DiagnosticsCardState.Warning || second == DiagnosticsCardState.Warning)
        {
            return DiagnosticsCardState.Warning;
        }

        if (first == DiagnosticsCardState.Unknown || second == DiagnosticsCardState.Unknown)
        {
            return DiagnosticsCardState.Unknown;
        }

        return DiagnosticsCardState.Ok;
    }
}
