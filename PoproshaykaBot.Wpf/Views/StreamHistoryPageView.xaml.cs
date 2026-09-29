using KeepShell.Services.Platform;
using PoproshaykaBot.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Views;

public partial class StreamHistoryPageView : UserControl, IView<StreamHistoryPageViewModel>
{
    public static readonly DependencyProperty SegmentColumnsProperty = DependencyProperty.Register(
        nameof(SegmentColumns),
        typeof(int),
        typeof(StreamHistoryPageView),
        new PropertyMetadata(1));

    public static readonly DependencyProperty ScaledRecordCardMinWidthProperty = DependencyProperty.Register(
        nameof(ScaledRecordCardMinWidth),
        typeof(double),
        typeof(StreamHistoryPageView),
        new PropertyMetadata(RecordCardMinWidth));

    public static readonly DependencyProperty ScaledRecordsPaneMinWidthProperty = DependencyProperty.Register(
        nameof(ScaledRecordsPaneMinWidth),
        typeof(double),
        typeof(StreamHistoryPageView),
        new PropertyMetadata(RecordsPaneMinWidth));

    public const double SegmentCardMinWidth = 272;
    public const double SegmentsPaneMinWidth = 220;
    public const int SegmentCardMaxColumns = 3;
    public const double SideBySideWidth = 1240;
    public const double StackedWidth = 1200;
    public const double DetailMinWidth = 360;
    public const double TableMinWidth = 792;
    public const double DetailSplitWidth = 740;
    public const double DetailStackWidth = 700;
    public const double ChattersMinWidth = 240;
    public const double RecordCardMinWidth = 164;
    public const double RecordsPaneMinWidth = 2 * (RecordCardMinWidth + 8);
    public const double TrendStripHeight = 40;
    public const double TrendStripCompactHeight = 36;
    public const double CardHeight = 96;
    public const double CardCompactHeight = 72;
    public const double DetailRowMinHeight = 200;
    public const double DetailCardsRowMinHeight = 150;
    public const double ListRowShare = 3;
    public const double DetailRowShare = 2;
    public const double DetailCardsRowShare = 1;

    // TODO: подпись пика выходит за свой столбик на 24 DIP в каждую сторону, у крайних столбиков – на 48
    // внутрь полосы; потолок держит пятизначное значение при 80 столбиках и масштабе шрифта 1.6,
    // шестизначный пик или больший масштаб снова обрежут её, и тогда подписи нужен свой слой поверх
    // полосы вместо запаса в поле
    public static readonly Thickness TrendValueMargin = new(-24, 0, -24, 2);
    public static readonly Thickness TrendValueFirstMargin = new(0, 0, -48, 2);
    public static readonly Thickness TrendValueLastMargin = new(-48, 0, 0, 2);

    private readonly IClipboardService _clipboard;

    private bool _sideBySide;
    private bool _layoutApplied;
    private bool _detailStacked;
    private bool _detailApplied;
    private double _segmentCardsWidth;
    private double _detailWidth = DetailMinWidth;
    private double _detailWidthScale = 1;
    private double? _detailShare;
    private GridLength? _stackedDetailHeight;

    public StreamHistoryPageView()
        : this(new ClipboardService())
    {
    }

