using KeepShell.Bootstrap;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure.Dashboard;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PoproshaykaBot.Wpf.Views;

public partial class DashboardView : UserControl, IView<DashboardViewModel>
{
    private const double DragThreshold = 6;
    private const double DropFillOpacity = 0.35;
    private const double ShareStep = 0.05;
    private const string ResizeRefused = "Размер этой плитки сейчас не изменить.";

    private readonly Dictionary<DashboardTileViewModel, ContentControl> _hosts = [];
    private readonly HashSet<DashboardTileViewModel> _placed = [];
    private readonly Dictionary<FrameworkElement, int[]> _panePaths = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SplitPaneLayout, Grid> _splitGrids = new(ReferenceEqualityComparer.Instance);

    private DashboardViewModel? _viewModel;
    private FrameworkElement? _builtRoot;
    private PaneLayout? _builtPane;
    private bool _subscribed;
    private bool _rebuildPending;
    private DashboardTileViewModel? _dragTile;
    private Border? _dragShield;
    private Border? _dropHint;
    private Border? _dropFill;
    private TextBlock? _shareHint;
    private DropPreview? _preview;
    private Point _dragOrigin;
    private bool _dragging;
    private bool _stacked;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();

        _viewModel = e.NewValue as DashboardViewModel;

        Attach();
        RebuildGrid();
    }

    private void Attach()
    {
        if (_subscribed || _viewModel is null)
        {
            return;
        }

        _viewModel.LayoutChanged += OnLayoutChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _subscribed = true;
    }

    private void Detach()
    {
        if (!_subscribed || _viewModel is null)
        {
            return;
        }

        _viewModel.LayoutChanged -= OnLayoutChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribed = false;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(DashboardViewModel.EditNotice), StringComparison.Ordinal)
            || string.IsNullOrEmpty(_viewModel?.EditNotice))
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(AnnounceNotice));
    }

    private void AnnounceNotice()
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(EditNoticeText)
            ?? System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(EditNoticeText);

        peer?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
    }

    private void OnTileStatusUpdated(object? sender, System.Windows.Data.DataTransferEventArgs e)
    {
        if (sender is not TextBlock text)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            if (!text.IsVisible)
            {
                return;
            }

            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(text)
                ?? System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(text);

            peer?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
        }));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _preview = null;

        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        if (_rebuildPending)
        {
            RebuildGrid(e.NewSize.Width, e.NewSize.Height);

            return;
        }

        if (e.WidthChanged && ComputeStacked(e.NewSize.Width) != _stacked)
        {
            RebuildGrid(e.NewSize.Width, e.NewSize.Height);

            return;
        }

        ApplyVerticalOverflow(e.NewSize.Height);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Attach();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        RebuildGrid();
    }

    private bool ComputeStacked(double width)
    {
        return DashboardPaneSurface.ShouldStack(width, _viewModel?.Pane);
    }

    private DashboardPaneSurface Surface()
    {
        return new()
        {
            Tile = GetOrCreateHost,
            Registered = (pane, element) => _panePaths[element] = pane.Path,
            SplitBuilt = (split, grid) => _splitGrids[split] = grid,
            Splitters = _viewModel?.IsEditing == true ? AddSplitters : null,
            Stacked = _stacked,
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
    }

    private void RebuildGrid()
    {
        RebuildGrid(ActualWidth, ActualHeight);
    }

    private void RebuildGrid(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            _rebuildPending = true;

            return;
        }

        _rebuildPending = false;

        if (TryUpdateInPlace(width, height))
        {
            return;
        }

        _builtRoot = null;
        _builtPane = null;

        DetachHosts();
        BandsGrid.Children.Clear();
        BandsGrid.ColumnDefinitions.Clear();
        BandsGrid.RowDefinitions.Clear();
        _panePaths.Clear();
        _splitGrids.Clear();
        _placed.Clear();
        _preview = null;

        if (_viewModel is null)
        {
            return;
        }

        _stacked = ComputeStacked(width);
        _viewModel.SetStacked(_stacked);

        BandsGrid.Margin = default;

        if (_viewModel.Pane is { } pane)
        {
            RebuildTree(pane);
            DropUnplacedHosts();
            ApplyVerticalOverflow(height);

            return;
        }

        var bands = _viewModel.Bands;
        var surface = Surface();

        if (_stacked)
        {
            DashboardPaneSurface.ApplyTracks(
                BandsGrid.RowDefinitions,
                bands.Count,
                static () => new RowDefinition(),
                (definition, index) =>
                {
                    definition.Height = GridLength.Auto;
                    definition.MinHeight = bands[index].Width.Length.IsStar ? DashboardPaneSurface.ScaledStarBandMinHeight : 0;
                });
        }
        else
        {
            DashboardPaneSurface.ApplyTracks(
                BandsGrid.ColumnDefinitions,
                bands.Count,
                static () => new ColumnDefinition(),
                (definition, index) =>
                {
                    var track = bands[index].Width;

                    definition.Width = track.Length;
                    definition.MaxWidth = track.Max;
                    definition.MinWidth = track.Length.IsStar ? DashboardPaneSurface.ScaledStarBandMinWidth : 0;
                });
        }

        for (var index = 0; index < bands.Count; index++)
        {
            var content = surface.BuildBandContent(bands[index]);

            if (_stacked)
            {
                Grid.SetRow(content, index);
            }
            else
            {
                Grid.SetColumn(content, index);
            }

            BandsGrid.Children.Add(content);
        }

        DropUnplacedHosts();
        ApplyVerticalOverflow(height);
    }

    private double RequiredHeight()
    {
        if (_stacked || _viewModel is null)
        {
            return 0;
        }

        return _viewModel.Pane is { } pane
            ? DashboardPaneSurface.RequiredHeight(pane)
            : _viewModel.Bands.Select(band => band.MinHeight).DefaultIfEmpty(0).Max();
    }

    private void ApplyVerticalOverflow(double height)
    {
        var required = RequiredHeight();
        var overflows = height > 0 && required > height;

        BandsScroll.VerticalScrollBarVisibility = _stacked || overflows ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        BandsGrid.Height = overflows ? required : double.NaN;
    }

    private void RebuildTree(PaneLayout pane)
    {
        var surface = Surface();

        if (_stacked)
        {
            surface.Stack(BandsGrid, pane);

            return;
        }

        var root = surface.BuildRoot(pane);

        BandsGrid.Children.Add(root);

        if (_viewModel?.IsEditing != true)
        {
            _builtRoot = root;
            _builtPane = pane;
        }
    }

    private bool TryUpdateInPlace(double width, double height)
    {
        if (_viewModel is not { IsEditing: false, Pane: { } pane }
            || _builtRoot is null
            || _builtPane is null
            || ComputeStacked(width)
            || !DashboardPaneSurface.SameShape(_builtPane, pane))
        {
            return false;
        }

        _panePaths.Clear();
        _splitGrids.Clear();
        _preview = null;

        Surface().Update(_builtRoot, pane);

        _builtPane = pane;

        ApplyVerticalOverflow(height);

        return true;
    }

    private void CommitShares(Grid grid, SplitPaneLayout split, bool alongColumns, int index, bool canceled)
    {
        HideOverlay();

        if (canceled
            || DashboardPaneSurface.ResizedWeights(grid, split, alongColumns, index) is not { } weights
            || _viewModel?.Resize(split.Path, weights) != true)
        {
            RebuildGrid();
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

        if (!EditOverlay.Children.Contains(_shareHint))
        {
            EditOverlay.Children.Add(_shareHint);
        }

        _shareHint.Text = hint;

        var position = Mouse.GetPosition(EditOverlay);

        Canvas.SetLeft(_shareHint, position.X + 12);
        Canvas.SetTop(_shareHint, position.Y + 12);
    }

    private void DetachHosts()
    {
        foreach (var host in _hosts.Values)
        {
            switch (host.Parent)
            {
                case Grid parent:
                    parent.Children.Remove(host);
                    break;

                case Border border:
                    border.Child = null;
                    break;

                case ScrollViewer viewer:
                    viewer.Content = null;
                    break;
            }
        }
    }

    private void DropUnplacedHosts()
    {
        foreach (var tile in _hosts.Keys.Where(tile => !_placed.Contains(tile)).ToList())
        {
            _hosts.Remove(tile);
        }
    }

    private ContentControl GetOrCreateHost(DashboardTileViewModel tile)
    {
        _placed.Add(tile);

        if (_hosts.TryGetValue(tile, out var existing))
        {
            return existing;
        }

        var host = new ContentControl
        {
            Content = tile,
            ContentTemplate = (DataTemplate)Resources["DashboardTileChrome"],
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        _hosts[tile] = host;
        return host;
    }

    private void OnShieldMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border shield || shield.DataContext is not DashboardTileViewModel tile)
        {
            return;
        }

        shield.Focus();

        _dragTile = tile;
        _dragShield = shield;
        _dragOrigin = e.GetPosition(this);
        _dragging = false;
        _preview = null;
    }

    private void OnShieldMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTile is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);

        if (!_dragging)
        {
            if (Math.Abs(position.X - _dragOrigin.X) < DragThreshold && Math.Abs(position.Y - _dragOrigin.Y) < DragThreshold)
            {
                return;
            }

            _dragging = true;
            _dragShield?.CaptureMouse();
        }

        ShowDropHint(position);
    }

    private void OnShieldMouseUp(object sender, MouseButtonEventArgs e)
    {
        var dragging = _dragging;
        var source = _dragTile;
        var position = e.GetPosition(this);

        EndDrag();

        if (!dragging || source is null || _viewModel is null)
        {
            return;
        }

        if (TargetAt(position, source) is not { } target
            || PathOf(source) is not { } sourcePath
            || sourcePath.AsSpan().SequenceEqual(target.Path))
        {
            return;
        }

        var side = DashboardPaneSurface.Side(position, target.Bounds);

        _viewModel.ReportMove(_viewModel.Move(sourcePath, target.Path, side));
    }

    private void ShowDropHint(Point position)
    {
        if (TargetAt(position, _dragTile) is not { } target || IsSourcePane(_dragTile, target.Path))
        {
            HideOverlay();

            return;
        }

        var side = DashboardPaneSurface.Side(position, target.Bounds);

        if (_preview is not { } preview || !preview.Matches(target.Path, side))
        {
            preview = new(target.Path, side, PredictRect(target.Path, side));
            _preview = preview;
        }

        if (preview.Rect is not { } rect)
        {
            HideOverlay();

            return;
        }

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

        if (!EditOverlay.Children.Contains(_dropHint))
        {
            EditOverlay.Children.Add(_dropHint);
        }

        _dropFill!.Visibility = side == PaneSide.None ? Visibility.Collapsed : Visibility.Visible;

        _dropHint.Width = rect.Width;
        _dropHint.Height = rect.Height;

        Canvas.SetLeft(_dropHint, rect.X);
        Canvas.SetTop(_dropHint, rect.Y);
    }

    private Rect? PredictRect(int[] targetPath, PaneSide side)
    {
        if (_viewModel is null || _dragTile is null || PathOf(_dragTile) is not { } sourcePath)
        {
            return null;
        }

        if (_viewModel.PreviewEdit(sourcePath, targetPath, side) is not { } pane
            || DashboardPaneSurface.LeafOf(pane, _dragTile) is not { } leaf)
        {
            return null;
        }

        if (DashboardPaneSurface.MeasurePane(pane, leaf, new(BandsGrid.ActualWidth, BandsGrid.ActualHeight), TileContentSize) is not { } rect)
        {
            return null;
        }

        var origin = BandsGrid.TransformToAncestor(this).Transform(default);

        rect.Offset(origin.X, origin.Y);

        return rect;
    }

    private Size TileContentSize(DashboardTileViewModel tile)
    {
        return _hosts.TryGetValue(tile, out var host) ? host.DesiredSize : default;
    }

    private DropTarget? TargetAt(Point position, DashboardTileViewModel? source)
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
            bounds.Add(DashboardPaneSurface.Bounds(element, this));
        }

        var nearest = DashboardPaneSurface.NearestPane(bounds, position, DashboardPaneSurface.DropReach);

        if (nearest >= 0 && !bounds[nearest].Contains(position) && IsSourcePane(source, paths[nearest]))
        {
            nearest = DashboardPaneSurface.NearestPane(bounds, position, DashboardPaneSurface.DropReach, nearest);
        }

        return nearest < 0 ? null : new(paths[nearest], bounds[nearest]);
    }

    private bool IsSourcePane(DashboardTileViewModel? source, int[] path)
    {
        return source is not null && PathOf(source) is { } sourcePath && sourcePath.AsSpan().SequenceEqual(path);
    }

    private int[]? PathOf(DashboardTileViewModel tile)
    {
        return _hosts.TryGetValue(tile, out var host) && _panePaths.TryGetValue(host, out var path) ? path : null;
    }

    private void EndDrag()
    {
        _dragShield?.ReleaseMouseCapture();
        _dragShield = null;
        _dragTile = null;
        _dragging = false;
        _preview = null;

        HideOverlay();
    }

    private void HideOverlay()
    {
        EditOverlay.Children.Clear();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is not { IsEditing: true } viewModel)
        {
            return;
        }

        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            case Key.Escape when _dragging:
                EndDrag();
                e.Handled = true;
                return;

            case Key.Escape when Mouse.Captured is GridSplitter { IsDragging: true } splitter:
                splitter.CancelDrag();
                e.Handled = true;
                return;

            case Key.Escape:
                viewModel.StopEditing();
                e.Handled = true;
                return;

            case Key.Z when control:
                viewModel.UndoCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Delete when FocusedTile() is { } removed:
                viewModel.RemoveTileCommand.Execute(removed.TypeId);
                e.Handled = true;
                return;

            case Key.Left or Key.Right or Key.Up or Key.Down when control:
                e.Handled = MoveOrResize(e.Key, shift);
                return;
        }
    }

    private bool MoveOrResize(Key key, bool move)
    {
        if (FocusedTile() is not { } tile || _viewModel is null)
        {
            return false;
        }

        if (!move)
        {
            return AdjustShare(tile, key);
        }

        if (PathOf(tile) is not { } path || TargetFor(key) is not { } neighbour)
        {
            _viewModel.ShowEditNotice("Перенести плитку некуда: с этой стороны соседей нет.");

            return true;
        }

        _viewModel.ReportMove(_viewModel.Move(path, neighbour, SideOf(key)));

        return true;
    }

    private static PaneSide SideOf(Key key)
    {
        return key switch
        {
            Key.Left => PaneSide.Left,
            Key.Right => PaneSide.Right,
            Key.Up => PaneSide.Top,
            _ => PaneSide.Bottom,
        };
    }

    private int[]? TargetFor(Key key)
    {
        if (FocusedTile() is not { } tile || PathOf(tile) is not { } path || _viewModel?.Pane is not { } root)
        {
            return null;
        }

        var alongColumns = key is Key.Left or Key.Right;
        var back = key is Key.Left or Key.Up;

        for (var depth = path.Length - 1; depth >= 0; depth--)
        {
            if (DashboardPaneTree.Find(root, path[..depth]) is not SplitPaneLayout node
                || (node.Orientation == SplitOrientation.Columns) != alongColumns)
            {
                continue;
            }

            var index = DashboardPaneTree.IndexOfChild(node, path[..(depth + 1)]);

            if (index < 0)
            {
                return null;
            }

            var neighbour = index + (back ? -1 : 1);

            if (neighbour < 0 || neighbour >= node.Children.Count)
            {
                return null;
            }

            return DashboardPaneTree.FirstLeafPath(node.Children[neighbour].Pane);
        }

        return null;
    }

    private bool AdjustShare(DashboardTileViewModel tile, Key key)
    {
        if (PathOf(tile) is not { } path || _viewModel is not { Pane: { } root } viewModel)
        {
            return false;
        }

        var alongColumns = key is Key.Left or Key.Right;
        var step = key is Key.Left or Key.Up ? -ShareStep : ShareStep;

        for (var depth = path.Length - 1; depth >= 0; depth--)
        {
            var nodePath = path[..depth];

            if (DashboardPaneTree.Find(root, nodePath) is not SplitPaneLayout node
                || (node.Orientation == SplitOrientation.Columns) != alongColumns
                || node.Children.Count < 2)
            {
                continue;
            }

            if (AdjustAt(node, nodePath, path[..(depth + 1)], tile, alongColumns, step) is { } reason)
            {
                viewModel.ShowEditNotice(reason);
            }

            return true;
        }

        viewModel.ShowEditNotice(alongColumns
            ? "Слева и справа от этой плитки соседей нет."
            : "Сверху и снизу от этой плитки соседей нет.");

        return true;
    }

    private string? AdjustAt(
        SplitPaneLayout node,
        int[] nodePath,
        int[] childPath,
        DashboardTileViewModel tile,
        bool alongColumns,
        double step)
    {
        var index = DashboardPaneTree.IndexOfChild(node, childPath);

        if (!node.IsComplete || index < 0)
        {
            return ResizeRefused;
        }

        if (node.Children[index].SizesToContent && tile.IsCollapsed)
        {
            return alongColumns
                ? $"Плитка «{tile.Title}» свёрнута, и её ширина не меняется. Разверните плитку, чтобы задать ей долю."
                : $"Плитка «{tile.Title}» свёрнута, и её высота не меняется. Разверните плитку, чтобы задать ей долю.";
        }

        if (node.Children[index].SizesToContent)
        {
            return alongColumns
                ? $"Ширина плитки «{tile.Title}» подстроена под содержимое и не меняется."
                : $"Высота плитки «{tile.Title}» подстроена под содержимое и не меняется.";
        }

        var neighbour = DashboardPaneTree.Neighbour(node, index);

        if (neighbour < 0)
        {
            return alongColumns
                ? "Рядом нет плитки, ширину которой можно изменить."
                : "Рядом нет плитки, высоту которой можно изменить.";
        }

        if (Resistance(node, alongColumns, index, neighbour, step) is { } obstacle)
        {
            return obstacle;
        }

        var weights = node.Children.Select(child => child.Weight).ToArray();
        var total = weights.Sum();

        if (total <= 0)
        {
            return ResizeRefused;
        }

        weights[index] = (weights[index] / total) + step;
        weights[neighbour] = (weights[neighbour] / total) - step;

        if (weights[index] <= 0)
        {
            return $"Плитке «{tile.Title}» уже некуда уступать место.";
        }

        if (weights[neighbour] <= 0)
        {
            return "Соседней плитке не останется места.";
        }

        return _viewModel?.Resize(nodePath, weights) == true ? null : ResizeRefused;
    }

    private string? Resistance(SplitPaneLayout node, bool alongColumns, int index, int neighbour, double step)
    {
        if (!_splitGrids.TryGetValue(node, out var grid)
            || !DashboardPaneSurface.HasReliableLayout(grid))
        {
            return null;
        }

        var boundary = Math.Max(index, neighbour);
        var shrinking = step > 0 ? neighbour : index;

        return DashboardPaneSurface.Obstacle(
            DashboardPaneSurface.Blocked(grid, node, alongColumns, boundary, shrinking == boundary),
            []);
    }

    private DashboardTileViewModel? FocusedTile()
    {
        return Keyboard.FocusedElement is FrameworkElement { DataContext: DashboardTileViewModel tile } ? tile : null;
    }

    private sealed record DropTarget(int[] Path, Rect Bounds);

    private sealed record DropPreview(int[] Path, PaneSide Side, Rect? Rect)
    {
        public bool Matches(int[] path, PaneSide side)
        {
            return Side == side && Path.AsSpan().SequenceEqual(path);
        }
    }
}
