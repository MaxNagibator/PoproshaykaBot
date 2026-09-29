using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;

namespace PoproshaykaBot.Wpf.Tests.Dashboard;

[TestFixture]
public class DashboardLayoutReloadTests
{
    [Test]
    public void Catalog_type_missing_from_the_file_is_written_back_hidden()
    {
        var store = new FakeLayoutStore(LayoutWith("stream-info"));

        CreateDashboard(store).Dispose();

        var saved = store.Saved;

        Assert.That(saved, Is.Not.Null, "Дашборд обязан дописать в файл плитку, которой там нет – иначе второй хост её не увидит.");

        var appended = saved!.Tiles.Where(tile => string.Equals(tile.TypeId, "broadcast-status", StringComparison.Ordinal)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(appended, Has.Count.EqualTo(1));
            Assert.That(appended[0].IsVisible, Is.False, "Дописанная плитка не должна сама появляться на дашборде.");
            Assert.That(saved.Tiles.Count(tile => string.Equals(tile.TypeId, "stream-info", StringComparison.Ordinal)), Is.EqualTo(1));
        });
    }

    [Test]
    public void File_that_already_covers_the_catalog_is_not_rewritten()
    {
        var store = new FakeLayoutStore(LayoutWith("stream-info", "broadcast-status"));

        CreateDashboard(store).Dispose();

        Assert.That(store.SaveCount, Is.Zero, "Обычная загрузка дашборда не должна переписывать файл, который и так полон.");
    }

    private static DashboardViewModel CreateDashboard(DashboardLayoutStore store)
    {
        return new([new FakeTile("stream-info"), new FakeTile("broadcast-status")], new(store), TimeProvider.System);
    }

    private static DashboardLayoutSettings LayoutWith(params string[] typeIds)
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 2,
            RowCount = 2,
        };

        foreach (var typeId in typeIds)
        {
            layout.Tiles.Add(new()
            {
                Id = typeId,
                TypeId = typeId,
                Order = layout.Tiles.Count,
                Row = layout.Tiles.Count,
                Column = 0,
                IsVisible = true,
            });
        }

        return layout;
    }
}