    public StreamHistoryPageView(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        InitializeComponent();
        ApplyScaledFloors();

        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        DetailSplit.SizeChanged += OnDetailSplitSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public int SegmentColumns
    {
        get => (int)GetValue(SegmentColumnsProperty);
        set => SetValue(SegmentColumnsProperty, value);
    }

    public double ScaledRecordCardMinWidth
    {
        get => (double)GetValue(ScaledRecordCardMinWidthProperty);
        set => SetValue(ScaledRecordCardMinWidthProperty, value);
    }

    public double ScaledRecordsPaneMinWidth
    {
        get => (double)GetValue(ScaledRecordsPaneMinWidthProperty);
        set => SetValue(ScaledRecordsPaneMinWidthProperty, value);
    }

    private static void Place(UIElement element, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetRowSpan(element, rowSpan);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FontScaleManager.Changed -= OnFontScaleChanged;
        FontScaleManager.Changed += OnFontScaleChanged;
        ApplyScaledFloors();
        _layoutApplied = false;
        UpdateLayoutMode();

        if (DataContext is StreamHistoryPageViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            ScrollToSelection(viewModel);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        FontScaleManager.Changed -= OnFontScaleChanged;
    }

    private void OnFontScaleChanged(object? sender, double scale)
    {
        ApplyScaledFloors();
        _layoutApplied = false;
        _detailApplied = false;
        UpdateLayoutMode();
        UpdateDetailMode();
        ApplySegmentColumns();
    }

    private void ApplyScaledFloors()
    {
        var scale = FontScaleManager.Current;

        ScaledRecordCardMinWidth = RecordCardMinWidth * scale;
        ScaledRecordsPaneMinWidth = RecordsPaneMinWidth * scale;
    }

    private void OnSegmentCardsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _segmentCardsWidth = e.NewSize.Width;
        ApplySegmentColumns();
    }

    private void ApplySegmentColumns()
    {
        if (_segmentCardsWidth <= 0)
        {
            return;
        }

        var fits = (int)(_segmentCardsWidth / (SegmentCardMinWidth * FontScaleManager.Current));

        SegmentColumns = Math.Clamp(fits, 1, SegmentCardMaxColumns);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutMode();
    }

    private void OnDetailSplitSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDetailMode();
    }

