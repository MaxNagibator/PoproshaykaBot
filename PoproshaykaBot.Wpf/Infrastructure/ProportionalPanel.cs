using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class ProportionalPanel : Panel
{
    public static readonly DependencyProperty ShareProperty = DependencyProperty.RegisterAttached(
        "Share",
        typeof(double),
        typeof(ProportionalPanel),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap),
        typeof(double),
        typeof(ProportionalPanel),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public static void SetShare(DependencyObject element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.SetValue(ShareProperty, value);
    }

    public static double GetShare(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return (double)element.GetValue(ShareProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var slots = ResolveSlots(availableSize.Width);
        var height = 0d;
        var width = 0d;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];

            child.Measure(new(slots[index], availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            width += slots[index];
        }

        return new(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var slots = ResolveSlots(finalSize.Width);
        var offset = 0d;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            InternalChildren[index].Arrange(new(offset, 0, slots[index], finalSize.Height));
            offset += slots[index] + Gap;
        }

        return finalSize;
    }

    private double[] ResolveSlots(double available)
    {
        var count = InternalChildren.Count;
        var slots = new double[count];

        if (count == 0)
        {
            return slots;
        }

        var usable = double.IsFinite(available) ? Math.Max(0, available - (Gap * (count - 1))) : 0;
        var total = 0d;

        for (var index = 0; index < count; index++)
        {
            var share = GetShare(InternalChildren[index]);

            slots[index] = double.IsFinite(share) && share > 0 ? share : 0;
            total += slots[index];
        }

        for (var index = 0; index < count; index++)
        {
            slots[index] = total > 0 ? usable * slots[index] / total : usable / count;
        }

        return slots;
    }
}
