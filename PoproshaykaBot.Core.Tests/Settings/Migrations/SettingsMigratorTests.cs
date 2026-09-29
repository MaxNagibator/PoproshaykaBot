using PoproshaykaBot.Core.Settings.Migrations;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Tests.Settings.Migrations;

[TestFixture]
public sealed class SettingsMigratorTests
{
    private static JsonObject Parse(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON is null");
        return node.AsObject();
    }

    [Test]
    public void Migrate_LegacySingleAccount_KeepsLoginAndDropsTokensWithScopes()
    {
        const string Legacy = """
                              {
                                "twitch": {
                                  "botUsername": "thevsakeeper",
                                  "channel": "bobito217",
                                  "accessToken": "access-1",
                                  "refreshToken": "refresh-1",
                                  "scopes": ["chat:read", "chat:edit"]
                                }
                              }
                              """;

        var root = Parse(Legacy);
        var changed = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);

            var twitch = root["twitch"]!.AsObject();
            Assert.That(twitch.ContainsKey("botUsername"), Is.False);
            Assert.That(twitch.ContainsKey("accessToken"), Is.False);
            Assert.That(twitch.ContainsKey("refreshToken"), Is.False);
            Assert.That(twitch.ContainsKey("scopes"), Is.False);
            Assert.That(twitch.ContainsKey("storedScopes"), Is.False);

