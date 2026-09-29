using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class SettingsManagerCorruptFileTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("settings-manager-corrupt");
        _filePath = Path.Combine(_directory.FullName, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private DirectoryInfo _directory = null!;
    private string _filePath = null!;

    private const string LegacyWithTokens = """
        {
          "twitch": {
            "channel": "bobito217",
            "botAccount": {
              "login": "thebot",
              "accessToken": "bot-secret-access",
              "refreshToken": "bot-secret-refresh"
            }
          }
        }
        """;

    [TestCase("{ \"twitch\": { \"clientId\": \"leaked-client-id\"", TestName = "Current_TruncatedJson_StartsOnDefaultsAndSetsTheFileAside")]
    [TestCase("[ \"leaked-client-id\" ]", TestName = "Current_ArrayRoot_StartsOnDefaultsAndSetsTheFileAside")]
    [TestCase("\"leaked-client-id\"", TestName = "Current_StringRoot_StartsOnDefaultsAndSetsTheFileAside")]
    [TestCase("не json вовсе, leaked-client-id", TestName = "Current_Garbage_StartsOnDefaultsAndSetsTheFileAside")]
    public void Current_FileThatFailsToLoad_StartsOnDefaultsAndSetsTheFileAside(string corrupt)
    {
        File.WriteAllText(_filePath, corrupt);

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, _filePath).Current;
        var backups = Directory.GetFiles(_directory.FullName, "settings.invalid-*.json");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Twitch.ClientId, Is.Empty);
            Assert.That(settings.Twitch.Channel, Is.EqualTo(new TwitchSettings().Channel));
            Assert.That(settings.Ranks.Ranks, Is.Not.Empty);

            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(backups[0]), Is.EqualTo(corrupt),
                "Бэкап settings.json снимается копией файла: содержимое обязано дойти до пользователя один в один.");

            Assert.That(File.ReadAllText(_filePath), Is.EqualTo(corrupt),
                "Битый оригинал остаётся на месте до первой записи – перезаписывает его только сохранение настроек.");
        }
    }

    [Test]
    public void Current_LegacySettingsWithTokens_LeavesNoTokensInBackups()
    {
        File.WriteAllText(_filePath, LegacyWithTokens);

        _ = new SettingsManager(NullLogger<SettingsManager>.Instance, _filePath).Current;

        var backups = Directory.GetFiles(_directory.FullName, "settings.pre-migration-*.json");
        var backup = backups.Length == 1 ? File.ReadAllText(backups[0]) : string.Empty;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(backup, Does.Not.Contain("bot-secret-access"),
                "Бэкап монолита снимается через редактор токенов, как у того же сплиттера из LegacySettingsLayoutMigrator.");

            Assert.That(File.Exists(_filePath + ".bak"), Is.False,
                "Откатная копия монолита с токенами не переживает удавшуюся запись.");

            Assert.That(File.ReadAllText(_filePath), Does.Not.Contain("bot-secret-access"),
                "Сам settings.json после разбора токенов не хранит – они уезжают в accounts.json.");

            Assert.That(File.ReadAllText(Path.Combine(_directory.FullName, "accounts.json")), Does.Contain("bot-secret-access"));
        }
    }
}
