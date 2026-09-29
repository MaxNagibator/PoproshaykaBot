using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

public enum DashboardRemoveStatus
{
    None = 0,
    Removed = 1,
    LastTile = 2,
    Rejected = 3,
}

public readonly record struct DashboardRemoveResult(DashboardRemoveStatus Status, DashboardPane? Root)
{
    public static DashboardRemoveResult Rejected { get; } = new(DashboardRemoveStatus.Rejected, null);

    public static DashboardRemoveResult LastTile { get; } = new(DashboardRemoveStatus.LastTile, null);

    public static DashboardRemoveResult Removed(DashboardPane root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return new(DashboardRemoveStatus.Removed, root);
    }
}