    private void UpdateDetailMode()
    {
        var width = DetailSplit.ActualWidth;

        if (width <= 0)
        {
            return;
        }

        var scale = FontScaleManager.Current;

        var stacked = _detailStacked
            ? width < DetailSplitWidth * scale
            : width < DetailStackWidth * scale;

        if (_detailApplied && stacked == _detailStacked)
        {
            return;
        }

        _detailStacked = stacked;
        _detailApplied = true;

        DetailSplit.ColumnDefinitions.Clear();
        DetailSplit.RowDefinitions.Clear();

        if (stacked)
        {
            DetailSplit.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MinHeight = 96 });
            DetailSplit.RowDefinitions.Add(new() { Height = new(12, GridUnitType.Pixel) });
            DetailSplit.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MinHeight = 120 });

            Place(SegmentsPane, 0, 0);
            Place(DetailSplitter, 1, 0);
            Place(ChattersPane, 2, 0);

            DetailSplitter.ResizeDirection = GridResizeDirection.Rows;
        }
        else
        {
            DetailSplit.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = SegmentsPaneMinWidth * scale });
            DetailSplit.ColumnDefinitions.Add(new() { Width = new(12, GridUnitType.Pixel) });
            DetailSplit.ColumnDefinitions.Add(new()
            {
                Width = GridLength.Auto,
                MinWidth = ChattersMinWidth * scale,
                MaxWidth = 360 * scale,
            });

            Place(SegmentsPane, 0, 0);
            Place(DetailSplitter, 0, 1);
            Place(ChattersPane, 0, 2);

            DetailSplitter.ResizeDirection = GridResizeDirection.Columns;
        }
    }

    private void UpdateLayoutMode()
    {
        var width = ActualWidth;

        if (width <= 0)
        {
            return;
        }

        var scale = FontScaleManager.Current;

        var sideBySide = _sideBySide
            ? width >= StackedWidth * scale
            : width >= SideBySideWidth * scale;

        if (_layoutApplied && sideBySide == _sideBySide)
        {
            ApplyDetailCeiling(width);
            return;
        }

        RememberSplit();

        _sideBySide = sideBySide;
        _layoutApplied = true;

        ApplyCompactMode(sideBySide is false, scale);

        if (sideBySide)
        {
            ApplySideBySide(scale);
            ApplyDetailCeiling(width);
        }
        else
        {
            ApplyStacked();
        }
    }

    private void RememberSplit()
    {
        var columns = PageGrid.ColumnDefinitions;
        var rows = PageGrid.RowDefinitions;

        if (columns.Count == 3)
        {
            _detailWidth = columns[2].Width.Value / _detailWidthScale;
        }
        else if (_stackedDetailHeight is { } applied && rows.Count == 4 && rows[3].Height != applied)
        {
            _detailShare = rows[3].Height.Value / (rows[1].Height.Value + rows[3].Height.Value);
        }
    }

    private void ApplyDetailCeiling(double width)
    {
        var columns = PageGrid.ColumnDefinitions;

        if (columns.Count != 3)
        {
            return;
        }

        var splitter = double.IsNaN(LayoutSplitter.Width) ? 0 : LayoutSplitter.Width;

        columns[2].MaxWidth = Math.Max(columns[2].MinWidth, width - columns[0].MinWidth - splitter);
    }

    private void ApplyCompactMode(bool compact, double scale)
    {
        TrendStrip.Height = (compact ? TrendStripCompactHeight : TrendStripHeight) * scale;
        TrendStrip.Margin = new(16, 6, 10, compact ? 6 : 0);

        if (DataContext is StreamHistoryPageViewModel viewModel)
        {
            viewModel.IsCompactLayout = compact;
        }
    }

    private void ApplyStacked()
    {
        PageGrid.ColumnDefinitions.Clear();
        PageGrid.RowDefinitions.Clear();

        var cards = DataContext is StreamHistoryPageViewModel { IsCardsView: true };
        var listHeight = new GridLength(ListRowShare, GridUnitType.Star);
        var detailHeight = new GridLength(cards ? DetailCardsRowShare : DetailRowShare, GridUnitType.Star);

        if (_detailShare is { } share)
        {
            listHeight = new(1 - share, GridUnitType.Star);
            detailHeight = new(share, GridUnitType.Star);
        }

        _stackedDetailHeight = detailHeight;

        PageGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        PageGrid.RowDefinitions.Add(new() { Height = listHeight, MinHeight = 160 });
        PageGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        PageGrid.RowDefinitions.Add(new()
        {
            Height = detailHeight,
            MinHeight = cards ? DetailCardsRowMinHeight : DetailRowMinHeight,
        });

        Place(HeaderStack, 0, 0);
        Place(TableCard, 1, 0);
        Place(LayoutSplitter, 2, 0);
        Place(DetailEmpty, 3, 0);
        Place(DetailCard, 3, 0);
        Place(HistoryEmpty, 1, 0, rowSpan: 3);

        LayoutSplitter.ResizeDirection = GridResizeDirection.Rows;
        LayoutSplitter.Width = double.NaN;
        LayoutSplitter.Height = 8;
    }

    private void ApplySideBySide(double scale)
    {
        PageGrid.ColumnDefinitions.Clear();
        PageGrid.RowDefinitions.Clear();

        PageGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        PageGrid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });

        PageGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = TableMinWidth * scale });
        PageGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        PageGrid.ColumnDefinitions.Add(new() { Width = new(_detailWidth * scale), MinWidth = DetailMinWidth * scale });

        _detailWidthScale = scale;

        Place(HeaderStack, 0, 0, columnSpan: 3);
        Place(TableCard, 1, 0);
        Place(LayoutSplitter, 1, 1);
        Place(DetailEmpty, 1, 2);
        Place(DetailCard, 1, 2);
        Place(HistoryEmpty, 1, 0, columnSpan: 3);

        LayoutSplitter.ResizeDirection = GridResizeDirection.Columns;
        LayoutSplitter.ClearValue(HeightProperty);
        LayoutSplitter.ClearValue(WidthProperty);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is StreamHistoryPageViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is StreamHistoryPageViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _layoutApplied = false;
            UpdateLayoutMode();
            ScrollToSelection(newViewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not StreamHistoryPageViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName is nameof(StreamHistoryPageViewModel.IsCardsView))
        {
            _layoutApplied = false;
            UpdateLayoutMode();
        }

        if (e.PropertyName is nameof(StreamHistoryPageViewModel.SelectedRow)
            or nameof(StreamHistoryPageViewModel.IsCardsView))
        {
            ScrollToSelection(viewModel);
        }

        if (e.PropertyName is nameof(StreamHistoryPageViewModel.Notice)
            && !string.IsNullOrEmpty(viewModel.Notice))
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AnnounceNotice));
        }
    }

    private void AnnounceNotice()
    {
        var peer = UIElementAutomationPeer.FromElement(NoticeText)
                   ?? UIElementAutomationPeer.CreatePeerForElement(NoticeText);

        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void ScrollToSelection(StreamHistoryPageViewModel viewModel)
    {
        if (viewModel.SelectedRow is not { } row)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                if (viewModel.IsCardsView)
                {
                    SessionCards.ScrollIntoView(row);
                }
                else
                {
                    SessionsList.ScrollIntoView(row);
                }
            },
            DispatcherPriority.Background);
    }

    private void OnTrendMenuClick(object sender, RoutedEventArgs e)
    {
        OpenMenu(sender);
    }

    private void OnSortMenuClick(object sender, RoutedEventArgs e)
    {
        OpenMenu(sender);
    }

    private static void OpenMenu(object sender)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnSegmentFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: StreamSessionSegmentRowViewModel segment }
            || DataContext is not StreamHistoryPageViewModel viewModel
            || !segment.CanFilter)
        {
            return;
        }

        viewModel.FilterByGameCommand.Execute(segment);
    }

    private void OnSessionHiddenClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: StreamSessionRowViewModel row }
            || DataContext is not StreamHistoryPageViewModel viewModel)
        {
            return;
        }

        viewModel.SetSessionHiddenCommand.Execute(row);
    }

    private void OnCopySessionCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ResolveSessionRow(e.Parameter) is not null;
        e.Handled = true;
    }

    private void OnCopySessionExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (ResolveSessionRow(e.Parameter) is not { } row)
        {
            return;
        }

        _clipboard.TrySetText(BuildRowClipboardText(row));
        e.Handled = true;
    }

    private StreamSessionRowViewModel? ResolveSessionRow(object? parameter)
    {
        if (parameter is StreamSessionRowViewModel parameterRow)
        {
            return parameterRow;
        }

        return (DataContext as StreamHistoryPageViewModel)?.SelectedRow;
    }

    private static string BuildRowClipboardText(StreamSessionRowViewModel row)
    {
        return string.Join(
            '\t',
            row.StartedAtFormatted,
            row.DurationFormatted,
            row.TitleFormatted,
            row.GameCellText,
            row.MessageCountFormatted,
            row.ChatterCountFormatted,
            row.PeakViewersFormatted,
            row.AverageViewersFormatted);
    }

    private void OnCategoryRowActivated(object sender, MouseButtonEventArgs e)
    {
        FilterByCategory(sender);
    }

    private void OnCategoryRowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        if (FilterByCategory(sender))
        {
            e.Handled = true;
        }
    }

    private bool FilterByCategory(object sender)
    {
        if (sender is not ListBoxItem { DataContext: StreamCategoryRowViewModel category }
            || DataContext is not StreamHistoryPageViewModel viewModel)
        {
            return false;
        }

        viewModel.FilterByGameCommand.Execute(category);

        return true;
    }

    private void OnChatterRowActivated(object sender, MouseButtonEventArgs e)
    {
        OpenChatter(sender);
    }

    private void OnChatterRowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        if (OpenChatter(sender))
        {
            e.Handled = true;
        }
    }

    private bool OpenChatter(object sender)
    {
        if (sender is not ListBoxItem { DataContext: StreamSessionChatterRowViewModel chatter }
            || DataContext is not StreamHistoryPageViewModel viewModel
            || !chatter.CanOpen)
        {
            return false;
        }

        viewModel.OpenChatterCommand.Execute(chatter);

        return true;
    }
}
