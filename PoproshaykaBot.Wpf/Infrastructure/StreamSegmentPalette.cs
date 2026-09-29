namespace PoproshaykaBot.Wpf.Infrastructure;

public static class StreamSegmentPalette
{
    public const int SlotCount = 6;
    public const int NeutralSlot = 5;

    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    public static int IndexOf(string? game)
    {
        if (game is not { Length: > 0 })
        {
            return NeutralSlot;
        }

        var hash = FnvOffsetBasis;

        foreach (var symbol in game)
        {
            hash = (hash ^ char.ToLowerInvariant(symbol)) * FnvPrime;
        }

        return (int)(hash % (SlotCount - 1));
    }
}
