using KeepShell.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class BroadcastProfilesTileView : UserControl, IView<BroadcastProfilesTileViewModel>
{
    public static readonly DependencyProperty CardColumnsProperty = DependencyProperty.Register(
        nameof(CardColumns),
        typeof(int),
        typeof(BroadcastProfilesTileView),
        new PropertyMetadata(1));

    public const double TwoColumnsWidth = 520;
    public const double ThreeColumnsWidth = 780;

    private double _cardsWidth;

    public BroadcastProfilesTileView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public int CardColumns
    {
        get => (int)GetValue(CardColumnsProperty);
        set => SetValue(CardColumnsProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FontScaleManager.Changed -= OnFontScaleChanged;
        FontScaleManager.Changed += OnFontScaleChanged;
        ApplyColumns();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        FontScaleManager.Changed -= OnFontScaleChanged;
    }

    private void OnFontScaleChanged(object? sender, double scale)
    {
        Dispatcher.BeginInvoke(ApplyColumns);
    }

    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _cardsWidth = e.NewSize.Width;
        ApplyColumns();
    }

    private void ApplyColumns()
    {
        var scale = FontScaleManager.Current;

        if (_cardsWidth >= ThreeColumnsWidth * scale)
        {
            CardColumns = 3;
        }
        else if (_cardsWidth >= TwoColumnsWidth * scale)
        {
            CardColumns = 2;
        }
        else
        {
            CardColumns = 1;
        }
    }
}
