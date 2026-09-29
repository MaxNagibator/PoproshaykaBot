using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

namespace PoproshaykaBot.Core.Tests.Settings.Migrations.LegacyImport;

[TestFixture]
public sealed class LegacyDataImporterTests
{
    [SetUp]
    public void SetUp()
    {
        _source = LegacyDataFixture.CreateDirectory("poproshayka-import-source");
        _target = LegacyDataFixture.CreateDirectory("poproshayka-import-target");
    }

    [TearDown]
    public void TearDown()
    {
        LegacyDataFixture.Delete(_source);
        LegacyDataFixture.Delete(_target);
    }

    private string _source = null!;
    private string _target = null!;

    private string TargetSettings(string fileName)
    {
        return Path.Combine(_target, "settings", fileName);
    }

    [TestCase(true, TestName = "Import_IntoEmptyTarget_NewLayoutSource")]
    [TestCase(false, TestName = "Import_IntoEmptyTarget_LegacyFlatSource")]
    public void Import_IntoEmptyTarget_CopiesEveryDataFile(bool nestedSettings)
    {
        LegacyDataFixture.FillSource(_source, nestedSettings);

        var result = LegacyDataImporter.Import(_source, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failures, Is.Empty);
            Assert.That(result.SkippedFiles, Is.Empty);
            Assert.That(result.IsInPlaceMigration, Is.False);

            Assert.That(result.CopiedFiles, Is.EquivalentTo(new[]
            {
                "users_statistics.json",
                "bot_statistics.json",
                "chat-zoom.txt",
                Path.Combine("settings", "settings.json"),
                Path.Combine("settings", "accounts.json"),
                Path.Combine("settings", "obs-chat.json"),
            }));

            Assert.That(File.ReadAllText(Path.Combine(_target, "users_statistics.json")), Is.EqualTo(LegacyDataFixture.UserStatistics));
            Assert.That(File.ReadAllText(Path.Combine(_target, "chat-zoom.txt")), Is.EqualTo("1.25"));
            Assert.That(File.ReadAllText(TargetSettings("obs-chat.json")), Is.EqualTo(LegacyDataFixture.ObsChat));
            Assert.That(File.ReadAllText(TargetSettings("accounts.json")), Is.EqualTo(LegacyDataFixture.Accounts));
            Assert.That(File.ReadAllText(TargetSettings("settings.json")), Is.EqualTo(LegacyDataFixture.Settings));

            Assert.That(File.Exists(Path.Combine(_target, "settings.json")), Is.False,
                "Файл настроек обязан попасть сразу в settings/, а не остаться в корне рабочей папки");

            Assert.That(Directory.GetFiles(_target, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }
    }

    [Test]
    public void Import_ExistingTargetFile_IsSkippedAndLeftIntact()
    {
        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "obs-chat.json"), """{"fontSize":99}""");
        LegacyDataFixture.Write(_target, "users_statistics.json", "[]");

        var result = LegacyDataImporter.Import(_source, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(TargetSettings("obs-chat.json")), Is.EqualTo("""{"fontSize":99}"""));
            Assert.That(File.ReadAllText(Path.Combine(_target, "users_statistics.json")), Is.EqualTo("[]"));

            Assert.That(result.SkippedFiles.Select(skip => skip.RelativeTargetPath),
                Is.EquivalentTo(new[] { "users_statistics.json", Path.Combine("settings", "obs-chat.json") }));

            Assert.That(result.SkippedFiles.Select(skip => skip.Reason),
                Is.All.EqualTo(LegacyImportSkipReason.TargetExists));

            Assert.That(Directory.GetFiles(_target, "*.pre-import-*", SearchOption.AllDirectories), Is.Empty,
                "Без перезаписи бэкапы не создаются – файл вообще не трогали");

