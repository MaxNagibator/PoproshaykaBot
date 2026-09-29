using System;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public static class ObsStreamHealth
{
    public const double NoticeableDropShare = 0.01D;
    public const double SevereDropShare = 0.05D;
    public const double CongestedShare = 0.05D;

    private const double SmallestShownPercent = 0.1D;

    public static (string Text, string? Severity)? Describe(
        bool? active,
        double? congestion,
        long? skippedFrames,
        long? totalFrames)
    {
        if (active != true)
        {
            return null;
        }

        var framesKnown = skippedFrames is >= 0 && totalFrames is > 0;
        var dropShare = framesKnown ? (double)skippedFrames!.Value / totalFrames!.Value : 0D;
        var dropSeverity = DropSeverity(dropShare);

        if (dropSeverity is null && congestion is > CongestedShare)
        {
            return (string.Create(UiCulture.Russian, $"Сеть перегружена: {congestion.Value * 100D:0} %"), "Warning");
        }

        if (!framesKnown)
        {
            return null;
        }

        if (skippedFrames == 0)
        {
            return ("Кадры не теряются", null);
        }

        return (string.Create(UiCulture.Russian, $"Пропущено кадров: {skippedFrames!.Value:N0} ({FormatPercent(dropShare)})"),
            dropSeverity);
    }

    public static string? DropSeverity(double share)
    {
        return share switch
        {
            > SevereDropShare => "Error",
            >= NoticeableDropShare => "Warning",
            _ => null,
        };
    }

    private static string FormatPercent(double share)
    {
        var percent = share * 100D;

        return percent < SmallestShownPercent
            ? string.Create(UiCulture.Russian, $"меньше {SmallestShownPercent:0.#} %")
            : string.Create(UiCulture.Russian, $"{percent:0.#} %");
    }
}
