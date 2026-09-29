using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.Bootstrap;
using KeepShell.ViewModels;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure.Dashboard;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private const string NotReadyNotice = "Панель пока не готова к правке. Выйдите из режима правки и войдите в него снова.";

    private readonly Dictionary<string, DashboardTileViewModel> _tilesByTypeId;
    private readonly DashboardLayoutCoordinator _coordinator;
    private readonly TimeProvider _time;
    private readonly ILogger<DashboardEditSession>? _sessionLogger;
    private readonly ModalHostViewModel? _modal;
    private readonly HashSet<DashboardTileViewModel> _observed = [];
    private DashboardEditSession? _session;
    private DashboardLayoutSettings? _layout;
    private IReadOnlyDictionary<string, DashboardTilePlacement>? _placements;
    private string? _editNotice;
    private bool _suppressCollapsePersist;
    private bool _stacked;

    public DashboardViewModel(
        IEnumerable<DashboardTileViewModel> tiles,
        DashboardLayoutCoordinator coordinator,
        TimeProvider time,
        ILogger<DashboardEditSession>? sessionLogger = null,
        ModalHostViewModel? modal = null)
    {
        _coordinator = coordinator;
        _time = time;
        _sessionLogger = sessionLogger;
        _modal = modal;
        _tilesByTypeId = new(StringComparer.Ordinal);

        foreach (var tile in tiles)
        {
            _tilesByTypeId[tile.TypeId] = tile;
        }

        Reload();

        FontScaleManager.Changed += OnFontScaleChanged;

        if (_modal is not null)
        {
            _modal.PropertyChanged += OnModalPropertyChanged;
            SetTilesModalDialogOpen(_modal.HasActive);
        }
    }

    public event EventHandler? LayoutChanged;

    public IReadOnlyList<TileBand> Bands { get; private set; } = [];

    public PaneLayout? Pane { get; private set; }

    public bool HasTiles { get; private set; }

    public bool IsEditing => _session is not null;

    public string? EditNotice
    {
        get => _editNotice;
        private set => SetProperty(ref _editNotice, value);
    }

    public bool CanEdit => Pane is not null && !_stacked;

    public bool CanUndo => _session?.CanUndo == true;

    public IReadOnlyList<HiddenTile> HiddenTiles { get; private set; } = [];

    public void OnEnter()
    {
        if (_session is null)
        {
            Reload();
        }
    }

    public void SetStacked(bool stacked)
    {
        if (_stacked == stacked)
        {
            return;
        }

        _stacked = stacked;

        if (stacked)
        {
            StopEditing();
        }

        DashboardPaneBuilder.ApplyCollapsedStrips(Pane, _stacked);

        OnPropertyChanged(nameof(CanEdit));
    }

    public bool Resize(IReadOnlyList<int> path, IReadOnlyList<double> weights)
    {
        if (_session is null)
        {
            return false;
        }

        if (_session.Resize(path, weights))
        {
            return true;
        }

        if (_sessionLogger?.IsEnabled(LogLevel.Debug) == true)
        {
            _sessionLogger.DashboardResizeRefused(
                string.Join('.', path),
                string.Join(' ', weights.Select(weight => weight.ToString("0.###", CultureInfo.InvariantCulture))));
        }

        return false;
    }

    public DashboardEditStatus Move(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        return _session?.Move(sourcePath, targetPath, side) ?? DashboardEditStatus.Unavailable;
    }

    public PaneLayout? PreviewEdit(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        if (_session is not { } session || _placements is null)
        {
            return null;
        }

        var root = session.PreviewMove(sourcePath, targetPath, side);

        return root is null ? null : DashboardPaneBuilder.BuildTree(root, _placements);
    }

    public void ReportMove(DashboardEditStatus status)
    {
        switch (status)
        {
            case DashboardEditStatus.Applied:
                EditNotice = null;
                return;

            case DashboardEditStatus.GridFull:
                ShowEditNotice("На панели больше нет места для ещё одного разреза. Перенесите плитку в другое место или уберите одну из соседних.");
                return;

            case DashboardEditStatus.Unavailable:
                ShowEditNotice(NotReadyNotice);
                return;

            default:
                ShowEditNotice("Плитку не получилось перенести на это место.");
                return;
        }
    }

    public void ShowEditNotice(string notice)
    {
        EditNotice = string.IsNullOrWhiteSpace(notice) ? null : notice;
    }

    public void StopEditing()
    {
        if (_session is null)
        {
            return;
        }

        EditNotice = null;

        _session.Changed -= OnSessionChanged;
        _session.Dispose();
        _session = null;

        SetTilesLayoutEditing(false);

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(CanUndo));

        Reload();
    }

    public void Reload()
    {
        var layout = _coordinator.Mutate(current =>
        {
            var created = current is null || current.Tiles.Count == 0;
            var target = created ? DashboardLayoutDefaults.Create() : current!;
            var appended = DashboardLayoutReconciler.AppendMissingTypes(target, _tilesByTypeId.Keys);

            target.ColumnCount = Math.Clamp(target.ColumnCount, DashboardLayoutDefaults.MinColumnCount, DashboardLayoutDefaults.MaxColumnCount);
            target.RowCount = Math.Clamp(target.RowCount, DashboardLayoutDefaults.MinRowCount, DashboardLayoutDefaults.MaxRowCount);

            DashboardLayoutReconciler.SyncRoot(target);

            return ClearFixedWeights(target) || appended || created ? target : null;
        });

        ApplyLayout(layout ?? DashboardLayoutDefaults.Create());
    }

    public void Dispose()
    {
        FontScaleManager.Changed -= OnFontScaleChanged;

        if (_modal is not null)
        {
            _modal.PropertyChanged -= OnModalPropertyChanged;
        }

        _session?.Dispose();
        _session = null;

        SetTilesLayoutEditing(false);
        SetTilesModalDialogOpen(false);

        foreach (var tile in _observed)
        {
            tile.PropertyChanged -= OnTilePropertyChanged;
        }

        _observed.Clear();
    }

    [RelayCommand]
    private void ToggleEdit()
    {
        if (_session is not null)
        {
            StopEditing();

            return;
        }

        if (!CanEdit)
        {
            return;
        }

        _session = new(_coordinator, _time, _sessionLogger);
        _session.Changed += OnSessionChanged;

        SetTilesLayoutEditing(true);

        OnPropertyChanged(nameof(IsEditing));

        ApplySession();
    }

    private void SetTilesLayoutEditing(bool editing)
    {
        foreach (var tile in _tilesByTypeId.Values)
        {
            tile.IsLayoutEditing = editing;
        }
    }

    private void SetTilesModalDialogOpen(bool open)
    {
        foreach (var tile in _tilesByTypeId.Values)
        {
            tile.IsModalDialogOpen = open;
        }
    }

    private void OnModalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_modal is not null
            && string.Equals(e.PropertyName, nameof(ModalHostViewModel.HasActive), StringComparison.Ordinal))
        {
            SetTilesModalDialogOpen(_modal.HasActive);
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (_session is { } session && !session.Undo())
        {
            ShowEditNotice("Отменять нечего: это первое состояние панели с начала правки.");
        }
    }

    [RelayCommand]
    private void ResetLayout()
    {
        _session?.ResetToDefaults();
    }

    [RelayCommand]
    private void AddTile(string? typeId)
    {
        if (_session is null || string.IsNullOrEmpty(typeId))
        {
            return;
        }

        var status = LargestLeaf() is { } target
            ? _session.Add(typeId, target, PaneSide.Right)
            : DashboardEditStatus.Unavailable;

        switch (status)
        {
            case DashboardEditStatus.Applied:
                EditNotice = null;
                return;

            case DashboardEditStatus.GridFull:
                ShowEditNotice("На панели больше нет места для новой плитки. Уберите одну из тех, что уже стоят.");
                return;

            case DashboardEditStatus.Unavailable:
                ShowEditNotice(NotReadyNotice);
                return;

            default:
                ShowEditNotice("Не получилось освободить место под эту плитку рядом с соседями. Попробуйте сначала поменять раскладку.");
                return;
        }
    }

    [RelayCommand]
    private void RemoveTile(string? typeId)
    {
        if (_session is null || string.IsNullOrEmpty(typeId))
        {
            return;
        }

        switch (_session.Remove(typeId))
        {
            case DashboardRemoveStatus.Removed:
                EditNotice = null;
                return;

            case DashboardRemoveStatus.LastTile:
                ShowEditNotice("Нельзя убрать последнюю плитку. На панели должна остаться хотя бы одна.");
                return;

            default:
                ShowEditNotice("Эту плитку сейчас убрать нельзя.");
                return;
        }
    }

    private bool ClearFixedWeights(DashboardLayoutSettings layout)
    {
        if (layout.Root is null)
        {
            return false;
        }

        var collapsed = layout.Tiles
            .Where(tile => tile.IsCollapsed)
            .Select(tile => tile.TypeId)
            .ToHashSet(StringComparer.Ordinal);

        if (!DashboardPaneEditor.TryClearFixedWeights(layout.Root, (typeId, orientation) => IsFixed(typeId, orientation, collapsed), out var relaxed))
        {
            return false;
        }

        layout.Root = relaxed;

        return true;
    }

    private bool IsFixed(string typeId, SplitOrientation orientation, HashSet<string> collapsed)
    {
        if (!_tilesByTypeId.TryGetValue(typeId, out var tile) || !tile.SizesToContent)
        {
            return false;
        }

        var isCollapsed = collapsed.Contains(typeId);

        if (tile.FillsAvailableSpace && !isCollapsed)
        {
            return false;
        }

        return orientation == SplitOrientation.Columns || isCollapsed || !tile.GrowsWithSpace;
    }

    private void OnFontScaleChanged(object? sender, double scale)
    {
        DashboardTileViewModel.NotifyScaleChanged();

        if (_layout is not null)
        {
            ApplyLayout(_layout);
        }
    }

    private void ApplyLayout(DashboardLayoutSettings layout)
    {
        _layout = layout;

        var model = DashboardPaneBuilder.Build(layout, _tilesByTypeId);

        ObserveCollapse(model.Placements);

        _placements = model.ByTypeId;
        Pane = model.Pane;
        Bands = model.Bands;
        HasTiles = model.Placements.Count > 0;

        DashboardPaneBuilder.ApplyCollapsedStrips(Pane, _stacked);

        HiddenTiles = layout.Tiles
            .Where(tile => !tile.IsVisible && _tilesByTypeId.ContainsKey(tile.TypeId))
            .Select(tile => new HiddenTile(tile.TypeId, _tilesByTypeId[tile.TypeId].Title))
            .ToList();

        OnPropertyChanged(nameof(HasTiles));
        OnPropertyChanged(nameof(HiddenTiles));
        OnPropertyChanged(nameof(CanEdit));
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ObserveCollapse(IReadOnlyList<DashboardTilePlacement> placements)
    {
        foreach (var tile in _observed)
        {
            tile.PropertyChanged -= OnTilePropertyChanged;
        }

        _observed.Clear();

        _suppressCollapsePersist = true;

        try
        {
            foreach (var placement in placements)
            {
                placement.Tile.IsCollapsed = placement.IsCollapsed;
                placement.Tile.IsCollapsedToStrip = false;

                if (_observed.Add(placement.Tile))
                {
                    placement.Tile.PropertyChanged += OnTilePropertyChanged;
                }
            }
        }
        finally
        {
            _suppressCollapsePersist = false;
        }
    }

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressCollapsePersist || sender is not DashboardTileViewModel tile)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(DashboardTileViewModel.GrowsWithSpace), StringComparison.Ordinal))
        {
            if (_session is { } editing)
            {
                ApplyLayout(editing.Draft);
            }
            else
            {
                Reload();
            }

            return;
        }

        if (!string.Equals(e.PropertyName, nameof(DashboardTileViewModel.IsCollapsed), StringComparison.Ordinal))
        {
            return;
        }

        if (_session is { } session)
        {
            if (!session.SetCollapsed(tile.TypeId, tile.IsCollapsed))
            {
                ApplyLayout(session.Draft);
            }

            return;
        }

        var persisted = false;

        var layout = _coordinator.Mutate(current =>
        {
            var setting = current?.Tiles.FirstOrDefault(t => string.Equals(t.TypeId, tile.TypeId, StringComparison.Ordinal));

            if (setting is null)
            {
                return null;
            }

            setting.IsCollapsed = tile.IsCollapsed;
            persisted = true;

            return current;
        });

        if (persisted && layout is not null)
        {
            ApplyLayout(layout);
        }
    }

    private void ApplySession()
    {
        if (_session is { } session)
        {
            EditNotice = null;

            ApplyLayout(session.Draft);
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        ApplySession();

        OnPropertyChanged(nameof(CanUndo));
    }

    private string? LargestLeaf()
    {
        return _session?.Draft.Tiles
            .Where(tile => tile.IsVisible && !DashboardLayoutTree.IsEmptySlot(tile.TypeId))
            .OrderByDescending(tile => tile.RowSpan * tile.ColumnSpan)
            .Select(tile => tile.TypeId)
            .FirstOrDefault();
    }
}

