using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

namespace PoproshaykaBot.Core.Tests.Settings.Migrations.LegacyImport;

[TestFixture]
public sealed class LegacyDataScannerTests
{
    [SetUp]
    public void SetUp()
    {
        _source = LegacyDataFixture.CreateDirectory("poproshayka-scan-source");
        _base = LegacyDataFixture.CreateDirectory("poproshayka-scan-base");
        _default = LegacyDataFixture.CreateDirectory("poproshayka-scan-default");
    }

    [TearDown]
    public void TearDown()
    {
        LegacyDataFixture.Delete(_source);
        LegacyDataFixture.Delete(_base);
        LegacyDataFixture.Delete(_default);
    }

    private string _source = null!;
    private string _base = null!;
    private string _default = null!;

    private LegacyDataSummary? Inspect(string path)
    {
        return LegacyDataScanner.Inspect(path, _base, _default, null);
    }

    [TestCase(true, TestName = "Inspect_ReadsSummary_NewLayout")]
    [TestCase(false, TestName = "Inspect_ReadsSummary_LegacyFlatLayout")]
    public void Inspect_ReadsSummaryOfBothLayouts(bool nestedSettings)
    {
        LegacyDataFixture.FillSource(_source, nestedSettings);

        var summary = Inspect(_source);

        Assert.That(summary, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary!.Kind, Is.EqualTo(LegacyDataSourceKind.SelectedFolder));
            Assert.That(summary.Channel, Is.EqualTo("bobito217"));
            Assert.That(summary.BotLogin, Is.EqualTo("thebot"));
            Assert.That(summary.UserStatisticsCount, Is.EqualTo(3));
            Assert.That(summary.TotalMessagesProcessed, Is.EqualTo(1234UL));
            Assert.That(summary.HasOAuthTokens, Is.True);
            Assert.That(summary.HasSettingsDirectory, Is.EqualTo(nestedSettings));
            Assert.That(summary.HasMonolithicSettings, Is.EqualTo(!nestedSettings));

            Assert.That(summary.Files.Select(file => file.RelativeTargetPath),
                Is.EquivalentTo(new[]
                {
                    "users_statistics.json",
                    "bot_statistics.json",
                    "chat-zoom.txt",
                    Path.Combine("settings", "settings.json"),
                    Path.Combine("settings", "accounts.json"),
                    Path.Combine("settings", "obs-chat.json"),
                }),
                "Файлы настроек обязаны приводиться к целевому settings/ независимо от раскладки источника");
        }
    }

    [Test]
    public void Inspect_CorruptSource_DoesNotThrow_AndStillListsFiles()
    {
        LegacyDataFixture.Write(_source, "settings.json", "{ это не json");
        LegacyDataFixture.Write(_source, "users_statistics.json", "мусор");
        LegacyDataFixture.Write(_source, "bot_statistics.json", string.Empty);

        var summary = Inspect(_source);

        Assert.That(summary, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary!.Channel, Is.Null);
            Assert.That(summary.BotLogin, Is.Null);
            Assert.That(summary.UserStatisticsCount, Is.Zero);
            Assert.That(summary.TotalMessagesProcessed, Is.Zero);
            Assert.That(summary.HasOAuthTokens, Is.False);
            Assert.That(summary.Files, Has.Count.EqualTo(3),
                "Битое содержимое не отменяет того, что файлы в папке есть");
        }
    }

    [Test]
    public void Inspect_FolderWithoutImportableFiles_ReturnsNull()
    {
        Directory.CreateDirectory(Path.Combine(_source, "logs"));
        Directory.CreateDirectory(Path.Combine(_source, "WebView2"));
        LegacyDataFixture.Write(_source, Path.Combine("logs", "bot_log_20260101.txt"), "log");
        LegacyDataFixture.Write(_source, "settings.json.bak", LegacyDataFixture.Settings);
        LegacyDataFixture.Write(_source, "settings.legacy-20260101-000000.json", LegacyDataFixture.Settings);

        Assert.That(Inspect(_source), Is.Null);
    }

    [Test]
    public void Inspect_MissingDirectory_ReturnsNull()
    {
        Assert.That(Inspect(Path.Combine(_source, "нет-такой-папки")), Is.Null);
    }

    [Test]
    public void Inspect_ClassifiesKindByPath()
    {
        LegacyDataFixture.FillSource(_base, false);
        LegacyDataFixture.FillSource(_default, true);
        LegacyDataFixture.FillSource(_source, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Inspect(_base)!.Kind, Is.EqualTo(LegacyDataSourceKind.CurrentBaseDirectory));
            Assert.That(Inspect(_default)!.Kind, Is.EqualTo(LegacyDataSourceKind.DefaultAppDataDirectory));
            Assert.That(Inspect(_source)!.Kind, Is.EqualTo(LegacyDataSourceKind.SelectedFolder));
        }
    }

    [Test]
    public void Scan_OffersLegacyBaseDirectoryFirst_ThenDefaultAppData()
    {
        LegacyDataFixture.FillSource(_base, false);
        LegacyDataFixture.FillSource(_default, true);

        var candidates = LegacyDataScanner.Scan(_base, _default, null);

        Assert.That(candidates.Select(candidate => candidate.Kind),
            Is.EqualTo(new[] { LegacyDataSourceKind.CurrentBaseDirectory, LegacyDataSourceKind.DefaultAppDataDirectory }));
    }

    [Test]
    public void Scan_BaseDirectoryAlreadyMigrated_IsNotOfferedAsSource()
    {
        LegacyDataFixture.FillSource(_base, true);

        var candidates = LegacyDataScanner.Scan(_base, _base, null);

        Assert.That(candidates, Is.Empty,
            "Рабочая папка в актуальной раскладке – не источник: мигрировать в ней нечего, а копировать её в саму себя незачем");
    }

    [Test]
    public void Scan_DefaultDirectoryEqualToBase_IsNotOfferedTwice()
    {
        LegacyDataFixture.FillSource(_base, false);

        var candidates = LegacyDataScanner.Scan(_base, _base, null);

        Assert.That(candidates.Select(candidate => candidate.Kind),
            Is.EqualTo(new[] { LegacyDataSourceKind.CurrentBaseDirectory }));
    }
}
