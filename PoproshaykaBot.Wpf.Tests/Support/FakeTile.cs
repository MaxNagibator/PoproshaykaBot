using PoproshaykaBot.Wpf.ViewModels.Tiles;

namespace PoproshaykaBot.Wpf.Tests.Support;

internal sealed class FakeTile(
    string typeId,
    bool fills = false,
    bool grows = false,
    bool sizesToContent = false,
    int? maxWidth = null,
    int? maxHeight = null,
    int minWidth = DashboardTileViewModel.DefaultMinWidth,
    int minHeight = DashboardTileViewModel.DefaultMinHeight)
    : DashboardTileViewModel(typeId, typeId, maxWidth, maxHeight, minWidth, minHeight)
{
    public override bool FillsAvailableSpace => fills;

    public override bool GrowsWithSpace => grows;

    public override bool SizesToContent => sizesToContent;
}
