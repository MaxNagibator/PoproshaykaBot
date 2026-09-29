using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.Bootstrap;
using MahApps.Metro.IconPacks;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public abstract partial class DashboardTileViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isCollapsed;

    [ObservableProperty]
    private bool _isCollapsedToStrip;

    [ObservableProperty]
    private bool _isLayoutEditing;

    [ObservableProperty]
    private bool _isModalDialogOpen;

    [ObservableProperty]
    private TileHeaderStatus? _headerStatus;

    protected DashboardTileViewModel(
        string typeId,
        string title,
        int? maxWidth = null,
        int? maxHeight = null,
        int minWidth = DefaultMinWidth,
        int minHeight = DefaultMinHeight)
    {
        TypeId = typeId;
        _title = title;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        MinWidth = minWidth;
        MinHeight = minHeight;
    }

    public const int DefaultMinWidth = 220;

    public const int DefaultMinHeight = 124;

    public const int CollapsedStripWidth = 38;

    public static double ScaledCollapsedStripWidth => CollapsedStripWidth * FontScaleManager.Current;

    // TODO: пол завышен – в свёрнутом виде нижняя граница шапки снимается (лишний DIP), а кнопка
    //  свёртки высотой 28 не масштабируется вместе со шрифтом, поэтому при крупном шрифте завышение
    //  растёт; мерить шапку по факту, когда пожалуются на прокрутку, включающуюся раньше нужного
    public const int CollapsedHeaderHeight = 53;

    public static double ScaledCollapsedHeaderHeight => CollapsedHeaderHeight * FontScaleManager.Current;

    public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;

    public static void NotifyScaleChanged()
    {
        StaticPropertyChanged?.Invoke(null, new(nameof(ScaledCollapsedStripWidth)));
        StaticPropertyChanged?.Invoke(null, new(nameof(ScaledCollapsedHeaderHeight)));
    }

    public string TypeId { get; }

    public int? MaxWidth { get; }

    public int? MaxHeight { get; }

    public int MinWidth { get; }

    public int MinHeight { get; }

    public double ScaledMinWidth => MinWidth * FontScaleManager.Current;

    public double ScaledMinHeight => MinHeight * FontScaleManager.Current;

    public virtual PackIconLucideKind Icon => PackIconLucideKind.LayoutDashboard;

    public virtual bool FillsAvailableSpace => false;

    public virtual bool GrowsWithSpace => false;

    public virtual bool SizesToContent => false;

    public virtual bool ContentFillsTile => false;

    public ObservableCollection<ToolbarItemViewModel> HeaderActions { get; } = [];

    [RelayCommand]
    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }
}
