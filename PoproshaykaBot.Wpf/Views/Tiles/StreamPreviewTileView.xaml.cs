using KeepShell.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class StreamPreviewTileView : UserControl, IView<StreamPreviewTileViewModel>
{
    private const double ContentSizedWidth = 320;
    private const double FrameAspect = 16.0 / 9.0;
    private const double Tolerance = 0.5;
    private const double SpareToRegrow = 4;

    private double _slotWidth;
    private double _heightCap = double.PositiveInfinity;
    private double _capScale = FontScaleManager.Current;

    public StreamPreviewTileView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => FitDecodeWidth();
        DataContextChanged += (_, _) => OnDataContextChanged();
        IsVisibleChanged += (_, _) => ReportOnScreen();
        Unloaded += (_, _) => _heightCap = double.PositiveInfinity;
        LayoutUpdated += (_, _) => FitFrameHeight();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var bounded = double.IsFinite(constraint.Width) && constraint.Width > 0;

        // TODO: у Auto-трека нет сигнала «потолок вырос», поэтому поднятая пользователем «Макс. высота»
        //  доезжает до кадра только со следующей перестройкой сетки; завести сброс от раскладки, если
        //  на это пожалуются
        if (FontScaleManager.Current != _capScale)
        {
            _capScale = FontScaleManager.Current;
            _heightCap = double.PositiveInfinity;
        }

        _slotWidth = bounded ? constraint.Width : 0;
        FrameImage.MaxWidth = bounded ? double.PositiveInfinity : ContentSizedWidth;
        FrameImage.MaxHeight = _heightCap;

        return base.MeasureOverride(constraint);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        FitDecodeWidth();
    }

    private void OnDataContextChanged()
    {
        _heightCap = double.PositiveInfinity;
        InvalidateMeasure();
        FitDecodeWidth();
        ReportOnScreen();
    }

    private void ReportOnScreen()
    {
        if (DataContext is StreamPreviewTileViewModel tile)
        {
            tile.SetOnScreen(IsVisible);
        }
    }

    private void FitFrameHeight()
    {
        if (FrameImage.Visibility != Visibility.Visible || FrameImage.ActualHeight <= 0)
        {
            return;
        }

        var (overflow, spare) = MeasureTileSlack();

        if (overflow > Tolerance)
        {
            ApplyHeightCap(Math.Max(0, FrameImage.ActualHeight - overflow));
        }
        else if (spare > SpareToRegrow)
        {
            ApplyHeightCap(double.PositiveInfinity);
        }
    }

    private void ApplyHeightCap(double cap)
    {
        var unchanged = double.IsPositiveInfinity(cap)
            ? double.IsPositiveInfinity(_heightCap)
            : Math.Abs(cap - _heightCap) <= Tolerance;

        if (unchanged)
        {
            return;
        }

        _heightCap = cap;
        InvalidateMeasure();
    }

    private (double Overflow, double Spare) MeasureTileSlack()
    {
        var overflow = 0.0;
        var spare = 0.0;

        for (DependencyObject? node = FrameImage; node is FrameworkElement element; node = VisualTreeHelper.GetParent(node))
        {
            var slot = LayoutInformation.GetLayoutSlot(element);
            var used = element.ActualHeight + element.Margin.Top + element.Margin.Bottom;

            overflow = Math.Max(overflow, used - slot.Height);

            if (!ReferenceEquals(element.DataContext, DataContext))
            {
                break;
            }

            spare = Math.Max(spare, slot.Height - used);

            if (element is ScrollViewer scroller)
            {
                overflow = Math.Max(overflow, scroller.ExtentHeight - scroller.ViewportHeight);
            }
        }

        return (overflow, spare);
    }

    private void FitDecodeWidth()
    {
        if (_slotWidth > 0 && DataContext is StreamPreviewTileViewModel tile)
        {
            var shownWidth = Math.Min(_slotWidth, _heightCap * FrameAspect);

            tile.FitTo(shownWidth * VisualTreeHelper.GetDpi(this).DpiScaleX);
        }
    }
}
