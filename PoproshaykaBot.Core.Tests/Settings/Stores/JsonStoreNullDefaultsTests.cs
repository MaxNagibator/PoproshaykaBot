using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Tests.Settings.Stores;

[TestFixture]
public sealed class JsonStoreNullDefaultsTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("json-store-null-defaults");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private DirectoryInfo _directory = null!;

    [Test]
    public void PollsStore_FileWithNullCollections_ReadsThemAsEmpty()
    {
        var path = Write("polls.json", """{"profiles":null,"chatTemplates":null}""");

        var settings = new PollsStore(NullLogger<PollsStore>.Instance, path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Profiles, Is.Empty);
            Assert.That(settings.ChatTemplates, Is.Not.Null,
                "Вложенный объект, отданный файлом как null, обязан читаться дефолтом: свойство объявлено ненулевым.");
        });
    }

    [Test]
    public void BroadcastProfilesStore_FileWithANullProfileList_ReadsItAsEmpty()
    {
        var path = Write("broadcast-profiles.json", """{"profiles":null}""");

        var settings = new BroadcastProfilesStore(NullLogger<BroadcastProfilesStore>.Instance, path).Load();

        Assert.That(settings.Profiles, Is.Empty);
    }

    [Test]
    public void ObsIntegrationStore_FileWithNullSourceLists_ReadsThemAsEmpty()
    {
        var path = Write("obs-integration.json", """{"dashboardSourceNames":null,"chatRefreshSources":null}""");

        var settings = new ObsIntegrationStore(Substitute.For<IEventBus>(), NullLogger<ObsIntegrationStore>.Instance, path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.DashboardSourceNames, Is.Empty);
            Assert.That(settings.ChatRefreshSources, Is.Empty);
        });
    }

    [Test]
    public void AccountsStore_FileWithANullScopeArray_FallsBackToTheRoleDefaults()
    {
        var path = Write("accounts.json", """{"botAccount":{"scopes":null}}""");

        var account = new AccountsStore(NullLogger<AccountsStore>.Instance, path).LoadBot();

        Assert.That(account.Scopes, Is.Not.Empty,
            "Пустой массив прав подменяется дефолтами роли, а null обязан дойти до этой подмены, а не уронить чтение.");
    }

    [Test]
    public void SettingsManager_FileWithANullString_KeepsTheDefaultInsteadOfNull()
    {
        var path = Write("settings.json", """{"twitch":{"redirectUri":null}}""");

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, path).Current;

        Assert.That(settings.Twitch.RedirectUri, Is.EqualTo("http://localhost:8080"),
            "Строку правило накрывает наравне с коллекцией: `RedirectUri.Trim()` в обеих оболочках падает разыменованием.");
    }

    [Test]
    public void FileWithANullCollection_KeepsTheDefaultContentsRatherThanEmptyingThem()
    {
        var path = Write("settings.json", """{"specialCommands":{"allowedUsers":null}}""");

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, path).Current;

        Assert.That(settings.SpecialCommands.AllowedUsers, Is.EqualTo(new[] { "qp_illson" }),
            "Подставляется инициализатор модели, а не пустая коллекция: у списка бывает непустой дефолт, и `null` в файле не должен его стирать.");
    }

    [Test]
    public void DashboardLayoutStore_FileWithNullSections_KeepsThemNull()
    {
        var path = Write("dashboard-layout.json", """{"dashboard":null,"mainWindow":null}""");

        var store = new DashboardLayoutStore(NullLogger<DashboardLayoutStore>.Instance, path);

        Assert.Multiple(() =>
        {
            Assert.That(store.LoadDashboard(), Is.Null,
                "Подмена идёт по аннотации: отсутствующая раскладка объявлена нулевой и обязана остаться нулевой, иначе хост примет дефолт за сохранённую раскладку.");

            Assert.That(store.LoadMainWindow(), Is.Null);
        });
    }

    private string Write(string fileName, string content)
    {
        var path = Path.Combine(_directory.FullName, fileName);
        File.WriteAllText(path, content);

        return path;
    }
}
