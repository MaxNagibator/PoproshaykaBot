using KeepShell.Bootstrap;
using MahApps.Metro.IconPacks;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure.Dashboard;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Settings;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class DashboardLayoutSectionView : UserControl, IView<DashboardLayoutSectionViewModel>
{
    private const string DragFormat = "DashboardTileTypeId";
    private const double MaxPreviewHeight = 560;
    private const double MinPreviewHeight = 380;
    private const double PreviewBottomReserve = 32;
    private const double MiniatureFontSize = 11;
    private const double MiniatureIconSize = 14;
    private const double MiniaturePadding = 8;
    private const double MiniatureLineHeight = 6;
    private const double DropFillOpacity = 0.35;
    private const string HoleCaption = "Пусто – перетащите сюда плитку";
    private const string HoleStrokeKey = "Border.Default";
    private const string HoleCaptionKey = "Fg.Secondary";

    private readonly Dictionary<FrameworkElement, int[]> _panePaths = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DashboardTileViewModel, FrameworkElement> _tileHosts = [];
    private readonly Dictionary<string, int[]> _tilePaths = new(StringComparer.Ordinal);

    private DashboardLayoutSectionViewModel? _viewModel;
    private ScrollViewer? _scroll;
    private Border? _dropHint;
    private Border? _dropFill;
    private TextBlock? _shareHint;
    private double _scale = 1;
    private double _builtWidth;
    private Size _canvas;

    public DashboardLayoutSectionView()
    {
        InitializeComponent();
        PreviewBox.MaxHeight = MaxPreviewHeight;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PreviewArea.SizeChanged += OnPreviewAreaSizeChanged;
        PreviewArea.LayoutUpdated += OnPreviewAreaLayoutUpdated;

        foreach (var slider in new[] { ColumnSlider, RowSlider })
        {
            slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnGridSliderDragStarted), handledEventsToo: true);
            slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnGridSliderDragCompleted), handledEventsToo: true);
        }
    }

    private void OnGridSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _viewModel?.BeginGridGesture();
    }

    private void OnGridSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _viewModel?.EndGridGesture();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Unsubscribe();

        _viewModel = e.NewValue as DashboardLayoutSectionViewModel;

        Subscribe();
        RebuildPreview();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();

        DetachScroll();
        _scroll = FindScroll();

        if (_scroll is not null)
        {
            _scroll.SizeChanged += OnScrollSizeChanged;
        }

        RebuildPreview();
    }

    private void DetachScroll()
    {
        if (_scroll is not null)
        {
            _scroll.SizeChanged -= OnScrollSizeChanged;
            _scroll = null;
        }
    }

    private ScrollViewer? FindScroll()
    {
        for (var node = VisualTreeHelper.GetParent(this); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollViewer scroll)
            {
                return scroll;
            }
        }

        return null;
    }

    private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
        {
            RebuildPreview();
        }
    }

    private double PreviewCeiling()
    {
        if (_scroll is not { ViewportHeight: > 0 } scroll || !PreviewArea.IsDescendantOf(scroll))
        {
            return MaxPreviewHeight;
        }

        var top = PreviewArea.TransformToAncestor(scroll).Transform(default).Y + scroll.VerticalOffset;

        return Math.Max(MinPreviewHeight, scroll.ViewportHeight - top - PreviewBottomReserve);
    }

    private void Subscribe()
    {
        if (_viewModel is null)
        {
            return;
        }

        Unsubscribe();

        _viewModel.LayoutChanged += OnLayoutChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unsubscribe();
        DetachScroll();
    }

    private void Unsubscribe()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.LayoutChanged -= OnLayoutChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(DashboardLayoutSectionViewModel.Notice), StringComparison.Ordinal)
            || string.IsNullOrEmpty(_viewModel?.Notice))
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AnnounceNotice));
    }

    private void AnnounceNotice()
    {
        var peer = UIElementAutomationPeer.FromElement(NoticeText) ?? UIElementAutomationPeer.CreatePeerForElement(NoticeText);

        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        RebuildPreview();
    }

    private void OnPreviewAreaSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildPreview();
    }

    private void OnPreviewAreaLayoutUpdated(object? sender, EventArgs e)
    {
        if (Math.Abs(PreviewArea.ActualWidth - _builtWidth) > 0.5)
        {
            RebuildPreview();

            return;
        }

        if (_viewModel is null || PreviewBox.Child is not FrameworkElement { ActualWidth: > 0 } canvas)
        {
            return;
        }

        var actual = PreviewBox.ActualWidth / canvas.ActualWidth;
        var drifted = Math.Abs(actual - _scale) > 0.05;

        _scale = actual;
        _viewModel.Scale = actual;

        if (drifted)
        {
            RebuildMiniatures();
        }
    }

    private void RebuildMiniatures()
    {
        HideDropHint();
        _panePaths.Clear();
        _tilePaths.Clear();
        _tileHosts.Clear();

        _canvas = CanvasSize(_viewModel!.ContentArea);

        var canvas = new Grid
        {
            Width = _canvas.Width,
            Height = _canvas.Height,
        };

        canvas.Children.Add(BuildContent(_viewModel));

        PreviewBox.Child = canvas;
    }

    private void OnPaletteMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button { Tag: string typeId } button)
        {
            DragDrop.DoDragDrop(button, new DataObject(DragFormat, typeId), DragDropEffects.Copy);
        }
    }

    private void OnMiniatureMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: string typeId } border)
        {
            DragDrop.DoDragDrop(border, new DataObject(DragFormat, typeId), DragDropEffects.Move);
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (_viewModel is null || e.Data.GetData(DragFormat) is not string typeId)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            return;
        }

        e.Effects = ShowDropHint(typeId, e.GetPosition(PreviewArea))
            ? e.AllowedEffects & (DragDropEffects.Copy | DragDropEffects.Move)
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void OnPreviewDragLeave(object sender, DragEventArgs e)
    {
        HideDropHint();
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        HideDropHint();

        if (_viewModel is null || e.Data.GetData(DragFormat) is not string typeId)
        {
            return;
        }

        var position = e.GetPosition(PreviewArea);

        if (TargetAt(position) is not { } target)
        {
            return;
        }

        var side = DashboardPaneSurface.Side(position, target.Bounds);

        if (_tilePaths.TryGetValue(typeId, out var sourcePath))
        {
            if (!sourcePath.AsSpan().SequenceEqual(target.Path))
            {
                _viewModel.Move(sourcePath, target.Path, side);
            }
        }
        else
        {
            _viewModel.Add(typeId, target.Path, side);
        }

        e.Handled = true;
    }

    private DropTarget? TargetAt(Point position)
    {
        var paths = new List<int[]>();
        var bounds = new List<Rect>();

        foreach (var (element, path) in _panePaths)
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                continue;
            }

            paths.Add(path);
            bounds.Add(DashboardPaneSurface.Bounds(element, PreviewArea));
        }

        var nearest = DashboardPaneSurface.NearestPane(bounds, position, DashboardPaneSurface.DropReach);

        return nearest < 0 ? null : new(paths[nearest], bounds[nearest]);
    }

    private bool ShowDropHint(string typeId, Point position)
    {
        if (_viewModel is null || TargetAt(position) is not { } target)
        {
            HideDropHint();

            return false;
        }

        var side = DashboardPaneSurface.Side(position, target.Bounds);
        var placed = _tilePaths.TryGetValue(typeId, out var sourcePath);

        if (placed && sourcePath!.AsSpan().SequenceEqual(target.Path))
        {
            HideDropHint();

            return false;
        }

        var pane = placed
            ? _viewModel.PreviewMove(sourcePath!, target.Path, side)
            : _viewModel.PreviewAdd(typeId, target.Path, side);

        if (pane is null
            || _viewModel.PreviewTile(typeId) is not { } tile
            || DashboardPaneSurface.LeafOf(pane, tile) is not { } leaf
            || DashboardPaneSurface.MeasurePane(pane, leaf, _canvas, TileContentSize) is not { } rect)
        {
            HideDropHint();

            return false;
        }

        var origin = PreviewBox.TransformToAncestor(PreviewArea).Transform(default);

        if (_dropHint is null)
        {
            _dropFill = new()
            {
                Opacity = DropFillOpacity,
            };

            _dropFill.SetResourceReference(Border.BackgroundProperty, ThemeKeys.AccentSoft);
            _dropFill.SetResourceReference(Border.CornerRadiusProperty, ThemeKeys.RadiusM);

            _dropHint = new()
            {
                BorderThickness = new(2),
                IsHitTestVisible = false,
                Child = _dropFill,
            };

            _dropHint.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.AccentPrimary);
            _dropHint.SetResourceReference(Border.CornerRadiusProperty, ThemeKeys.RadiusM);
        }

        if (!PreviewOverlay.Children.Contains(_dropHint))
        {
            PreviewOverlay.Children.Add(_dropHint);
        }

        _dropFill!.Visibility = side == PaneSide.None ? Visibility.Collapsed : Visibility.Visible;

        _dropHint.Width = rect.Width * _scale;
        _dropHint.Height = rect.Height * _scale;

        Canvas.SetLeft(_dropHint, origin.X + (rect.X * _scale));
        Canvas.SetTop(_dropHint, origin.Y + (rect.Y * _scale));

        return true;
    }

    private void HideDropHint()
    {
        PreviewOverlay.Children.Clear();
    }

    private Size TileContentSize(DashboardTileViewModel tile)
    {
        return _tileHosts.TryGetValue(tile, out var host) ? host.DesiredSize : default;
    }

    private void RebuildPreview()
    {
        HideDropHint();

        PreviewBox.Child = null;
        _panePaths.Clear();
        _tilePaths.Clear();
        _tileHosts.Clear();

        if (_viewModel is null)
        {
            return;
        }

        var area = _viewModel.ContentArea;

        if (area.Width <= 0 || area.Height <= 0 || PreviewArea.ActualWidth <= 0)
        {
            return;
        }

        var ceiling = PreviewCeiling();
        var canvas = CanvasSize(area);

        PreviewBox.MaxHeight = ceiling;

        _builtWidth = PreviewArea.ActualWidth;
        _scale = Math.Min(_builtWidth / canvas.Width, ceiling / canvas.Height);

        RebuildMiniatures();
    }

    private Size CanvasSize(Size area)
    {
        var width = _viewModel?.Pane is { } pane
            ? Math.Max(area.Width, pane.MinWidth(DashboardPaneSurface.ScaledStarBandMinWidth))
            : area.Width;

        return new(width, Math.Max(area.Height, RequiredHeight()));
    }

    private double RequiredHeight()
    {
        if (_viewModel is not { Pane: { } pane })
        {
            return _viewModel?.Bands.Select(band => band.MinHeight).DefaultIfEmpty(0).Max() ?? 0;
        }

        return DashboardPaneSurface.RequiredHeight(pane);
    }

    private FrameworkElement BuildContent(DashboardLayoutSectionViewModel viewModel)
    {
        var surface = Surface();

        if (viewModel.Pane is { } pane)
        {
            return surface.BuildRoot(pane);
        }

        var bands = new Grid();

        DashboardPaneSurface.ApplyTracks(
            bands.ColumnDefinitions,
            viewModel.Bands.Count,
            static () => new ColumnDefinition(),
            (definition, index) =>
            {
                var track = viewModel.Bands[index].Width;

                definition.Width = track.Length;
                definition.MaxWidth = track.Max;
                definition.MinWidth = track.Length.IsStar ? DashboardPaneSurface.ScaledStarBandMinWidth : 0;
            });

        for (var index = 0; index < viewModel.Bands.Count; index++)
        {
            var content = surface.BuildBandContent(viewModel.Bands[index]);

            Grid.SetColumn(content, index);
            bands.Children.Add(content);
        }

        return bands;
    }

    private DashboardPaneSurface Surface()
    {
        return new()
        {
            Tile = CreateMiniature,
            Hole = CreateHole,
            Registered = Register,
            Splitters = AddSplitters,
        };
    }

    private void AddSplitters(Grid grid, SplitPaneLayout split, bool alongColumns)
    {
        DashboardPaneSurface.AddSplitters(
            grid,
            split,
            alongColumns,
            index => ShowShareHint(grid, split, alongColumns, index),
            (index, canceled) => CommitShares(grid, split, alongColumns, index, canceled));

        var thickness = DashboardPaneSurface.SplitterThickness / Math.Clamp(_scale, 0.25, 1);

        foreach (var splitter in grid.Children.OfType<GridSplitter>())
        {
            if (alongColumns)
            {
                splitter.Width = thickness;
                splitter.Margin = new(-thickness / 2, 0, 0, 0);
            }
            else
            {
                splitter.Height = thickness;
                splitter.Margin = new(0, -thickness / 2, 0, 0);
            }
        }
    }

    private void Register(PaneLayout pane, FrameworkElement element)
    {
        _panePaths[element] = pane.Path;

        if (pane is TilePaneLayout leaf)
        {
            _tilePaths[leaf.Tile.TypeId] = leaf.Path;
            _tileHosts[leaf.Tile] = element;
        }
    }

    private void CommitShares(Grid grid, SplitPaneLayout split, bool alongColumns, int index, bool canceled)
    {
        HideDropHint();

        if (canceled
            || DashboardPaneSurface.ResizedWeights(grid, split, alongColumns, index) is not { } weights
            || _viewModel?.Resize(split.Path, weights) != true)
        {
            RebuildPreview();
        }
    }

    private void ShowShareHint(Grid grid, SplitPaneLayout split, bool alongColumns, int index)
    {
        if (DashboardPaneSurface.ShareHint(grid, split, alongColumns, index) is not { } hint)
        {
            return;
        }

        if (_shareHint is null)
        {
            _shareHint = new()
            {
                Padding = new(8, 4, 8, 4),
            };

            _shareHint.SetResourceReference(TextBlock.BackgroundProperty, ThemeKeys.BgSurface);
            _shareHint.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.FgPrimary);
        }

        if (!PreviewOverlay.Children.Contains(_shareHint))
        {
            PreviewOverlay.Children.Add(_shareHint);
        }

        _shareHint.Text = hint;

        var position = Mouse.GetPosition(PreviewOverlay);

        Canvas.SetLeft(_shareHint, position.X + 12);
        Canvas.SetTop(_shareHint, position.Y + 12);
    }

    private FrameworkElement CreateHole()
    {
        var inverse = 1 / _scale;

        var outline = new Rectangle
        {
            StrokeThickness = 1.5 * inverse,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            RadiusX = 4 * inverse,
            RadiusY = 4 * inverse,
        };

        outline.SetResourceReference(Shape.StrokeProperty, HoleStrokeKey);

        var caption = new TextBlock
        {
            Text = HoleCaption,
            FontSize = MiniatureFontSize * inverse,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(MiniaturePadding * inverse),
        };

        caption.SetResourceReference(ForegroundProperty, HoleCaptionKey);

        var hole = new Grid
        {
            Margin = new(2 * inverse),
            Background = Brushes.Transparent,
            ClipToBounds = true,
        };

        AutomationProperties.SetName(hole, HoleCaption);

        hole.Children.Add(outline);
        hole.Children.Add(caption);

        return hole;
    }

    private FrameworkElement CreateMiniature(DashboardTileViewModel tile)
    {
        var inverse = 1 / _scale;

        var icon = new PackIconLucide
        {
            Kind = tile.Icon,
            Width = MiniatureIconSize * inverse,
            Height = MiniatureIconSize * inverse,
            VerticalAlignment = VerticalAlignment.Center,
        };

        icon.SetResourceReference(ForegroundProperty, ThemeKeys.FgMuted);

        var header = new StackPanel { Orientation = Orientation.Horizontal };

        header.Children.Add(icon);

        if (!tile.IsCollapsedToStrip)
        {
            var title = new TextBlock
            {
                Text = tile.Title,
                ToolTip = tile.Title,
                FontSize = MiniatureFontSize * inverse,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new(6 * inverse, 0, 0, 0),
            };

            title.SetResourceReference(ForegroundProperty, ThemeKeys.FgPrimary);
            header.Children.Add(title);
        }

        var content = new Grid();

        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        content.Children.Add(header);

        if (!tile.IsCollapsed)
        {
            var body = new StackPanel { Margin = new(0, MiniaturePadding * inverse, 0, 0) };

            body.Children.Add(SchematicLine(inverse, 0.75));
            body.Children.Add(SchematicLine(inverse, 0.45));

            Grid.SetRow(body, 1);
            content.Children.Add(body);
        }

        var border = new Border
        {
            BorderThickness = new(inverse),
            CornerRadius = new(4 * inverse),
            Margin = new(2 * inverse),
            Padding = new(MiniaturePadding * inverse),
            Cursor = Cursors.SizeAll,
            Tag = tile.TypeId,
            Child = content,
            ContextMenu = BuildContextMenu(tile),
        };

        border.SetResourceReference(Border.BackgroundProperty, ThemeKeys.BgSurface);
        border.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.BorderSubtle);

        border.PreviewMouseLeftButtonDown += OnMiniatureMouseDown;

        return border;
    }

    private static Grid SchematicLine(double inverse, double share)
    {
        var line = new Grid { Margin = new(0, 0, 0, 4 * inverse) };

        line.ColumnDefinitions.Add(new() { Width = new(share, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new() { Width = new(1 - share, GridUnitType.Star) });

        var bar = new Border
        {
            Height = MiniatureLineHeight * inverse,
            CornerRadius = new(MiniatureLineHeight * inverse / 2),
        };

        bar.SetResourceReference(Border.BackgroundProperty, ThemeKeys.BgBase);

        line.Children.Add(bar);

        return line;
    }

    private ContextMenu BuildContextMenu(DashboardTileViewModel tile)
    {
        var menu = new ContextMenu();
        var record = _viewModel?.Record(tile.TypeId);
        var meta = _viewModel?.Meta(tile.TypeId);

        menu.Items.Add(BuildMaxSizeMenu(
            "Макс. ширина",
            _viewModel?.MaxWidthPresets ?? [],
            record?.MaxWidth,
            meta?.DefaultMaxWidth,
            value => _viewModel?.SetMaxWidth(tile.TypeId, value)));

        menu.Items.Add(BuildMaxSizeMenu(
            "Макс. высота",
            _viewModel?.MaxHeightPresets ?? [],
            record?.MaxHeight,
            meta?.DefaultMaxHeight,
            value => _viewModel?.SetMaxHeight(tile.TypeId, value)));

        menu.Items.Add(new Separator());

        var removeItem = new MenuItem { Header = "Убрать плитку" };

        removeItem.Click += (_, _) =>
        {
            if (_tilePaths.TryGetValue(tile.TypeId, out var path))
            {
                _viewModel?.Remove(path);
            }
        };

        menu.Items.Add(removeItem);

        return menu;
    }

    private static MenuItem BuildMaxSizeMenu(string label, IReadOnlyList<int> presets, int? overrideValue, int? typeDefault, Action<int?> setValue)
    {
        var isExplicitAuto = overrideValue is <= 0;
        var effective = isExplicitAuto ? null : overrideValue ?? typeDefault;

        string headerText;

        if (isExplicitAuto)
        {
            headerText = $"{label}: авто";
        }
        else if (effective.HasValue)
        {
            headerText = overrideValue.HasValue
                ? $"{label}: {effective.Value}px"
                : $"{label}: {effective.Value}px (по умолч.)";
        }
        else
        {
            headerText = $"{label}: авто (по умолч.)";
        }

        var root = new MenuItem { Header = headerText };

        var defaultLabel = typeDefault.HasValue
            ? $"По умолчанию ({typeDefault.Value}px)"
            : "По умолчанию (авто)";

        var defaultItem = new MenuItem { Header = defaultLabel, IsCheckable = true, IsChecked = !overrideValue.HasValue };
        defaultItem.Click += (_, _) => setValue(null);
        root.Items.Add(defaultItem);

        if (typeDefault.HasValue)
        {
            var autoItem = new MenuItem { Header = "Авто", IsCheckable = true, IsChecked = isExplicitAuto };
            autoItem.Click += (_, _) => setValue(0);
            root.Items.Add(autoItem);
        }

        root.Items.Add(new Separator());

        foreach (var preset in presets)
        {
            var value = preset;
            var item = new MenuItem { Header = $"{value}px", IsCheckable = true, IsChecked = overrideValue == value };
            item.Click += (_, _) => setValue(value);
            root.Items.Add(item);
        }

        // TODO: произвольное значение в px ввести нельзя – у IDialogService каркаса нет запроса числа,
        //       поэтому остаются пресеты, «авто» и умолчание. Завести поле, когда числовой промпт появится в KeepShell.
        return root;
    }

    private sealed record DropTarget(int[] Path, Rect Bounds);
}
