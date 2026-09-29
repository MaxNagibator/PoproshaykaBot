using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.WinForms.Forms.Settings;
using PoproshaykaBot.WinForms.Tiles;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class DashboardSettingsControlTests
{
    [SetUp]
    public void SetUp()
    {
        _catalog = Substitute.For<IDashboardTileCatalog>();
        _catalog.All.Returns([]);
        _control = new()
        {
            TileCatalog = _catalog,
        };
    }

    [TearDown]
    public void TearDown()
    {
        _control.Dispose();
    }

    private IDashboardTileCatalog _catalog = null!;
    private DashboardSettingsControl _control = null!;

    [Test]
    public void SaveSettings_NotInitialized_PreservesPendingLayout()
    {
        var initialLayout = new DashboardLayoutSettings
        {
            ColumnCount = 5,
            RowCount = 4,
            Tiles =
            [
                new()
                {
                    Id = "chat",
                    TypeId = "chat",
                    Row = 0,
                    Column = 0,
                    IsVisible = true,
                },
                new()
                {
                    Id = "stream",
                    TypeId = "stream",
                    Row = 1,
                    Column = 2,
                    ColumnSpan = 2,
                    IsVisible = true,
                },
            ],
        };

        _control.LoadSettings(initialLayout);

        var saved = _control.SaveSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Tiles, Has.Count.EqualTo(2),
                "Контрол не открывался – должен сохранить layout, который мы загрузили, а не пустоту.");

            Assert.That(saved.ColumnCount, Is.EqualTo(5));
            Assert.That(saved.RowCount, Is.EqualTo(4));
        }
    }

    [Test]
    public void SaveSettings_NotInitialized_NullDashboard_FallsBackToDefaults()
    {
        _control.LoadSettings(null);

        var saved = _control.SaveSettings();

        Assert.That(saved.Tiles, Is.Not.Empty,
            "Если LoadSettings получил null, SaveSettings без инициализации должен вернуть defaults.");
    }
}
