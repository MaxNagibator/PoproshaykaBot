using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public sealed class TileHeaderLine : Panel
{
    public const double TitleFloorShare = 0.5;

    private double _titleNatural;
    private double _statusNatural;

    public static (double Title, double Status) Split(double available, double title, double status)
    {
        available = Math.Max(0, available);

        if (title + status <= available)
        {
            return (title, available - title);
        }

        var titleWidth = Math.Floor(Math.Min(title, Math.Max(available - status - 1, available * TitleFloorShare)));

        return (titleWidth, available - titleWidth);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = new Size(double.PositiveInfinity, availableSize.Height);

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            InternalChildren[index].Measure(natural);
        }

        _titleNatural = InternalChildren.Count > 0 ? InternalChildren[0].DesiredSize.Width : 0;
        _statusNatural = InternalChildren.Count > 1 ? InternalChildren[1].DesiredSize.Width : 0;

        var slots = Slots(availableSize.Width);
        var width = 0d;
        var height = 0d;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];

            child.Measure(new(SlotAt(slots, index), availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var slots = Slots(finalSize.Width);
        var x = 0d;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var slot = SlotAt(slots, index);

            InternalChildren[index].Arrange(new(x, 0, slot, finalSize.Height));
            x += slot;
        }

        return finalSize;
    }

    private (double Title, double Status) Slots(double available)
    {
        return double.IsPositiveInfinity(available)
            ? (_titleNatural, _statusNatural)
            : Split(available, _titleNatural, _statusNatural);
    }

    private static double SlotAt((double Title, double Status) slots, int index)
    {
        return index switch
        {
            0 => slots.Title,
            1 => slots.Status,
            _ => 0,
        };
    }
}
