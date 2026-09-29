using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.Bootstrap;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.Infrastructure.Dashboard;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class DashboardLayoutSectionViewModel : ObservableObject
{
    private const string NoRootNotice = "Эту раскладку нельзя править деревом: сетку не режет ни один сквозной шов. Сбросьте раскладку, чтобы начать заново.";

    private readonly Dictionary<string, TileMeta> _catalog;
    private readonly Dictionary<string, DashboardTileViewModel> _previewTiles = new(StringComparer.Ordinal);
    private readonly bool _navCollapsed;

    private DashboardLayoutDraft _draft = new(DashboardLayoutDefaults.Create());
    private IReadOnlyDictionary<string, DashboardTilePlacement> _placements = new Dictionary<string, DashboardTilePlacement>(StringComparer.Ordinal);
    private double _scale = 1;
    private bool _gridFromDraft;

    [ObservableProperty]
    private string _notice = string.Empty;

    [ObservableProperty]
    private DashboardPreviewReference _reference;

    [ObservableProperty]
    private int _columnCount = DashboardLayoutDefaults.DefaultColumnCount;

    [ObservableProperty]
    private int _rowCount = DashboardLayoutDefaults.DefaultRowCount;

    public DashboardLayoutSectionViewModel(
        IEnumerable<DashboardTileViewModel> tiles,
        ISettingsStore? settings = null,
        DashboardLayoutStore? layoutStore = null)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        _catalog = new(StringComparer.Ordinal);

        foreach (var tile in tiles)
        {
            _catalog[tile.TypeId] = new(tile.TypeId, tile.Title, tile.MaxWidth, tile.MaxHeight);
            _previewTiles[tile.TypeId] = new DashboardPreviewTileViewModel(tile);
        }

        _navCollapsed = settings?.GetBool(SettingsKeys.NavCollapsed) == true;

        References =
        [
            DashboardPreviewReference.Current(RestoreWindowSize(settings, layoutStore)),
            DashboardPreviewReference.Window1024,
            DashboardPreviewReference.Window1920,
        ];

        _reference = References[0];

        Refresh();
    }

    public event EventHandler? LayoutChanged;

    public event EventHandler? Edited;

    public IReadOnlyList<DashboardPreviewReference> References { get; }

    public PaneLayout? Pane { get; private set; }

    public IReadOnlyList<TileBand> Bands { get; private set; } = [];

    public bool Stacked { get; private set; }

    public Size ContentArea { get; private set; }

    public bool CanUndo => _draft.CanUndo;

    public bool CanEditTree => Pane is not null;

    public bool TreeEdited => _draft.Version > 0;

    public double Scale
    {
        get => _scale;
        set
        {
            if (Math.Abs(_scale - value) < 0.0001 || value <= 0 || !double.IsFinite(value))
            {
                return;
            }

            _scale = value;

            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(ReferenceCaption));
        }
    }

    public string ReferenceCaption => Reference.Describe(Scale);

    public string StackNote => Stacked
        ? "При таком окне «Обзор» покажет плитки стопкой, одну под другой. Превью рисует саму раскладку, чтобы её можно было править."
        : string.Empty;

    public int MinColumnCount => DashboardLayoutDefaults.MinColumnCount;

    public int MaxColumnCount => DashboardLayoutDefaults.MaxColumnCount;

    public int MinRowCount => DashboardLayoutDefaults.MinRowCount;

    public int MaxRowCount => DashboardLayoutDefaults.MaxRowCount;

    public IReadOnlyList<int> MaxWidthPresets { get; } = [200, 300, 400, 500, 600, 800];

    public IReadOnlyList<int> MaxHeightPresets { get; } = [100, 150, 200, 250, 300, 400, 500];

    public IReadOnlyList<TileMeta> AvailablePalette =>
        _catalog.Values.Where(meta => !IsPlaced(meta.TypeId)).ToList();

    public TileMeta? Meta(string typeId)
    {
        return _catalog.GetValueOrDefault(typeId);
    }

    public DashboardTileSettings? Record(string typeId)
    {
        return _draft.Layout.Tiles.FirstOrDefault(tile => string.Equals(tile.TypeId, typeId, StringComparison.Ordinal));
    }

    public void LoadSettings(DashboardLayoutSettings? draft)
    {
        _draft = new(draft is { Tiles.Count: > 0 } ? draft : DashboardLayoutDefaults.Create());

        Notice = string.Empty;

        Refresh();
    }

    public DashboardLayoutSettings BuildLayout()
    {
        return _draft.Layout;
    }

    public void BeginGridGesture()
    {
        _draft.BeginGridGesture();
    }

    public void EndGridGesture()
    {
        _draft.EndGridGesture();
    }

    public bool Resize(IReadOnlyList<int> path, IReadOnlyList<double> weights)
    {
        if (!_draft.Resize(path, weights))
        {
            return false;
        }

        Committed();

        return true;
    }

    public void Move(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        Report(_draft.Move(sourcePath, targetPath, side));
    }

    public void Add(string typeId, IReadOnlyList<int> targetPath, PaneSide side)
    {
        if (!_catalog.ContainsKey(typeId))
        {
            return;
        }

        Report(_draft.AddAt(typeId, targetPath, side));
    }

    public PaneLayout? PreviewMove(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        var root = _draft.Preview(pane => DashboardPaneEditor.TryMove(pane, sourcePath, targetPath, side, out var result) ? result : null);

        return root is null ? null : DashboardPaneBuilder.BuildTree(root, _placements);
    }

    public PaneLayout? PreviewAdd(string typeId, IReadOnlyList<int> targetPath, PaneSide side)
    {
        if (!_previewTiles.ContainsKey(typeId))
        {
            return null;
        }

        var root = _draft.Preview(pane => DashboardLayoutDraft.Place(pane, targetPath, side, typeId));

        if (root is null)
        {
            return null;
        }

        var placements = new Dictionary<string, DashboardTilePlacement>(_placements, StringComparer.Ordinal);

        if (!placements.ContainsKey(typeId))
        {
            placements[typeId] = new(_previewTiles[typeId], 0, 0, 1, 1, null, null, null, null, false);
        }

        return DashboardPaneBuilder.BuildTree(root, placements);
    }

    public DashboardTileViewModel? PreviewTile(string typeId)
    {
        return _previewTiles.GetValueOrDefault(typeId);
    }

    public void Remove(IReadOnlyList<int> path)
    {
        switch (_draft.RemoveAt(path))
        {
            case DashboardRemoveStatus.Removed:
                Committed();
                return;

            case DashboardRemoveStatus.LastTile:
                Notice = "Нельзя убрать последнюю плитку. На панели должна остаться хотя бы одна.";
                return;

            default:
                Notice = "Эту плитку сейчас убрать нельзя.";
                return;
        }
    }

    public void SetMaxWidth(string typeId, int? value)
    {
        if (Record(typeId) is not { } record)
        {
            return;
        }

        record.MaxWidth = value;
        Committed();
    }

    public void SetMaxHeight(string typeId, int? value)
    {
        if (Record(typeId) is not { } record)
        {
            return;
        }

        record.MaxHeight = value;
        Committed();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_draft.Undo())
        {
            Notice = string.Empty;
            Committed();

            return;
        }

        Notice = "Отменять нечего: это первое состояние панели с начала правки.";
    }

    [RelayCommand]
    private void ResetLayout()
    {
        _draft.ResetToDefaults();
        Notice = string.Empty;
        Committed();
    }

    private static Size RestoreWindowSize(ISettingsStore? settings, DashboardLayoutStore? layoutStore)
    {
        if (settings is not null)
        {
            var width = settings.GetDouble(SettingsKeys.WindowWidth);
            var height = settings.GetDouble(SettingsKeys.WindowHeight);

            if (width > 0 && height > 0)
            {
                return new(width, height);
            }
        }

        if (layoutStore?.LoadMainWindow() is { Width: > 0, Height: > 0 } saved)
        {
            var bounds = WinFormsPlacementImport.ToDeviceIndependent(saved, WinFormsPlacementImport.GetSystemScale());

            return new(bounds.Width, bounds.Height);
        }

        return DashboardPreviewReference.DefaultWindow;
    }

    partial void OnReferenceChanged(DashboardPreviewReference value)
    {
        Refresh();
        OnPropertyChanged(nameof(ReferenceCaption));
    }

    partial void OnColumnCountChanged(int value)
    {
        ApplyGridSize();
    }

    partial void OnRowCountChanged(int value)
    {
        ApplyGridSize();
    }

    private void ApplyGridSize()
    {
        if (_gridFromDraft)
        {
            return;
        }

        var version = _draft.Version;
        var columns = _draft.Layout.ColumnCount;
        var rows = _draft.Layout.RowCount;
        var hidden = _draft.SetGridSize(ColumnCount, RowCount);

        if (_draft.Version == version)
        {
            return;
        }

        Committed();

        if (hidden.Count > 0)
        {
            Notice = DashboardLayoutReconciler.DescribeHiddenTiles(hidden.Select(Title).ToList());

            return;
        }

        if (Crowded(_draft.Layout.ColumnCount > columns, _draft.Layout.RowCount > rows) is { Length: > 0 } crowded)
        {
            Notice = crowded;
        }
        else if (Pane is not null)
        {
            Notice = string.Empty;
        }
    }

    private string Crowded(bool widened, bool heightened)
    {
        if (Pane is not { } pane)
        {
            return string.Empty;
        }

        if (widened
            && ContentArea.Width - pane.MinWidth(DashboardPaneSurface.ScaledStarBandMinWidth) < DashboardPaneSurface.ScaledStarBandMinWidth)
        {
            return "Новой колонке при этом эталоне почти не остаётся ширины: плитки уже на минимуме. Возьмите эталон шире или уберите плитку.";
        }

        if (heightened
            && ContentArea.Height - pane.MinHeight(DashboardPaneSurface.ScaledStarBandMinHeight) < DashboardPaneSurface.ScaledStarBandMinHeight)
        {
            return "Новой строке при этом эталоне почти не остаётся высоты: плитки уже на минимуме. Возьмите эталон выше или уберите плитку.";
        }

        return string.Empty;
    }

    private string Title(string typeId)
    {
        return _catalog.GetValueOrDefault(typeId)?.Title ?? typeId;
    }

    private bool IsPlaced(string typeId)
    {
        return Record(typeId) is { IsVisible: true };
    }

    private void Report(DashboardEditStatus status)
    {
        switch (status)
        {
            case DashboardEditStatus.Applied:
                Notice = string.Empty;
                Committed();
                return;

            case DashboardEditStatus.GridFull:
                Notice = "На панели больше нет места для ещё одного разреза. Перенесите плитку в другое место или уберите одну из соседних.";
                return;

            case DashboardEditStatus.Unavailable:
                Notice = NoRootNotice;
                return;

            default:
                Notice = "Плитку не получилось перенести на это место.";
                return;
        }
    }

    private void Committed()
    {
        Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        _gridFromDraft = true;

        ColumnCount = _draft.Layout.ColumnCount;
        RowCount = _draft.Layout.RowCount;

        _gridFromDraft = false;

        var model = DashboardPaneBuilder.Build(_draft.Layout, _previewTiles);

        foreach (var placement in model.Placements)
        {
            placement.Tile.IsCollapsed = placement.IsCollapsed;
            placement.Tile.IsCollapsedToStrip = false;
        }

        _placements = model.ByTypeId;
        Pane = model.Pane;
        Bands = model.Bands;
        ContentArea = Reference.ContentArea(_navCollapsed);
        Stacked = DashboardPaneSurface.ShouldStack(ContentArea.Width, Pane);

        DashboardPaneBuilder.ApplyCollapsedStrips(Pane, Stacked);

        if (Pane is null)
        {
            Notice = NoRootNotice;
        }

        OnPropertyChanged(nameof(Pane));
        OnPropertyChanged(nameof(Bands));
        OnPropertyChanged(nameof(Stacked));
        OnPropertyChanged(nameof(StackNote));
        OnPropertyChanged(nameof(ContentArea));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanEditTree));
        OnPropertyChanged(nameof(AvailablePalette));

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record TileMeta(string TypeId, string Title, int? DefaultMaxWidth, int? DefaultMaxHeight);
