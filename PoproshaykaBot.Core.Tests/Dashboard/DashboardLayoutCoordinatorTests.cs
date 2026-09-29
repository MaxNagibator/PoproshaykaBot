using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Tests.Dashboard;

[TestFixture]
public sealed class DashboardLayoutCoordinatorTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("dashboard-layout-coordinator");
        _coordinator = new(new(null, Path.Combine(_directory.FullName, "dashboard-layout.json")));
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private DirectoryInfo _directory = null!;
    private DashboardLayoutCoordinator _coordinator = null!;

    [Test]
    public void Mutate_ThatWrites_RaisesTheRevisionAndTellsTheOtherSurface()
    {
        var seen = new List<int>();

        _coordinator.LayoutChanged += (_, e) => seen.Add(e.Snapshot.Revision);

        _coordinator.Mutate(_ => TwoColumns());
        _coordinator.Mutate(current =>
        {
            current!.Tiles[0].IsCollapsed = true;

            return current;
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seen, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_coordinator.Read().Revision, Is.EqualTo(2));
            Assert.That(_coordinator.Read().Layout?.Tiles[0].IsCollapsed, Is.True);
        }
    }

    [Test]
    public void Mutate_ThatDeclinesToWrite_ChangesNothing()
    {
        _coordinator.Mutate(_ => TwoColumns());

        var raised = 0;

        _coordinator.LayoutChanged += (_, _) => raised++;

        var layout = _coordinator.Mutate(current =>
        {
            current!.Tiles[0].IsCollapsed = true;

            return null;
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(raised, Is.Zero);
            Assert.That(_coordinator.Read().Revision, Is.EqualTo(1));
            Assert.That(layout?.Tiles[0].IsCollapsed, Is.False, "Отменённая правка не должна утекать в файл через возвращённую раскладку.");
        }
    }

    [Test]
    public void Commit_OfADraftTakenBeforeTheDashboardWrote_KeepsBothEdits()
    {
        _coordinator.Mutate(_ => TwoColumns());

        var draft = _coordinator.Read();

        _coordinator.Mutate(current =>
        {
            current!.Tiles[0].IsCollapsed = true;

            return current;
        });

        draft.Layout!.ColumnCount = 3;

        var result = _coordinator.Commit(draft.Layout, draft.Revision);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Merged, Is.True, "Ревизия разошлась – коммит обязан признать, что писал не в ту версию.");
            Assert.That(result.Snapshot.Layout?.ColumnCount, Is.EqualTo(3), "Правка настроек не должна пропадать из-за свёртки на дашборде.");
            Assert.That(result.Snapshot.Layout?.Tiles[0].IsCollapsed, Is.True, "Свёртка на дашборде не должна откатываться сохранением настроек.");
        }
    }

    [Test]
    public void Commit_OfAFreshDraft_ReportsNoMerge()
    {
        _coordinator.Mutate(_ => TwoColumns());

        var draft = _coordinator.Read();
        var result = _coordinator.Commit(draft.Layout!, draft.Revision);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Merged, Is.False);
            Assert.That(result.Snapshot.Revision, Is.EqualTo(2));
        }
    }

    private static DashboardLayoutSettings TwoColumns()
    {
        return new()
        {
            ColumnCount = 2,
            RowCount = 1,
            Tiles =
            [
                new() { Id = "stream-info", TypeId = "stream-info", Order = 0 },
                new() { Id = "polls-control", TypeId = "polls-control", Order = 1, Column = 1 },
            ],
        };
    }
}