            var bot = twitch["botAccount"]!.AsObject();
            Assert.That(bot["login"]!.GetValue<string>(), Is.EqualTo("thevsakeeper"));
            Assert.That(bot.ContainsKey("accessToken"), Is.False, "Старые токены сбрасываем – они выпущены под недостаточный набор прав");
            Assert.That(bot.ContainsKey("refreshToken"), Is.False, "Старые токены сбрасываем – они выпущены под недостаточный набор прав");
            Assert.That(bot.ContainsKey("storedScopes"), Is.False);
        }
    }

    [Test]
    public void Migrate_LegacySingleAccount_PreservesBotAccountInJson()
    {
        const string Legacy = """
                              {
                                "twitch": {
                                  "botUsername": "thevsakeeper",
                                  "accessToken": "access-1",
                                  "refreshToken": "refresh-1",
                                  "scopes": ["chat:read", "chat:edit"]
                                }
                              }
                              """;

        var root = Parse(Legacy);
        SettingsMigrator.Migrate(root);

        var bot = root["twitch"]!.AsObject()["botAccount"]!.AsObject();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bot["login"]!.GetValue<string>(), Is.EqualTo("thevsakeeper"));
            Assert.That(bot.ContainsKey("accessToken"), Is.False, "Токены сбрасываются, чтобы запустить чистый OAuth-flow под новый набор прав");
            Assert.That(bot.ContainsKey("refreshToken"), Is.False);
            Assert.That(bot.ContainsKey("storedScopes"), Is.False);
        }
    }

    [Test]
    public void Migrate_IntermediateWithStoredScopes_DropsTokensAndScopes()
    {
        const string Json = """
                            {
                              "twitch": {
                                "accessToken": "a",
                                "refreshToken": "r",
                                "scopes": ["user:read:chat", "user:write:chat", "user:bot", "channel:bot", "channel:manage:broadcast"],
                                "storedScopes": ["user:read:chat", "user:write:chat"]
                              }
                            }
                            """;

        var root = Parse(Json);
        var changed = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);

            var twitch = root["twitch"]!.AsObject();
            Assert.That(twitch.ContainsKey("accessToken"), Is.False);
            Assert.That(twitch.ContainsKey("refreshToken"), Is.False);
            Assert.That(twitch.ContainsKey("scopes"), Is.False);
            Assert.That(twitch.ContainsKey("storedScopes"), Is.False);
            Assert.That(twitch.ContainsKey("botAccount"), Is.False, "Без login и без токенов botAccount остаётся пустым – пусть его создаст десериализатор из дефолтов");
        }
    }

    [Test]
    public void Migrate_LegacyUiFields_AreRemoved()
    {
        const string Json = """
                            {
                              "ui": {
                                "showLogsPanel": true,
                                "showChatPanel": false,
                                "currentChatViewMode": 1
                              }
                            }
                            """;

        var root = Parse(Json);
        var changed = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);

            var ui = root["ui"]!.AsObject();
            Assert.That(ui.ContainsKey("showLogsPanel"), Is.False);
            Assert.That(ui.ContainsKey("showChatPanel"), Is.False);
            Assert.That(ui.ContainsKey("currentChatViewMode"), Is.False);
        }
    }

    [Test]
    public void Migrate_AlreadyCurrentSchema_ReturnsFalseAndDoesNotMutate()
    {
        const string Json = """
                            {
                              "twitch": {
                                "channel": "bobito217",
                                "botAccount": {
                                  "accessToken": "a",
                                  "refreshToken": "r",
                                  "login": "thevsakeeper",
                                  "scopes": ["user:read:chat"],
                                  "storedScopes": ["user:read:chat"]
                                },
                                "broadcasterAccount": {
                                  "accessToken": "ba",
                                  "refreshToken": "br"
                                }
                              },
                              "ui": { "dashboard": null }
                            }
                            """;

        var before = Parse(Json).ToJsonString();
        var root = JsonNode.Parse(before)!.AsObject();

        var changed = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.False, "Свежая схема не должна меняться повторным запуском мигратора");
            Assert.That(root.ToJsonString(), Is.EqualTo(before));
        }
    }

    [Test]
    public void Migrate_PartialState_DropsLegacyTopLevelTokenButKeepsBotAccountIntact()
    {
        const string Json = """
                            {
                              "twitch": {
                                "accessToken": "old-leftover",
                                "botAccount": {
                                  "accessToken": "current",
                                  "refreshToken": "current-r"
                                }
                              }
                            }
                            """;

        var root = Parse(Json);
        var changed = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);

            var twitch = root["twitch"]!.AsObject();
            Assert.That(twitch.ContainsKey("accessToken"), Is.False);

            var bot = twitch["botAccount"]!.AsObject();
            Assert.That(bot["accessToken"]!.GetValue<string>(), Is.EqualTo("current"), "Уже мигрированный botAccount.accessToken не трогаем");
            Assert.That(bot["refreshToken"]!.GetValue<string>(), Is.EqualTo("current-r"));
        }
    }

    [Test]
    public void Migrate_EmptyLegacyTokens_AreDiscardedSilently()
    {
        const string Json = """
                            {
                              "twitch": {
                                "botUsername": "",
                                "accessToken": "",
                                "refreshToken": "",
                                "scopes": []
                              }
                            }
                            """;

        var root = Parse(Json);
        var changed = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True, "Пустые legacy-поля всё равно должны исчезнуть из верхнего уровня");

            var twitch = root["twitch"]!.AsObject();
            Assert.That(twitch.ContainsKey("botUsername"), Is.False);
            Assert.That(twitch.ContainsKey("accessToken"), Is.False);
            Assert.That(twitch.ContainsKey("refreshToken"), Is.False);
            Assert.That(twitch.ContainsKey("scopes"), Is.False);
            Assert.That(twitch.ContainsKey("botAccount"), Is.False, "Создание botAccount только ради пустых полей бессмысленно");
        }
    }

    [Test]
    public void Migrate_NoTwitchSection_DoesNotCrash()
    {
        var root = Parse("{}");
        var changed = SettingsMigrator.Migrate(root).Changed;

        Assert.That(changed, Is.False);
    }

    [Test]
    public void Migrate_TwiceOnLegacyJson_IsIdempotent()
    {
        const string Legacy = """
                              {
                                "twitch": {
                                  "botUsername": "thevsakeeper",
                                  "accessToken": "a",
                                  "refreshToken": "r",
                                  "scopes": ["chat:read"]
                                }
                              }
                              """;

        var root = Parse(Legacy);
        var firstChanged = SettingsMigrator.Migrate(root).Changed;
        var snapshot = root.ToJsonString();
        var secondChanged = SettingsMigrator.Migrate(root).Changed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstChanged, Is.True);
            Assert.That(secondChanged, Is.False);
            Assert.That(root.ToJsonString(), Is.EqualTo(snapshot));
        }
    }
}
