using KeepShell.Bootstrap;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public sealed class DashboardPaneSurface
{
    public const double StackedWidthThreshold = 720;
    public const double StarBandMinWidth = 320;
    public const double StarBandMinHeight = 96;
    public const double SplitterThickness = 6;
    public const double RoomTolerance = 0.5;
    public const double DropReach = 24;
    public const double SwapZone = 0.3;

    public required Func<DashboardTileViewModel, FrameworkElement> Tile { get; init; }

    public Func<FrameworkElement>? Hole { get; init; }

    public Action<PaneLayout, FrameworkElement>? Registered { get; init; }

    public Action<SplitPaneLayout, Grid>? SplitBuilt { get; init; }

    public Action<Grid, SplitPaneLayout, bool>? Splitters { get; init; }

    public bool Stacked { get; init; }

    public static double ScaledStarBandMinWidth => StarBandMinWidth * FontScaleManager.Current;

    public static double ScaledStarBandMinHeight => StarBandMinHeight * FontScaleManager.Current;

    public static void ApplyTracks<T>(IList<T> definitions, int count, Func<T> create, Action<T, int> apply)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(apply);

        while (definitions.Count > count)
        {
            definitions.RemoveAt(definitions.Count - 1);
        }

        while (definitions.Count < count)
        {
            definitions.Add(create());
        }

        for (var i = 0; i < count; i++)
        {
            apply(definitions[i], i);
        }
    }

    public static bool ShouldStack(double width)
    {
        return width > 0 && width < StackedWidthThreshold * FontScaleManager.Current;
    }

    public static bool ShouldStack(double width, PaneLayout? pane)
    {
        return pane is null
            ? ShouldStack(width)
            : width > 0 && width < Math.Max(StackedWidthThreshold * FontScaleManager.Current, pane.MinWidth(ScaledStarBandMinWidth));
    }

    public static void CollectLeaves(PaneLayout pane, List<TilePaneLayout> leaves)
    {
        ArgumentNullException.ThrowIfNull(leaves);

        switch (pane)
        {
            case TilePaneLayout leaf:
                leaves.Add(leaf);
                return;

            case SplitPaneLayout split:
                foreach (var child in split.Children)
                {
                    CollectLeaves(child.Pane, leaves);
                }

                return;
        }
    }

    public static TrackSize StackedRow(TilePaneLayout leaf)
    {
        ArgumentNullException.ThrowIfNull(leaf);

        var ceiling = leaf.ContentHeight;
        var floor = leaf.Strip ? leaf.Height.Min : Floor(leaf.Height, ScaledStarBandMinHeight);

        return new(GridLength.Auto, ceiling, Math.Min(floor, ceiling));
    }

    public static double TrackFloor(PaneLayoutSlot slot, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(slot);

        return alongColumns
            ? Floor(Track(slot, static target => target.Width), slot.Pane.Width, LeafMinimum(slot.Pane, ScaledStarBandMinWidth))
            : Floor(Track(slot, static target => target.Height), slot.Pane.Height, LeafMinimum(slot.Pane, ScaledStarBandMinHeight));
    }

    public static double RequiredHeight(PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        return pane.MinHeight(ScaledStarBandMinHeight);
    }

    public static FrameworkElement Wrap(FrameworkElement content)
    {
        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    public static double[] Shares(Grid grid, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var sizes = alongColumns
            ? grid.ColumnDefinitions.Select(definition => definition.ActualWidth).ToArray()
            : grid.RowDefinitions.Select(definition => definition.ActualHeight).ToArray();

        var total = sizes.Sum();

        return total <= 0 ? sizes : sizes.Select(size => size / total).ToArray();
    }

    public static PaneSide Side(Point position, Rect bounds)
    {
        var horizontal = (position.X - bounds.X) / Math.Max(bounds.Width, 1);
        var vertical = (position.Y - bounds.Y) / Math.Max(bounds.Height, 1);

        var edges = new (PaneSide Side, double Distance)[]
        {
            (PaneSide.Left, horizontal),
            (PaneSide.Right, 1 - horizontal),
            (PaneSide.Top, vertical),
            (PaneSide.Bottom, 1 - vertical),
        };

        return edges.All(edge => edge.Distance > SwapZone)
            ? PaneSide.None
            : edges.MinBy(edge => edge.Distance).Side;
    }

    public static Rect Bounds(FrameworkElement element, Visual ancestor)
    {
        ArgumentNullException.ThrowIfNull(element);

        return element.TransformToAncestor(ancestor).TransformBounds(new(0, 0, element.ActualWidth, element.ActualHeight));
    }

    public static int NearestPane(IReadOnlyList<Rect> panes, Point position, double reach, int skip = -1)
    {
        ArgumentNullException.ThrowIfNull(panes);

        var nearest = -1;
        var best = double.MaxValue;

        for (var index = 0; index < panes.Count; index++)
        {
            if (index == skip)
            {
                continue;
            }

            var distance = Distance(panes[index], position);

            if (distance > reach || distance >= best)
            {
                continue;
            }

            best = distance;
            nearest = index;
        }

        return nearest;
    }

    public static bool HasReliableLayout(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return grid is { IsMeasureValid: true, IsArrangeValid: true, ActualWidth: > 0, ActualHeight: > 0 };
    }

    public static void ApplySplitTracks(Grid grid, SplitPaneLayout split, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(split);

        if (alongColumns)
        {
            ApplyTracks(
                grid.ColumnDefinitions,
                split.Children.Count,
                static () => new ColumnDefinition(),
                (definition, index) =>
                {
                    var child = split.Children[index];

                    definition.Width = Track(child, static target => target.Width);
                    definition.MaxWidth = child.Pane.Width.Max;
                    definition.MinWidth = TrackFloor(child, alongColumns: true);
                });

            return;
        }

        ApplyTracks(
            grid.RowDefinitions,
            split.Children.Count,
            static () => new RowDefinition(),
            (definition, index) =>
            {
                var child = split.Children[index];

                definition.Height = Track(child, static target => target.Height);
                definition.MaxHeight = child.Pane.Height.Max;
                definition.MinHeight = TrackFloor(child, alongColumns: false);
            });
    }

    public static bool HasSplitter(SplitPaneLayout split, int index)
    {
        return SplitterPair(split, index) is not null;
    }

    public static (int Previous, int Current)? SplitterPair(SplitPaneLayout split, int boundary)
    {
        ArgumentNullException.ThrowIfNull(split);

        if (boundary < 1 || boundary >= split.Children.Count)
        {
            return null;
        }

        var previous = boundary - 1;

        while (previous >= 0 && split.Children[previous].SizesToContent)
        {
            previous--;
        }

        var current = boundary;

        while (current < split.Children.Count && split.Children[current].SizesToContent)
        {
            current++;
        }

        if (previous < 0 || current >= split.Children.Count)
        {
            return null;
        }

        return previous == boundary - 1 || current == boundary ? (previous, current) : null;
    }

    public static double[]? ResizedWeights(Grid grid, SplitPaneLayout split, bool alongColumns, int boundary)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (SplitterPair(split, boundary) is not { } pair
            || pair.Current >= (alongColumns ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count))
        {
            return null;
        }

        var first = Extent(grid, alongColumns, pair.Previous).Actual;
        var second = Extent(grid, alongColumns, pair.Current).Actual;
        var weights = split.Children.Select(child => child.Weight).ToArray();
        var shared = weights[pair.Previous] + weights[pair.Current];

        if (first + second <= 0 || !double.IsFinite(shared) || shared <= 0)
        {
            return null;
        }

        weights[pair.Previous] = shared * first / (first + second);
        weights[pair.Current] = shared - weights[pair.Previous];

        return weights;
    }

    public static string? ShareHint(Grid grid, SplitPaneLayout split, bool alongColumns, int boundary)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var shares = Shares(grid, alongColumns);

        if (SplitterPair(split, boundary) is not { } pair || pair.Current >= shares.Length)
        {
            return null;
        }

        var proportions = $"{shares[pair.Previous] * 100:0} % / {shares[pair.Current] * 100:0} %";

        var obstacle = Obstacle(
            Blocked(grid, split, alongColumns, boundary, false),
            Blocked(grid, split, alongColumns, boundary, true));

        return obstacle is null ? proportions : $"{proportions}{Environment.NewLine}{obstacle}";
    }

    public static GridSplitter CreateSplitter(bool alongColumns, int index)
    {
        var splitter = new GridSplitter
        {
            ResizeBehavior = GridResizeBehavior.PreviousAndCurrent,
            ResizeDirection = alongColumns ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            Background = Brushes.Transparent,
        };

        if (alongColumns)
        {
            splitter.Width = SplitterThickness;
            splitter.HorizontalAlignment = HorizontalAlignment.Left;
            splitter.VerticalAlignment = VerticalAlignment.Stretch;
            splitter.Margin = new(-SplitterThickness / 2, 0, 0, 0);
            splitter.Cursor = Cursors.SizeWE;
            Grid.SetColumn(splitter, index);
        }
        else
        {
            splitter.Height = SplitterThickness;
            splitter.VerticalAlignment = VerticalAlignment.Top;
            splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            splitter.Margin = new(0, -SplitterThickness / 2, 0, 0);
            splitter.Cursor = Cursors.SizeNS;
            Grid.SetRow(splitter, index);
        }

        return splitter;
    }

    public static void AddSplitters(
        Grid grid,
        SplitPaneLayout split,
        bool alongColumns,
        Action<int>? dragged = null,
        Action<int, bool>? completed = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(split);

        var added = false;

        for (var index = 1; index < split.Children.Count; index++)
        {
            if (SplitterPair(split, index) is not { } pair)
            {
                continue;
            }

            var boundary = index;
            var splitter = CreateSplitter(alongColumns, boundary);

            System.Windows.Automation.AutomationProperties.SetName(
                splitter,
                alongColumns ? "Разделитель по вертикали" : "Разделитель по горизонтали");

            if (pair.Current - pair.Previous > 1)
            {
                Bridge(splitter, grid, alongColumns, pair.Previous, pair.Current);
            }

            splitter.PreviewKeyDown += (_, args) =>
            {
                if (args.Key == Key.Escape && splitter.IsDragging)
                {
                    splitter.CancelDrag();
                    args.Handled = true;
                }
            };

            if (dragged is not null)
            {
                splitter.DragDelta += (_, _) => dragged(boundary);
            }

            if (completed is not null)
            {
                splitter.DragCompleted += (_, args) => completed(boundary, args.Canceled);
            }

            Panel.SetZIndex(splitter, 1);
            grid.Children.Add(splitter);
            added = true;
        }

        if (added)
        {
            grid.SizeChanged += (_, _) => RefreshSplitters(grid, split, alongColumns);
        }
    }

    public static void RefreshSplitters(Grid grid, SplitPaneLayout split, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(grid);

        foreach (var splitter in grid.Children.OfType<GridSplitter>())
        {
            var index = alongColumns ? Grid.GetColumn(splitter) : Grid.GetRow(splitter);
            var back = Blocked(grid, split, alongColumns, index, false);
            var forward = Blocked(grid, split, alongColumns, index, true);

            var movable = alongColumns ? Cursors.SizeWE : Cursors.SizeNS;

            splitter.Cursor = back.Length > 0 && forward.Length > 0 ? Cursors.No : movable;
            splitter.ToolTip = Obstacle(back, forward);
        }
    }

    public static string[] Blocked(Grid grid, SplitPaneLayout split, bool alongColumns, int index, bool forward)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(split);

        var tracks = alongColumns ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count;

        if (SplitterPair(split, index) is not { } pair || pair.Current >= tracks)
        {
            return [];
        }

        var shrinking = forward ? pair.Current : pair.Previous;
        var growing = forward ? pair.Previous : pair.Current;
        var shrink = Extent(grid, alongColumns, shrinking);
        var grow = Extent(grid, alongColumns, growing);
        var canShrink = shrink.Actual > shrink.Min + RoomTolerance;
        var canGrow = grow.Actual < grow.Max - RoomTolerance;

        if (canShrink && canGrow)
        {
            return [];
        }

        var floor = canShrink ? null : FloorObstacle(split.Children[shrinking], alongColumns);
        var ceiling = canGrow ? null : CeilingObstacle(split.Children[growing].Pane, alongColumns);

        return [.. new[] { floor, ceiling }.OfType<string>()];
    }

    public static string? FloorObstacle(PaneLayoutSlot slot, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(slot);

        var floor = TrackFloor(slot, alongColumns);

        if (floor <= 0)
        {
            return null;
        }

        var (required, single) = SmallestFit(slot.Pane, alongColumns);

        if (required + RoomTolerance < floor)
        {
            return alongColumns
                ? $"колонка не бывает уже {floor:0} px"
                : $"строка не бывает ниже {floor:0} px";
        }

        if (single is { } leaf)
        {
            return $"плитка «{leaf.Tile.Title}» уже на минимуме";
        }

        return alongColumns
            ? "несколько плиток подряд уже на минимуме ширины"
            : "несколько плиток одна под другой уже на минимуме высоты";
    }

    public static string? CeilingObstacle(PaneLayout pane, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(pane);

        var (allowed, single) = LargestFit(pane, alongColumns);

        if (!double.IsFinite(allowed))
        {
            return null;
        }

        if (single is { } leaf)
        {
            return alongColumns
                ? $"плитка «{leaf.Tile.Title}» не шире {allowed:0} px"
                : $"плитка «{leaf.Tile.Title}» не выше {allowed:0} px";
        }

        return alongColumns
            ? $"плитки подряд вместе не шире {allowed:0} px"
            : $"плитки одна под другой вместе не выше {allowed:0} px";
    }

    public static string? Obstacle(IEnumerable<string> back, IEnumerable<string> forward)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(forward);

        var reasons = back
            .Concat(forward)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return reasons.Length == 0 ? null : $"Разделитель упирается: {string.Join("; ", reasons)}";
    }

    public static FrameworkElement WrapScrollable(PaneLayout pane, FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(pane);

        return pane is SplitPaneLayout && pane.Scrollable ? Wrap(element) : element;
    }

    public static void ApplyRootConstraints(FrameworkElement content, PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(pane);

        content.MaxWidth = pane.Width.Max;
        content.MaxHeight = pane.Height.Max;
        content.MinWidth = Math.Min(pane.Width.Min, pane.Width.Max);
        content.MinHeight = Math.Min(pane.Height.Min, pane.Height.Max);
        content.HorizontalAlignment = Stretches(pane.FillsWidth, pane.Width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        content.VerticalAlignment = Stretches(pane.FillsHeight, pane.Height) ? VerticalAlignment.Stretch : VerticalAlignment.Top;
    }

    public static void ApplyCrossConstraints(FrameworkElement content, PaneLayout pane, bool alongColumns)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(pane);

        if (alongColumns)
        {
            content.MaxHeight = pane.Height.Max;
            content.MinHeight = Math.Min(pane.Height.Min, pane.Height.Max);
            content.VerticalAlignment = Stretches(pane.FillsHeight, pane.Height) ? VerticalAlignment.Stretch : VerticalAlignment.Top;
            return;
        }

        content.MaxWidth = pane.Width.Max;
        content.MinWidth = Math.Min(pane.Width.Min, pane.Width.Max);
        content.HorizontalAlignment = Stretches(pane.FillsWidth, pane.Width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
    }

    public static Rect? MeasurePane(PaneLayout root, PaneLayout target, Size available, Func<DashboardTileViewModel, Size>? content = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(target);

        if (available.Width <= 0 || available.Height <= 0 || double.IsInfinity(available.Width) || double.IsInfinity(available.Height))
        {
            return null;
        }

        var panes = new Dictionary<PaneLayout, FrameworkElement>(ReferenceEqualityComparer.Instance);

        var surface = new DashboardPaneSurface
        {
            Tile = tile => new PaneProbe(content is null ? default : content(tile)),
            Hole = static () => new PaneProbe(default),
            Registered = (pane, element) => panes[pane] = element,
            SplitBuilt = (pane, grid) => panes[pane] = grid,
            Splitters = static (grid, split, alongColumns) => AddSplitters(grid, split, alongColumns),
        };

        var skeleton = surface.BuildRoot(root);
        var host = new Grid();

        host.Children.Add(skeleton);
        host.Measure(available);
        host.Arrange(new(default, available));

        if (!panes.TryGetValue(target, out var element) || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return null;
        }

        return new(element.TransformToAncestor(host).Transform(default), new Size(element.ActualWidth, element.ActualHeight));
    }

    public static PaneLayout? LeafOf(PaneLayout pane, DashboardTileViewModel tile)
    {
        switch (pane)
        {
            case TilePaneLayout leaf:
                return ReferenceEquals(leaf.Tile, tile) ? leaf : null;

            case SplitPaneLayout split:
                foreach (var child in split.Children)
                {
                    if (LeafOf(child.Pane, tile) is { } found)
                    {
                        return found;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    public FrameworkElement BuildRoot(PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        var content = WrapScrollable(pane, Build(pane));

        if (pane is TilePaneLayout)
        {
            content = new Border { Child = content };
        }

        ApplyRootConstraints(content, pane);

        return content;
    }

    public static bool SameShape(PaneLayout previous, PaneLayout next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        return (previous, next) switch
        {
            (EmptyPaneLayout, EmptyPaneLayout) => true,
            (TilePaneLayout before, TilePaneLayout after) => ReferenceEquals(before.Tile, after.Tile),
            (SplitPaneLayout before, SplitPaneLayout after) => before.Orientation == after.Orientation
                && before.IsComplete == after.IsComplete
                && before.Scrollable == after.Scrollable
                && before.Children.Count == after.Children.Count
                && before.Children.Zip(after.Children).All(pair => SameShape(pair.First.Pane, pair.Second.Pane)),
            _ => false,
        };
    }

    public void Update(FrameworkElement root, PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(pane);

        ApplyRootConstraints(root, pane);

        var content = pane is TilePaneLayout && root is Border { Child: FrameworkElement child } ? child : Unwrap(pane, root);

        Refresh(content, pane);
    }

    public FrameworkElement Build(PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        if (pane is EmptyPaneLayout)
        {
            var empty = Hole?.Invoke() ?? new Border { Background = Brushes.Transparent };

            Registered?.Invoke(pane, empty);

            return empty;
        }

        if (pane is TilePaneLayout leaf)
        {
            var host = Tile(leaf.Tile);

            DashboardTileSlot.SetFills(host, leaf.Fills);
            Registered?.Invoke(pane, host);

            return host;
        }

        var split = (SplitPaneLayout)pane;
        var alongColumns = split.Orientation == SplitOrientation.Columns;
        var grid = new Grid();

        ApplySplitTracks(grid, split, alongColumns);

        for (var index = 0; index < split.Children.Count; index++)
        {
            var childPane = split.Children[index].Pane;
            var content = WrapScrollable(childPane, Build(childPane));

            ApplyCrossConstraints(content, childPane, alongColumns);

            if (alongColumns)
            {
                Grid.SetColumn(content, index);
            }
            else
            {
                Grid.SetRow(content, index);
            }

            grid.Children.Add(content);
        }

        if (split.IsComplete)
        {
            Splitters?.Invoke(grid, split, alongColumns);
        }

        SplitBuilt?.Invoke(split, grid);

        return grid;
    }

    public void Stack(Grid grid, PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(pane);

        var leaves = new List<TilePaneLayout>();

        CollectLeaves(pane, leaves);

        ApplyTracks(
            grid.RowDefinitions,
            leaves.Count,
            static () => new RowDefinition(),
            (definition, index) =>
            {
                var row = StackedRow(leaves[index]);

                definition.Height = row.Length;
                definition.MaxHeight = row.Max;
                definition.MinHeight = row.Min;
            });

        for (var index = 0; index < leaves.Count; index++)
        {
            var host = Tile(leaves[index].Tile);

            DashboardTileSlot.SetFills(host, leaves[index].Fills);
            Registered?.Invoke(leaves[index], host);
            Grid.SetRow(host, index);
            grid.Children.Add(host);
        }
    }

    public FrameworkElement BuildBandContent(TileBand band)
    {
        ArgumentNullException.ThrowIfNull(band);

        var grid = BuildBand(band);

        if (Stacked)
        {
            return grid;
        }

        return new Border
        {
            MaxWidth = band.Width.Max,
            Child = Scrollable(band) ? Wrap(grid) : grid,
        };
    }

    private static bool Scrollable(TileBand band)
    {
        return band.Rows.All(row => row.Length.IsAuto) && band.Columns.All(column => column.Length.IsAuto);
    }

    private static double Floor(TrackSize track, double minimum)
    {
        return Floor(track.Length, track, minimum);
    }

    private static double LeafMinimum(PaneLayout pane, double minimum)
    {
        return pane is EmptyPaneLayout ? 0 : minimum;
    }

    private static double Floor(GridLength length, TrackSize track, double minimum)
    {
        return Math.Min(Math.Max(track.Min, length.IsStar ? minimum : 0), track.Max);
    }

    private static GridLength Track(PaneLayoutSlot slot, Func<PaneLayout, TrackSize> axis)
    {
        return slot.HasWeight || axis(slot.Pane).Length.IsStar
            ? new(slot.Weight, GridUnitType.Star)
            : GridLength.Auto;
    }

    private static bool Stretches(bool fills, TrackSize track)
    {
        return fills && double.IsPositiveInfinity(track.Max);
    }

    private static double Distance(Rect bounds, Point position)
    {
        var horizontal = Math.Max(Math.Max(bounds.X - position.X, position.X - bounds.Right), 0);
        var vertical = Math.Max(Math.Max(bounds.Y - position.Y, position.Y - bounds.Bottom), 0);

        return Math.Sqrt((horizontal * horizontal) + (vertical * vertical));
    }

    private static void Bridge(GridSplitter splitter, Grid grid, bool alongColumns, int previous, int current)
    {
        if (alongColumns)
        {
            Grid.SetColumnSpan(splitter, 2);
        }
        else
        {
            Grid.SetRowSpan(splitter, 2);
        }

        splitter.DragDelta += (_, args) => MovePair(grid, alongColumns, previous, current, alongColumns ? args.HorizontalChange : args.VerticalChange);
    }

    private static void MovePair(Grid grid, bool alongColumns, int previous, int current, double change)
    {
        var count = alongColumns ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count;

        if (current >= count || change == 0 || !double.IsFinite(change))
        {
            return;
        }

        var first = Extent(grid, alongColumns, previous);
        var second = Extent(grid, alongColumns, current);
        var lower = Math.Max(first.Min - first.Actual, second.Actual - second.Max);
        var upper = Math.Min(first.Max - first.Actual, second.Actual - second.Min);

        if (lower > upper)
        {
            return;
        }

        var delta = Math.Clamp(change, lower, upper);

        if (delta == 0 || Math.Sign(delta) != Math.Sign(change))
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var length = LengthOf(grid, alongColumns, index);
            var unit = length.IsStar ? GridUnitType.Star : GridUnitType.Pixel;

            if (index == previous)
            {
                SetLength(grid, alongColumns, index, new(first.Actual + delta, unit));
            }
            else if (index == current)
            {
                SetLength(grid, alongColumns, index, new(second.Actual - delta, unit));
            }
            else if (length.IsStar)
            {
                SetLength(grid, alongColumns, index, new(Extent(grid, alongColumns, index).Actual, GridUnitType.Star));
            }
        }
    }

    private static GridLength LengthOf(Grid grid, bool alongColumns, int index)
    {
        return alongColumns ? grid.ColumnDefinitions[index].Width : grid.RowDefinitions[index].Height;
    }

    private static void SetLength(Grid grid, bool alongColumns, int index, GridLength length)
    {
        if (alongColumns)
        {
            grid.ColumnDefinitions[index].Width = length;
        }
        else
        {
            grid.RowDefinitions[index].Height = length;
        }
    }

    private static (double Actual, double Min, double Max) Extent(Grid grid, bool alongColumns, int index)
    {
        if (!alongColumns)
        {
            var row = grid.RowDefinitions[index];

            return (row.ActualHeight, row.MinHeight, row.MaxHeight);
        }

        var column = grid.ColumnDefinitions[index];

        return (column.ActualWidth, column.MinWidth, column.MaxWidth);
    }

    private static (double Amount, TilePaneLayout? Single) SmallestFit(PaneLayout pane, bool alongColumns)
    {
        var amount = Axis(pane, alongColumns).Min;

        switch (pane)
        {
            case TilePaneLayout leaf:
                return (amount, leaf);

            case SplitPaneLayout split:
            {
                var parts = split.Children.Select(child => SmallestFit(child.Pane, alongColumns)).ToArray();

                if ((split.Orientation == SplitOrientation.Columns) != alongColumns)
                {
                    return (amount, parts.MaxBy(part => part.Amount).Single);
                }

                var holding = parts.Where(part => part.Amount > 0).ToArray();

                return (amount, holding.Length == 1 ? holding[0].Single : null);
            }

            default:
                return (amount, null);
        }
    }

    private static (double Amount, TilePaneLayout? Single) LargestFit(PaneLayout pane, bool alongColumns)
    {
        var amount = Axis(pane, alongColumns).Max;

        switch (pane)
        {
            case TilePaneLayout leaf:
                return (amount, leaf);

            case SplitPaneLayout split:
            {
                var parts = split.Children.Select(child => LargestFit(child.Pane, alongColumns)).ToArray();

                if ((split.Orientation == SplitOrientation.Columns) != alongColumns)
                {
                    return (amount, parts.MaxBy(part => part.Amount).Single);
                }

                var holding = parts.Where(part => double.IsFinite(part.Amount)).ToArray();

                return (amount, holding.Length == 1 ? holding[0].Single : null);
            }

            default:
                return (amount, null);
        }
    }

    private static TrackSize Axis(PaneLayout pane, bool alongColumns)
    {
        return alongColumns ? pane.Width : pane.Height;
    }

    private static FrameworkElement Unwrap(PaneLayout pane, FrameworkElement element)
    {
        return pane is SplitPaneLayout && pane.Scrollable && element is ScrollViewer { Content: FrameworkElement content }
            ? content
            : element;
    }

    private void Refresh(FrameworkElement element, PaneLayout pane)
    {
        if (pane is TilePaneLayout leaf)
        {
            DashboardTileSlot.SetFills(element, leaf.Fills);
        }

        if (pane is not SplitPaneLayout split || element is not Grid grid)
        {
            Registered?.Invoke(pane, element);

            return;
        }

        var alongColumns = split.Orientation == SplitOrientation.Columns;

        ApplySplitTracks(grid, split, alongColumns);

        for (var index = 0; index < split.Children.Count; index++)
        {
            var childPane = split.Children[index].Pane;
            var content = (FrameworkElement)grid.Children[index];

            ApplyCrossConstraints(content, childPane, alongColumns);
            Refresh(Unwrap(childPane, content), childPane);
        }

        SplitBuilt?.Invoke(split, grid);
    }

    private Grid BuildBand(TileBand band)
    {
        var grid = new Grid();

        ApplyTracks(
            grid.ColumnDefinitions,
            band.Columns.Count,
            static () => new ColumnDefinition(),
            (definition, index) =>
            {
                definition.Width = Stacked ? new(1, GridUnitType.Star) : band.Columns[index].Length;
                definition.MaxWidth = Stacked ? double.PositiveInfinity : band.Columns[index].Max;
            });

        ApplyTracks(
            grid.RowDefinitions,
            band.Rows.Count,
            static () => new RowDefinition(),
            (definition, index) =>
            {
                definition.Height = band.Rows[index].Length;
                definition.MaxHeight = band.Rows[index].Max;
                definition.MinHeight = Math.Min(band.Rows[index].Min, band.Rows[index].Max);
            });

        foreach (var slot in band.Tiles)
        {
            var host = Tile(slot.Tile);

            DashboardTileSlot.SetFills(host, false);
            Grid.SetRow(host, slot.Row);
            Grid.SetColumn(host, slot.Column);
            Grid.SetRowSpan(host, slot.RowSpan);
            Grid.SetColumnSpan(host, slot.ColumnSpan);

            grid.Children.Add(host);
        }

        return grid;
    }

    private sealed class PaneProbe(Size content) : FrameworkElement
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            return new(Math.Min(content.Width, availableSize.Width), Math.Min(content.Height, availableSize.Height));
        }
    }
}