public sealed record HiddenTile(string TypeId, string Title);

public sealed record TrackSize(GridLength Length, double Max, double Min = 0);

public sealed record TileSlot(
    DashboardTileViewModel Tile,
    int Row,
    int Column,
    int RowSpan,
    int ColumnSpan);

public sealed record TileBand(
    TrackSize Width,
    IReadOnlyList<TrackSize> Columns,
    IReadOnlyList<TrackSize> Rows,
    IReadOnlyList<TileSlot> Tiles)
{
    public double MinHeight => Rows.Sum(row => Math.Min(row.Min, row.Max));
}

public abstract record PaneLayout(TrackSize Width, TrackSize Height, int[] Path)
{
    public bool Scrollable => Width.Length.IsAuto && Height.Length.IsAuto;

    public bool FillsWidth => this switch
    {
        TilePaneLayout leaf => leaf.Fills,
        EmptyPaneLayout => true,
        SplitPaneLayout { Orientation: SplitOrientation.Columns } => Width.Length.IsStar,
        SplitPaneLayout split => split.Children.Any(child => child.Pane.FillsWidth),
        _ => false,
    };

    public bool FillsHeight => this switch
    {
        TilePaneLayout leaf => leaf.Fills,
        EmptyPaneLayout => true,
        SplitPaneLayout { Orientation: SplitOrientation.Rows } => Height.Length.IsStar,
        SplitPaneLayout split => split.Children.Any(child => child.Pane.FillsHeight),
        _ => false,
    };

