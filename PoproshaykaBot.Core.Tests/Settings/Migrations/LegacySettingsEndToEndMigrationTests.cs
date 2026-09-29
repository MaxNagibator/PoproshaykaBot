using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Migrations;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Chat;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Tests.Settings.Migrations;

[TestFixture]
public sealed class LegacySettingsEndToEndMigrationTests
{
    private const string FixtureResourceName =
        "PoproshaykaBot.Core.Tests.Settings.Migrations.Fixtures.LegacyMonolithicSettings.json";

    private static string LoadFixtureJson()
    {
        var assembly = typeof(LegacySettingsEndToEndMigrationTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(FixtureResourceName)
                           ?? throw new InvalidOperationException($"Embedded resource {FixtureResourceName} not found. "
                                                                  + "Проверь <EmbeddedResource> в PoproshaykaBot.Core.Tests.csproj.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static JsonObject LoadFixtureRoot()
    {
        return JsonNode.Parse(LoadFixtureJson()) as JsonObject
               ?? throw new InvalidOperationException("Корневой элемент fixture не является JSON-объектом");
    }

    [Test]
    public void EmbeddedFixture_IsAccessible()
    {
        var assembly = typeof(LegacySettingsEndToEndMigrationTests).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        Assert.That(resourceNames, Does.Contain(FixtureResourceName),
            "Fixture должен быть упакован как EmbeddedResource – иначе тесты молча отвалятся при перемещении файла.");
    }

    [Test]
    public void Fixture_DeserializesIntoCurrentAppSettings_PreservingDomainValues()
    {
        var root = LoadFixtureRoot();

        SettingsMigrator.Migrate(root);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var settings = root.Deserialize<AppSettings>(options);

        Assert.That(settings, Is.Not.Null, "Мигрированный JSON должен валидно десериализоваться в AppSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings!.Twitch.Channel, Is.EqualTo("bobito217"));
            Assert.That(settings.Twitch.ClientId, Is.EqualTo("legacy-client-id"));
            Assert.That(settings.Twitch.RedirectUri, Is.EqualTo("http://localhost:8080"));
            Assert.That(settings.Twitch.Messages.Welcome, Does.Contain("legacy"),
                "Кастомные сообщения должны переживать миграцию без перезаписи дефолтами");

            Assert.That(settings.Ranks.Ranks, Has.Count.EqualTo(1));
            Assert.That(settings.Ranks.Ranks[0].Name, Is.EqualTo("ТЕСТ"));

            var obsChat = root["twitch"]?["obsChat"] as JsonObject;
            Assert.That(obsChat?["maxMessages"]?.GetValue<int>(), Is.EqualTo(25));

            var botAccount = root["twitch"]?["botAccount"] as JsonObject;
            Assert.That(botAccount?["login"]?.GetValue<string>(), Is.EqualTo("thebot_legacy"),
                "botAccount.login обязан быть взят из legacy twitch.botUsername");
        }
    }

    [Test]
    public void Fixture_RoundTripsThroughAtomicFile_AndSecondMigrationIsNoop()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "poproshayka-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempSettings = Path.Combine(tempDir, "settings.json");

        try
        {
            File.WriteAllText(tempSettings, LoadFixtureJson(), Encoding.UTF8);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            var root = JsonNode.Parse(File.ReadAllText(tempSettings, Encoding.UTF8))!.AsObject();

            var firstChanged = SettingsMigrator.Migrate(root, NullLogger.Instance).Changed;
            Assert.That(firstChanged, Is.True, "Legacy-fixture должен мигрироваться при первом запуске");

            AtomicFile.Save(tempSettings, root.ToJsonString(options), NullLogger.Instance);

            var diskJson = File.ReadAllText(tempSettings, Encoding.UTF8);
            var rootRoundTrip = JsonNode.Parse(diskJson)!.AsObject();
            var secondChanged = SettingsMigrator.Migrate(rootRoundTrip, NullLogger.Instance).Changed;
            var settings = JsonSerializer.Deserialize<AppSettings>(diskJson, options);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings, Is.Not.Null,
                    "JSON, прошедший через AtomicFile.Save, должен валидно десериализоваться в AppSettings");

                Assert.That(secondChanged, Is.False,
                    "Повторная миграция уже мигрированного и сохранённого через AtomicFile файла не должна ничего менять");

                Assert.That(File.Exists(tempSettings + ".tmp"), Is.False,
                    "После успешного File.Replace .tmp не должен оставаться");

                Assert.That(File.Exists(tempSettings + ".old"), Is.False,
                    ".old удаляется TryDelete после успешного File.Replace");

                var botAccountNode = rootRoundTrip["twitch"]?["botAccount"] as JsonObject;
                if (botAccountNode != null)
                {
                    Assert.That(botAccountNode.ContainsKey("accessToken"), Is.False);
                    Assert.That(botAccountNode.ContainsKey("refreshToken"), Is.False);
                    Assert.That(botAccountNode.ContainsKey("storedScopes"), Is.False);
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
            }
        }
    }

    [Test]
    public void Fixture_FullSplitPipeline_BothAccountsEndUpWithRequiredScopes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "poproshayka-migration-scopes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempSettings = Path.Combine(tempDir, "settings.json");

        try
        {
            File.WriteAllText(tempSettings, LoadFixtureJson(), Encoding.UTF8);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            var root = JsonNode.Parse(File.ReadAllText(tempSettings, Encoding.UTF8))!.AsObject();

            var migration = SettingsMigrator.Migrate(root, NullLogger.Instance, tempDir);
            AtomicFile.Save(tempSettings, root.ToJsonString(options), NullLogger.Instance);

            Assert.That(migration.SplitFiles, Does.Contain("accounts.json"),
                "Разбор монолита обязан доложить о каждом файле, который он записал мимо стора – по этому списку у сторов снимается право записи");

            var accountsStore = new AccountsStore(filePath: Path.Combine(tempDir, "accounts.json"));
            var bot = accountsStore.LoadBot();
            var broadcaster = accountsStore.LoadBroadcaster();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(bot.Scopes, Is.EquivalentTo(TwitchScopes.BotRequired),
                    "После полного pipeline BotAccount.Scopes должен содержать актуальный целевой набор для повторной авторизации");

                Assert.That(broadcaster.Scopes, Is.EquivalentTo(TwitchScopes.BroadcasterRequired),
                    "После полного pipeline BroadcasterAccount.Scopes должен содержать актуальный целевой набор");

                Assert.That(bot.AccessToken, Is.Empty);
                Assert.That(bot.StoredScopes, Is.Empty);
                Assert.That(broadcaster.AccessToken, Is.Empty);
                Assert.That(broadcaster.StoredScopes, Is.Empty);

                Assert.That(TwitchScopes.SetEquals(bot.Scopes, bot.StoredScopes), Is.False,
                    "Целевые Scopes и пустые StoredScopes должны различаться – иначе после получения токена TwitchOAuthService не зафиксирует scope-mismatch");
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
            }
        }
    }
}