            Assert.That(result.CopiedFiles, Does.Contain(Path.Combine("settings", "accounts.json")));
        }
    }

    [Test]
    public void Import_Overwrite_BacksUpTargetBeforeReplacing()
    {
        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "obs-chat.json"), """{"fontSize":99}""");

        var result = LegacyDataImporter.Import(_source, _target, true, null);
        var backups = Directory.GetFiles(Path.Combine(_target, "settings"), "obs-chat.pre-import-*.json");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(TargetSettings("obs-chat.json")), Is.EqualTo(LegacyDataFixture.ObsChat));
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(backups[0]), Is.EqualTo("""{"fontSize":99}"""),
                "Бэкап снимается до подмены – иначе перезапись уносит данные пользователя безвозвратно");

            Assert.That(result.SkippedFiles, Is.Empty);
            Assert.That(result.Failures, Is.Empty);
        }
    }

    [Test]
    public void Сорвавшаяся_подмена_оставляет_целевой_файл_с_прежним_содержимым()
    {
        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "obs-chat.json"), """{"fontSize":99}""");
        Directory.CreateDirectory(TargetSettings("obs-chat.json") + ".old");

        var result = LegacyDataImporter.Import(_source, _target, true, null);
        var relativePath = Path.Combine("settings", "obs-chat.json");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(TargetSettings("obs-chat.json")), Is.EqualTo("""{"fontSize":99}"""),
                "Сорвавшаяся подмена обязана вернуть файл из .bak – иначе перенос уносит данные пользователя, хотя копия лежит рядом.");

            Assert.That(result.CopiedFiles, Does.Not.Contain(relativePath),
                "Файл, который не удалось подменить, перенесённым не считается.");

            Assert.That(result.Failures.Select(failure => failure.RelativeTargetPath), Does.Contain(relativePath),
                "Об отказе подмены пользователю сообщают, а не глотают его молча.");

            Assert.That(File.ReadAllText(TargetSettings("obs-chat.json") + ".bak"), Is.EqualTo("""{"fontSize":99}"""),
                "Рядом с целевым файлом остаётся копия прежнего содержимого – та самая, из которой шёл откат.");
        }
    }

    [Test]
    public void Import_Overwrite_RedactsTokensInAccountsBackup()
    {
        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "accounts.json"),
            """{"botAccount":{"login":"local","accessToken":"local-access","refreshToken":"local-refresh"}}""");

        LegacyDataImporter.Import(_source, _target, true, null);

        var backups = Directory.GetFiles(Path.Combine(_target, "settings"), "accounts.pre-import-*.json");
        var backup = backups.Length == 1 ? File.ReadAllText(backups[0]) : string.Empty;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(backup, Does.Contain("local"), "Логин в бэкапе остаётся – по нему видно, чей это аккаунт");
            Assert.That(backup, Does.Not.Contain("local-access"));
            Assert.That(backup, Does.Not.Contain("local-refresh"));
        }
    }

    [Test]
    public void Перенос_поверх_аккаунтов_не_оставляет_рядом_откатную_копию_с_токенами()
    {
        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "accounts.json"), LegacyDataFixture.LocalAccounts);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "obs-chat.json"), """{"fontSize":99}""");

        var result = LegacyDataImporter.Import(_source, _target, true, null);

        var leaks = Directory.GetFiles(_target, "*", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("local-access"))
            .Select(path => Path.GetRelativePath(_target, path));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failures, Is.Empty);

            Assert.That(leaks, Is.Empty,
                "Резервная копия прежних аккаунтов редактируется, и откатная не должна проносить их токены мимо неё: гейт снимает право записи, и переписать её нечем до перезапуска.");

            Assert.That(File.Exists(TargetSettings("obs-chat.json") + ".bak"), Is.True,
                "У файлов без секретов откатная копия живёт как прежде – общий алгоритм не меняется.");
        }
    }

    [Test]
    public void Import_OntoCurrentBaseDirectory_MigratesInPlaceWithoutCopying()
    {
        LegacyDataFixture.FillSource(_target, false);

        var result = LegacyDataImporter.Import(_target, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsInPlaceMigration, Is.True);
            Assert.That(result.CopiedFiles, Is.Empty);
            Assert.That(result.SkippedFiles, Is.Empty);
            Assert.That(result.Failures, Is.Empty);

            Assert.That(File.ReadAllText(TargetSettings("accounts.json")), Is.EqualTo(LegacyDataFixture.Accounts));
            Assert.That(File.Exists(Path.Combine(_target, "accounts.json")), Is.False);
            Assert.That(Directory.GetFiles(_target, "accounts.legacy-*.json"), Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(_target, "users_statistics.json")), Is.EqualTo(LegacyDataFixture.UserStatistics));
            Assert.That(Directory.GetFiles(_target, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            Assert.That(result.UnmigratedLegacyFiles, Is.Empty);
        }
    }

    [TestCase(false, TestName = "Import_InPlace_LeftoverLegacyFile_IsReported")]
    [TestCase(true, TestName = "Import_InPlace_LeftoverLegacyFile_IsReportedWithOverwrite")]
    public void Import_InPlace_LegacyFileLeftInRoot_IsReported(bool overwrite)
    {
        LegacyDataFixture.Write(_target, "accounts.json", LegacyDataFixture.Accounts);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "accounts.json"), LegacyDataFixture.LocalAccounts);

        var result = LegacyDataImporter.Import(_target, _target, overwrite, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsInPlaceMigration, Is.True);

            Assert.That(result.UnmigratedLegacyFiles, Is.EqualTo(new[] { "accounts.json" }),
                "Мигратор оставляет legacy-файл в корне, когда в settings/ уже есть свой – пользователю об этом обязаны сказать");

            Assert.That(File.ReadAllText(TargetSettings("accounts.json")), Is.EqualTo(LegacyDataFixture.LocalAccounts));
            Assert.That(File.Exists(Path.Combine(_target, "accounts.json")), Is.True);
        }
    }

    [Test]
    public void Import_MonolithSplit_ReportsGeneratedFilesAsRewritten()
    {
        const string Monolith = """
                                {
                                  "twitch": {
                                    "channel": "bobito217",
                                    "botAccount": { "login": "thebot" },
                                    "broadcastProfiles": { "profiles": [] },
                                    "polls": { "historyMaxItems": 777 },
                                    "obsChat": { "fontSize": 24 },
                                    "infrastructure": { "recentCategories": ["Just Chatting"] }
                                  },
                                  "ui": { "dashboard": { "columnCount": 2 } }
                                }
                                """;

        LegacyDataFixture.Write(_source, Path.Combine("settings", "settings.json"), Monolith);

        var result = LegacyDataImporter.Import(_source, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CopiedFiles, Is.EquivalentTo(new[] { Path.Combine("settings", "settings.json") }),
                "Разбор порождает файлы из секций монолита, а не копирует их – в списке скопированного их нет и быть не может");

            Assert.That(result.RewrittenSettingsFiles, Is.EquivalentTo(new[]
            {
                "settings.json",
                "accounts.json",
                "broadcast-profiles.json",
                "polls.json",
                "recent-categories.json",
                "obs-chat.json",
                "dashboard-layout.json",
            }), "Этот список гейт получает как перечень переписанного мимо сторов: файл, выпавший из него, "
                + "первое же сохранение его владельца затрёт доимпортным состоянием из памяти");

            Assert.That(result.Failures, Is.Empty);
        }
    }

    [Test]
    public void Import_InPlace_ReportsRelocatedFilesAsRewritten()
    {
        LegacyDataFixture.Write(_target, "accounts.json", LegacyDataFixture.Accounts);

        var result = LegacyDataImporter.Import(_target, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsInPlaceMigration, Is.True);
            Assert.That(result.CopiedFiles, Is.Empty,
                "Перенос на месте не копирует ничего – файл только переезжает из корня в settings/");

            Assert.That(File.ReadAllText(TargetSettings("accounts.json")), Is.EqualTo(LegacyDataFixture.Accounts));

            Assert.That(result.RewrittenSettingsFiles, Is.EquivalentTo(new[] { "accounts.json" }),
                "До переезда settings/accounts.json не существовало, поэтому живой стор держит дефолты и первым же сохранением "
                + "положит их поверх перенесённого – право записи у него надо снять");
        }
    }

    [TestCase(false, TestName = "Import_MonolithSplit_KeepsExistingStore_WhenOverwriteIsOff")]
    [TestCase(true, TestName = "Import_MonolithSplit_ReplacesExistingStore_WhenOverwriteIsOn")]
    public void Import_MonolithSplit_RespectsOverwriteFlag(bool overwrite)
    {
        LegacyDataFixture.Write(_source, "settings.json", LegacyDataFixture.SettingsWithTokens);
        LegacyDataFixture.Write(_target, Path.Combine("settings", "accounts.json"), LegacyDataFixture.LocalAccounts);

        var result = LegacyDataImporter.Import(_source, _target, overwrite, null);
        var accounts = File.ReadAllText(TargetSettings("accounts.json"));
        var relativeAccounts = Path.Combine("settings", "accounts.json");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accounts.Contains("local-access", StringComparison.Ordinal), Is.EqualTo(!overwrite),
                "С выключенной перезаписью разбор перенесённого settings.json не имеет права заменять существующий accounts.json: его бэкап редактируется, и живые токены пропадут безвозвратно");

            Assert.That(accounts.Contains("thebot", StringComparison.Ordinal), Is.EqualTo(overwrite));

            Assert.That(result.SkippedFiles.Any(skip => skip.RelativeTargetPath == relativeAccounts), Is.EqualTo(!overwrite));
            Assert.That(result.CopiedFiles, Does.Contain(Path.Combine("settings", "settings.json")));
            Assert.That(result.Failures, Is.Empty);

            Assert.That(result.RewrittenSettingsFiles.Contains("accounts.json", StringComparer.OrdinalIgnoreCase), Is.EqualTo(overwrite),
                "Возвращённый в прежний вид файл совпадает с тем, что стор держит в памяти: отнимать у него запись не за что, "
                + "а переписанный разбором обязан остаться в списке");
        }
    }

    [TestCase("obs-chat.json", TestName = "Import_BackupFailure_KeepsObsChat")]
    [TestCase("accounts.json", TestName = "Import_BackupFailure_KeepsAccounts")]
    public void Import_Overwrite_BackupFailure_LeavesTargetIntact(string fileName)
    {
        const string LocalContent = """{"local":true}""";

        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_target, Path.Combine("settings", fileName), LocalContent);
        LegacyDataFixture.BlockBackupPaths(Path.Combine(_target, "settings"), fileName, "pre-import");

        var result = LegacyDataImporter.Import(_source, _target, true, null);
        var relativePath = Path.Combine("settings", fileName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(TargetSettings(fileName)), Is.EqualTo(LocalContent),
                "Резервная копия – единственное, что стоит между пользователем и потерей данных: не удалась она – отменяется и перезапись");

            Assert.That(result.Failures.Select(failure => failure.RelativeTargetPath), Does.Contain(relativePath));
            Assert.That(result.CopiedFiles, Does.Not.Contain(relativePath));
        }
    }

    [TestCase(true, 0, TestName = "CopyAll_EmptySource_ReportsNothing")]
    [TestCase(false, 1, TestName = "CopyAll_VanishedSource_ReportsFailure")]
    public void CopyAll_DistinguishesEmptySourceFromUnavailableOne(bool sourceExists, int expectedFailures)
    {
        var source = sourceExists ? _source : Path.Combine(_source, "исчезнувшая-папка");
        var copied = new List<string>();
        var skipped = new List<LegacyImportSkip>();
        var failures = new List<LegacyImportFailure>();

        LegacyDataImporter.CopyAll(source, _target, false, copied, skipped, failures, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failures, Has.Count.EqualTo(expectedFailures),
                "Пропавший во время переноса источник обязан отличаться от источника, в котором нечего переносить");

            Assert.That(copied, Is.Empty);
            Assert.That(skipped, Is.Empty);
        }
    }

    [Test]
    public void Import_SourceIsTargetSettingsDirectory_SkipsInsteadOfCopyingFileOntoItself()
    {
        var settingsDirectory = Path.Combine(_target, "settings");
        LegacyDataFixture.Write(_target, Path.Combine("settings", "accounts.json"), LegacyDataFixture.Accounts);

        var result = LegacyDataImporter.Import(settingsDirectory, _target, true, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(result.SkippedFiles.Select(skip => skip.Reason),
                Is.All.EqualTo(LegacyImportSkipReason.SameLocation));

            Assert.That(File.ReadAllText(TargetSettings("accounts.json")), Is.EqualTo(LegacyDataFixture.Accounts),
                "Копирование файла в самого себя обязано отбиваться до записи, иначе файл теряется");

            Assert.That(result.Failures, Is.Empty);
        }
    }

    [TestCase(true, TestName = "Import_LegacyTokensDropped_RequiresReauthorization")]
    [TestCase(false, TestName = "Import_AccountsTokensSurvive_NoReauthorization")]
    public void Import_ReportsReauthorizationOnlyWhenTokensAreLost(bool tokensOnlyInMonolith)
    {
        if (tokensOnlyInMonolith)
        {
            LegacyDataFixture.Write(_source, "settings.json", LegacyDataFixture.SettingsWithTokens);
        }
        else
        {
            LegacyDataFixture.Write(_source, Path.Combine("settings", "accounts.json"), LegacyDataFixture.Accounts);
        }

        var result = LegacyDataImporter.Import(_source, _target, false, null);
        var accounts = File.Exists(TargetSettings("accounts.json")) ? File.ReadAllText(TargetSettings("accounts.json")) : string.Empty;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.RequiresReauthorization, Is.EqualTo(tokensOnlyInMonolith));
            Assert.That(accounts, Does.Not.Contain("legacy-access"),
                "Токены из монолитного settings.json мигратор выбрасывает намеренно");

            Assert.That(accounts.Contains("secret-access", StringComparison.Ordinal), Is.EqualTo(!tokensOnlyInMonolith));
        }
    }

    [Test]
    public void Import_MissingSource_ReportsFailureWithoutThrowing()
    {
        var missing = Path.Combine(_source, "нет-такой-папки");

        var result = LegacyDataImporter.Import(missing, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.CopiedFiles, Is.Empty);
            Assert.That(result.RequiresReauthorization, Is.False);
        }
    }

    [Test]
    public void Import_NeverCopiesLogsBackupsAndTemporaryFiles()
    {
        LegacyDataFixture.FillSource(_source, true);
        LegacyDataFixture.Write(_source, Path.Combine("logs", "bot_log_20260101.txt"), "log");
        LegacyDataFixture.Write(_source, Path.Combine("update", "apply-update.json"), "{}");
        LegacyDataFixture.Write(_source, Path.Combine("WebView2", "state.dat"), "state");
        LegacyDataFixture.Write(_source, Path.Combine("settings", "accounts.json.bak"), LegacyDataFixture.Accounts);
        LegacyDataFixture.Write(_source, "users_statistics.legacy-20260101-000000.json", LegacyDataFixture.UserStatistics);
        LegacyDataFixture.Write(_source, "obs-chat.tmp", LegacyDataFixture.ObsChat);

        LegacyDataImporter.Import(_source, _target, false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.Exists(Path.Combine(_target, "logs")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_target, "update")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_target, "WebView2")), Is.False);
            Assert.That(File.Exists(Path.Combine(_target, "settings", "accounts.json.bak")), Is.False);
            Assert.That(File.Exists(Path.Combine(_target, "users_statistics.legacy-20260101-000000.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(_target, "obs-chat.tmp")), Is.False);
        }
    }
}