    public double MinWidth(double leafMinimum)
    {
        return this switch
        {
            EmptyPaneLayout => 0,
            SplitPaneLayout { Orientation: SplitOrientation.Columns } split => split.Children.Sum(child => child.Pane.MinWidth(leafMinimum)),
            SplitPaneLayout split => split.Children.Max(child => child.Pane.MinWidth(leafMinimum)),
            _ => Math.Max(Width.Min, Width.Length.IsStar ? leafMinimum : 0),
        };
    }

    public double MinHeight(double leafMinimum)
    {
        return this switch
        {
            EmptyPaneLayout => 0,
            SplitPaneLayout { Orientation: SplitOrientation.Rows } split => split.Children.Sum(child => child.Pane.MinHeight(leafMinimum)),
            SplitPaneLayout split => split.Children.Max(child => child.Pane.MinHeight(leafMinimum)),
            _ => Math.Min(Math.Max(Height.Min, Height.Length.IsStar ? leafMinimum : 0), Height.Max),
        };
    }
}

public sealed record TilePaneLayout(
    DashboardTileViewModel Tile,
    TrackSize Width,
    TrackSize Height,
    int[] Path,
    bool Fills,
    double ContentHeight,
    bool Strip = false) : PaneLayout(Width, Height, Path);

public sealed record EmptyPaneLayout(TrackSize Width, TrackSize Height, int[] Path)
    : PaneLayout(Width, Height, Path);

public sealed record SplitPaneLayout(
    SplitOrientation Orientation,
    IReadOnlyList<PaneLayoutSlot> Children,
    TrackSize Width,
    TrackSize Height,
    int[] Path,
    bool IsComplete) : PaneLayout(Width, Height, Path);

public sealed record PaneLayoutSlot(PaneLayout Pane, double Weight, bool HasWeight, bool SizesToContent);
