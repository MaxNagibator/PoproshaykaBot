using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Support;

internal sealed class FakeLayoutStore(DashboardLayoutSettings? persisted)
    : DashboardLayoutStore(null, Path.Combine(Path.GetTempPath(), $"dashboard-layout-{Guid.NewGuid():N}.json"))
{
    private DashboardLayoutSettings? _current = persisted;

    public DashboardLayoutSettings? Saved { get; private set; }

    public int SaveCount { get; private set; }

    public bool FailSaves { get; set; }

    public override DashboardLayoutSettings? LoadDashboard()
    {
        return JsonStoreClone.DeepCloneNullable(_current);
    }

    public override void SaveDashboard(DashboardLayoutSettings layout)
    {
        if (FailSaves)
        {
            throw new IOException("Файл раскладки занят другим процессом.");
        }

        var snapshot = JsonStoreClone.DeepClone(layout);

        DashboardLayoutReconciler.SyncRoot(snapshot);

        Saved = snapshot;
        _current = JsonStoreClone.DeepClone(snapshot);
        SaveCount++;
    }
}
