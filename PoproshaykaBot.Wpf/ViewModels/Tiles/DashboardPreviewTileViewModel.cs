using MahApps.Metro.IconPacks;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public sealed class DashboardPreviewTileViewModel : DashboardTileViewModel
{
    private readonly DashboardTileViewModel _source;

    public DashboardPreviewTileViewModel(DashboardTileViewModel source)
        : base(source.TypeId, source.Title, source.MaxWidth, source.MaxHeight, source.MinWidth, source.MinHeight)
    {
        _source = source;
    }

    public override PackIconLucideKind Icon => _source.Icon;

    public override bool FillsAvailableSpace => _source.FillsAvailableSpace;

    public override bool GrowsWithSpace => _source.GrowsWithSpace;

    public override bool SizesToContent => _source.SizesToContent;

    public override bool ContentFillsTile => _source.ContentFillsTile;
}
