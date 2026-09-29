using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Migrations;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Tests.Settings.Migrations;

[TestFixture]
public sealed class LegacySettingsLayoutMigratorTests
{
    [SetUp]
    public void SetUp()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "poproshayka-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDirectory);
        _settingsDirectory = Path.Combine(_baseDirectory, "settings");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_baseDirectory, true);
        }
        catch
        {
        }
    }

    private string _baseDirectory = null!;
    private string _settingsDirectory = null!;

    private const string MonolithicWithTokens = """
                                                {
                                                  "twitch": {
                                                    "channel": "bobito217",
                                                    "botAccount": {
                                                      "login": "thebot",
                                                      "accessToken": "bot-secret-access",
                                                      "refreshToken": "bot-secret-refresh"
                                                    }
                                                  },
                                                  "ui": {
                                                    "dashboard": { "columnCount": 4 }
                                                  }
                                                }
                                                """;

    [Test]
    public void Run_RelocatesFlatSettingsFilesIntoSubdirectory()
    {
        File.WriteAllText(Path.Combine(_baseDirectory, "accounts.json"), "{}");
        File.WriteAllText(Path.Combine(_baseDirectory, "broadcast-profiles.json"), "{}");
        File.WriteAllText(Path.Combine(_baseDirectory, "polls.json"), "{}");
        File.WriteAllText(Path.Combine(_baseDirectory, "obs-chat.json"), "{}");
        File.WriteAllText(Path.Combine(_baseDirectory, "recent-categories.json"), "{}");
        File.WriteAllText(Path.Combine(_baseDirectory, "dashboard-layout.json"), "{}");

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "accounts.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "broadcast-profiles.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "polls.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "obs-chat.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "recent-categories.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "dashboard-layout.json")), Is.True);

            Assert.That(File.Exists(Path.Combine(_baseDirectory, "accounts.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(_baseDirectory, "broadcast-profiles.json")), Is.False);
        }
    }

    [Test]
    public void Run_PreservesLegacyOriginalAsTimestampedBackup()
    {
        const string Original = "{\"original\":true}";
        File.WriteAllText(Path.Combine(_baseDirectory, "accounts.json"), Original);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var backups = Directory.GetFiles(_baseDirectory, "accounts.legacy-*.json");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(_baseDirectory, "accounts.json")), Is.False,
                "Канонический legacy-путь освобождается, чтобы повторный запуск не зацикливался");

            Assert.That(backups, Has.Length.EqualTo(1), "Должен остаться ровно один таймстампированный бэкап оригинала");
            Assert.That(File.ReadAllText(backups[0]), Is.EqualTo(Original));
            Assert.That(File.ReadAllText(Path.Combine(_settingsDirectory, "accounts.json")), Is.EqualTo(Original));
        }
    }

    [Test]
    public void Run_DoesNotOverwriteExistingTargetFile()
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(Path.Combine(_baseDirectory, "accounts.json"), "{\"legacy\":true}");
        File.WriteAllText(Path.Combine(_settingsDirectory, "accounts.json"), "{\"current\":true}");

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(Path.Combine(_settingsDirectory, "accounts.json")),
                Is.EqualTo("{\"current\":true}"),
                "Файл в settings/ имеет приоритет – мигратор не должен затирать актуальные данные");

            Assert.That(File.Exists(Path.Combine(_baseDirectory, "accounts.json")), Is.True,
                "Legacy-копия остаётся на месте, чтобы пользователь мог разобраться вручную");
        }
    }

    [Test]
    public void Run_ОборванноеКопирование_НеОставляетОбрезанныйФайлИНеТеряетОригинал()
    {
        const string Original = "{\"original\":true}";
        var legacy = Path.Combine(_baseDirectory, "accounts.json");
        var target = Path.Combine(_settingsDirectory, "accounts.json");
        File.WriteAllText(legacy, Original);
        Directory.CreateDirectory(_settingsDirectory);
        Directory.CreateDirectory(target + ".tmp");

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(target), Is.False,
                "Оборванное копирование не должно оставлять в settings/ обрезанный файл");

            Assert.That(File.Exists(legacy), Is.True, "Целый оригинал остаётся под прежним именем");
            Assert.That(File.ReadAllText(legacy), Is.EqualTo(Original));

            Assert.That(Directory.GetFiles(_baseDirectory, "accounts.legacy-*.json"), Is.Empty,
                "Без удавшейся подмены оригинал не переименовывается");
        }

        Directory.Delete(target + ".tmp", true);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(target), Is.EqualTo(Original), "Следующий запуск переносит целый оригинал");
            Assert.That(File.Exists(legacy), Is.False);
            Assert.That(Directory.GetFiles(_baseDirectory, "accounts.legacy-*.json"), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Run_SplitsMonolithicSettingsIntoSeparateFiles()
    {
        const string Monolithic = """
                                  {
                                    "twitch": {
                                      "channel": "bobito217",
                                      "botAccount": { "login": "thebot" },
                                      "broadcasterAccount": { "login": "thecaster" },
                                      "broadcastProfiles": { "profiles": [], "lastAppliedProfileId": null },
                                      "polls": { "profiles": [], "historyMaxItems": 500 },
                                      "obsChat": { "fontSize": 24 },
                                      "infrastructure": { "chatHistoryMaxItems": 1000, "recentCategories": [] }
                                    },
                                    "ui": {
                                      "dashboard": { "columnCount": 4 },
                                      "mainWindow": { "width": 1280 }
                                    }
                                  }
                                  """;

        File.WriteAllText(Path.Combine(_baseDirectory, "settings.json"), Monolithic);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "settings.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "accounts.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "broadcast-profiles.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "polls.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "obs-chat.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "recent-categories.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_settingsDirectory, "dashboard-layout.json")), Is.True);

            var migratedSettings = File.ReadAllText(Path.Combine(_settingsDirectory, "settings.json"));
            Assert.That(migratedSettings, Does.Not.Contain("\"botAccount\""));
            Assert.That(migratedSettings, Does.Not.Contain("\"broadcastProfiles\""));
            Assert.That(migratedSettings, Does.Not.Contain("\"polls\""));
            Assert.That(migratedSettings, Does.Not.Contain("\"obsChat\""));
            Assert.That(migratedSettings, Does.Not.Contain("\"dashboard\""));
            Assert.That(migratedSettings, Does.Contain("\"channel\""));
        }
    }

    [Test]
    public void Run_PicksUpStoredAccountsAfterMigration()
    {
        const string Monolithic = """
                                  {
                                    "twitch": {
                                      "botAccount": { "login": "thebot", "userId": "42" },
                                      "broadcasterAccount": { "login": "thecaster", "userId": "100" }
                                    }
                                  }
                                  """;

        File.WriteAllText(Path.Combine(_baseDirectory, "settings.json"), Monolithic);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var accountsStore = new AccountsStore(filePath: Path.Combine(_settingsDirectory, "accounts.json"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accountsStore.LoadBot().Login, Is.EqualTo("thebot"));
            Assert.That(accountsStore.LoadBot().UserId, Is.EqualTo("42"));
            Assert.That(accountsStore.LoadBroadcaster().Login, Is.EqualTo("thecaster"));
            Assert.That(accountsStore.LoadBroadcaster().UserId, Is.EqualTo("100"));
        }
    }

    [Test]
    public void Run_IsIdempotent()
    {
        const string Monolithic = """
                                  {
                                    "twitch": {
                                      "channel": "bobito217",
                                      "botAccount": { "login": "thebot" }
                                    }
                                  }
                                  """;

        File.WriteAllText(Path.Combine(_baseDirectory, "settings.json"), Monolithic);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);
        var firstAccounts = File.ReadAllText(Path.Combine(_settingsDirectory, "accounts.json"));
        var firstSettings = File.ReadAllText(Path.Combine(_settingsDirectory, "settings.json"));

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(Path.Combine(_settingsDirectory, "accounts.json")), Is.EqualTo(firstAccounts));
            Assert.That(File.ReadAllText(Path.Combine(_settingsDirectory, "settings.json")), Is.EqualTo(firstSettings));
        }
    }

    [Test]
    public void Run_SplitsOnlyInsideTheSettingsDirectory_AndLeavesNothingForSettingsManager()
    {
        File.WriteAllText(Path.Combine(_baseDirectory, "settings.json"), MonolithicWithTokens);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var settingsFile = Path.Combine(_settingsDirectory, "settings.json");
        var afterMigrator = File.ReadAllText(settingsFile);

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, settingsFile).Current;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(_baseDirectory, "*.json"), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(_baseDirectory, "settings.legacy-*.json"), Has.Length.EqualTo(1),
                "Сплиттер обязан работать по settings/, а не по корню: порядок «перенос, затем разбор» держится на этом.");

            Assert.That(File.ReadAllText(settingsFile), Is.EqualTo(afterMigrator),
                "SettingsManager поднимается после DI-сборки и по уже разобранному файлу не должен писать ничего.");

            Assert.That(Directory.GetFiles(_settingsDirectory, "settings.pre-migration-*.json"), Has.Length.EqualTo(1),
                "Второй pre-migration бэкап означал бы, что разбор монолита прошёл дважды.");

            Assert.That(settings.Twitch.Channel, Is.EqualTo("bobito217"));
        }
    }

    [Test]
    public void Run_MonolithicSettingsWithTokens_KeepsThemOnlyInAccountsFile()
    {
        File.WriteAllText(Path.Combine(_baseDirectory, "settings.json"), MonolithicWithTokens);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var backups = Directory.GetFiles(_settingsDirectory, "settings.pre-migration-*.json");
        var backup = backups.Length == 1 ? File.ReadAllText(backups[0]) : string.Empty;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(backup, Is.EqualTo(RedactedMonolith()),
                "Бэкап – это монолит с вычеркнутыми токенами: канал, раскладка и остальное обязаны дойти до разбора целиком.");

            Assert.That(File.ReadAllText(Path.Combine(_settingsDirectory, "accounts.json")), Does.Contain("bot-secret-access"),
                "Редактируется только бэкап – рабочий accounts.json обязан унести токены дальше.");

            Assert.That(File.ReadAllText(Path.Combine(_settingsDirectory, "settings.json")), Does.Not.Contain("bot-secret-access"));
        }
    }

    [Test]
    public void Разбор_монолита_не_оставляет_в_настройках_сырых_токенов_вне_accounts()
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(Path.Combine(_settingsDirectory, "accounts.json"), """{"botAccount":{"accessToken":"stale-secret-access"}}""");
        File.WriteAllText(Path.Combine(_baseDirectory, "settings.json"), MonolithicWithTokens);

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var leaks = Directory.GetFiles(_settingsDirectory)
            .Where(path => !string.Equals(Path.GetFileName(path), "accounts.json", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path) is var text
                           && (text.Contains("bot-secret-access") || text.Contains("stale-secret-access")))
            .Select(Path.GetFileName);

        Assert.That(leaks, Is.Empty,
            "Именованные копии до разбора редактируются – откатные копии settings.json и accounts.json не должны проносить токены мимо них.");
    }

    [TestCase("", TestName = "Пустой_target_восстанавливается_из_целого_оригинала")]
    [TestCase("{\"fontSize\":2", TestName = "Обрезанный_target_восстанавливается_из_целого_оригинала")]
    public void Повреждённый_target_восстанавливается_из_целого_оригинала(string damaged)
    {
        const string Original = "{\"fontSize\":24}";
        var legacy = Path.Combine(_baseDirectory, "obs-chat.json");
        var target = Path.Combine(_settingsDirectory, "obs-chat.json");
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(legacy, Original);
        File.WriteAllText(target, damaged);

        var rewritten = LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var invalid = Directory.GetFiles(_settingsDirectory, "obs-chat.invalid-*.json");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(target), Is.EqualTo(Original),
                "Target от старого сбоя переноса нечитаем, а целый оригинал лежит в корне – стор иначе встанет на умолчания.");

            Assert.That(invalid, Has.Length.EqualTo(1), "Повреждённый файл остаётся рядом с суффиксом, как у стора.");
            Assert.That(invalid.Length == 1 ? File.ReadAllText(invalid[0]) : null, Is.EqualTo(damaged));

            Assert.That(File.Exists(legacy), Is.False);
            Assert.That(Directory.GetFiles(_baseDirectory, "obs-chat.legacy-*.json"), Has.Length.EqualTo(1));

            Assert.That(rewritten, Does.Contain("obs-chat.json"),
                "Лечение – такая же запись мимо стора, как переезд: гейт обязан о ней узнать.");
        }
    }

    [Test]
    public void Повреждённый_accounts_лечится_без_сырых_токенов_рядом()
    {
        var target = Path.Combine(_settingsDirectory, "accounts.json");
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(Path.Combine(_baseDirectory, "accounts.json"), """{"botAccount":{"login":"thebot","accessToken":"whole-secret"}}""");
        File.WriteAllText(target, """{"botAccount":{"login":"thebot","accessToken":"half-sec""");

        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        var leaks = Directory.GetFiles(_settingsDirectory)
            .Where(path => path != target && File.ReadAllText(path) is var text
                           && (text.Contains("half-sec") || text.Contains("whole-secret")))
            .Select(Path.GetFileName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(target), Does.Contain("whole-secret"));
            Assert.That(Directory.GetFiles(_settingsDirectory, "accounts.invalid-*.json"), Has.Length.EqualTo(1));
            Assert.That(leaks, Is.Empty, "Копия повреждённого файла аккаунтов редактируется, а откатная не переживает подмену.");
        }
    }

    [Test]
    public void Повреждённый_оригинал_не_лечит_повреждённый_target()
    {
        const string DamagedLegacy = "{\"fontSize\":2";
        const string DamagedTarget = "{\"fontSize\":9";
        var legacy = Path.Combine(_baseDirectory, "obs-chat.json");
        var target = Path.Combine(_settingsDirectory, "obs-chat.json");
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(legacy, DamagedLegacy);
        File.WriteAllText(target, DamagedTarget);

        var rewritten = LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(target), Is.EqualTo(DamagedTarget), "Лечить нечем – оба файла остаются как были.");
            Assert.That(File.ReadAllText(legacy), Is.EqualTo(DamagedLegacy));
            Assert.That(Directory.GetFiles(_settingsDirectory, "obs-chat.invalid-*.json"), Is.Empty);
            Assert.That(rewritten, Does.Not.Contain("obs-chat.json"));
        }
    }

    [Test]
    public void Run_NoLegacyFiles_DoesNothing()
    {
        LegacySettingsLayoutMigrator.Run(_baseDirectory, _settingsDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.Exists(_settingsDirectory), Is.True, "Целевая директория создаётся даже без работы");
            Assert.That(Directory.GetFiles(_settingsDirectory), Is.Empty);
        }
    }

    private static string RedactedMonolith()
    {
        var expected = JsonNode.Parse(MonolithicWithTokens)!;
        var account = expected["twitch"]!["botAccount"]!;

        account["accessToken"] = string.Empty;
        account["refreshToken"] = string.Empty;

        return expected.ToJsonString(JsonStoreOptions.Default);
    }
}
