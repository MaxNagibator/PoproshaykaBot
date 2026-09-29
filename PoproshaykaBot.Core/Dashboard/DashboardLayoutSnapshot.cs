using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

public sealed record DashboardLayoutSnapshot(DashboardLayoutSettings? Layout, int Revision);

public sealed record DashboardLayoutCommitResult(DashboardLayoutSnapshot Snapshot, bool Merged);

public sealed class DashboardLayoutChangedEventArgs(DashboardLayoutSnapshot snapshot) : EventArgs
{
    public DashboardLayoutSnapshot Snapshot { get; } = snapshot;
}
