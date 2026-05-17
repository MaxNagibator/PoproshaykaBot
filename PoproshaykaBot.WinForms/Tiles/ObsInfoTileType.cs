using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Widgets;

namespace PoproshaykaBot.WinForms.Tiles;

public sealed class ObsInfoTileType() : DashboardTileType(TypeId, "🎬 OBS", 200, 380)
{
    public const string TypeId = "obs-info";

    public override Control CreateBody(IControlFactory factory)
    {
        return factory.Create<ObsInfoWidget>();
    }
}
