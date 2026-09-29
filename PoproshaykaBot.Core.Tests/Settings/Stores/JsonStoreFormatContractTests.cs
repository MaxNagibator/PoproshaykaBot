using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Twitch.Auth;
using System.Drawing;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Settings.Stores;

[TestFixture]
public sealed class JsonStoreFormatContractTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("json-store-format");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private const string AstralEscape = "\\uD83D\\uDCCA";

    private DirectoryInfo _directory = null!;

    [Test]
    public void UpdateStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "update.json");
        var store = new UpdateStore(NullLogger<UpdateStore>.Instance, path);

        var settings = new UpdateSettings
        {
            AutoCheckEnabled = false,
            AllowFrameworkDependentUpdate = true,
            RepositoryOverride = "owner/repo",
            CheckIntervalHours = 12,
            SkippedVersion = "1.2.3",
            LastCheckUtc = new(2026, 9, 2, 18, 46, 3, TimeSpan.Zero),
            ApplyMode = UpdateApplyMode.SilentOnExit,
        };

        store.Save(settings);

        AssertFileMatches(path,
            """
            {
              "autoCheckEnabled": false,
              "allowFrameworkDependentUpdate": true,
              "repositoryOverride": "owner/repo",
              "checkIntervalHours": 12,
              "skippedVersion": "1.2.3",
              "lastCheckUtc": "2026-09-02T18:46:03+00:00",
              "applyMode": "SilentOnExit"
            }
            """);

        Assert.That(new UpdateStore(NullLogger<UpdateStore>.Instance, path).Load().ApplyMode,
            Is.EqualTo(UpdateApplyMode.SilentOnExit));
    }

    [Test]
    public void PollsStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "polls.json");
        var store = new PollsStore(NullLogger<PollsStore>.Instance, path);

        store.Save(BuildPolls());

        AssertFileMatches(path,
            """
            {
              "profiles": [
                {
                  "id": "11111111-2222-3333-4444-555555555555",
                  "name": "Утренний опрос",
                  "title": "Что смотрим?",
                  "choices": [
                    "Кино",
                    "Игры"
                  ],
                  "durationSeconds": 90,
                  "channelPointsVotingEnabled": true,
                  "channelPointsPerVote": 250,
                  "autoTrigger": {
                    "event": "None",
                    "broadcastProfileId": null,
                    "cooldownMinutes": 0
                  }
                }
              ],
              "chatTemplates": {
                "startEnabled": true,
                "startTemplate": "{astral} Старт «{title}»",
                "progressEnabled": false,
                "progressTemplate": "Лидер: {leader}",
                "endEnabled": true,
                "endTemplate": "Финиш – {winner}",
                "terminatedEnabled": false,
                "terminatedTemplate": "⛔ Стоп",
                "archivedEnabled": true,
                "archivedTemplate": "Архив",
                "progressAnnounceIntervalSeconds": 30
              },
              "autoTriggerKillSwitchDateUtc": "2026-09-02T00:00:00Z",
              "historyMaxItems": 250
            }
            """);

        var reloaded = new PollsStore(NullLogger<PollsStore>.Instance, path).Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Profiles, Has.Count.EqualTo(1));
            Assert.That(reloaded.ChatTemplates.StartTemplate, Is.EqualTo("📊 Старт «{title}»"));
            Assert.That(reloaded.HistoryMaxItems, Is.EqualTo(250));
        }
    }

    [Test]
    public void BroadcastProfilesStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "broadcast-profiles.json");
        var store = new BroadcastProfilesStore(NullLogger<BroadcastProfilesStore>.Instance, path);

        store.Save(BuildProfiles());

        AssertFileMatches(path,
            """
            {
              "profiles": [
                {
                  "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                  "name": "Стрим «Бег»",
                  "title": "Забег #12",
                  "gameId": "509658",
                  "gameName": "Just Chatting",
                  "broadcasterLanguage": "ru",
                  "tags": [
                    "ru",
                    "бег"
                  ],
                  "obsSceneName": "Игра",
                  "currentNumber": 12,
                  "lastApplyAt": "2026-08-31T10:00:00+03:00",
                  "lastAutoAdvanceAt": null
                }
              ],
              "lastAppliedProfileId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
            }
            """);

        var reloaded = new BroadcastProfilesStore(NullLogger<BroadcastProfilesStore>.Instance, path).Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Profiles.Single().LastApplyAt,
                Is.EqualTo(new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.FromHours(3))));

            Assert.That(reloaded.LastAppliedProfileId,
                Is.EqualTo(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
        }
    }

    [Test]
    public void ObsChatStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "obs-chat.json");
        var store = new ObsChatStore(Substitute.For<IEventBus>(), NullLogger<ObsChatStore>.Instance, path);

        store.Save(new());

        AssertFileMatches(path,
            """
            {
              "backgroundColor": {
                "a": 179,
                "r": 0,
                "g": 0,
                "b": 0
              },
              "textColor": {
                "a": 255,
                "r": 255,
                "g": 255,
                "b": 255
              },
              "usernameColor": {
                "a": 255,
                "r": 145,
                "g": 70,
                "b": 255
              },
              "systemMessageColor": {
                "a": 255,
                "r": 255,
                "g": 204,
                "b": 0
              },
              "timestampColor": {
                "a": 255,
                "r": 153,
                "g": 153,
                "b": 153
              },
              "fontFamily": "\"Motiva Sans\", \"Inter\", \"Noto Sans\", Arial, sans-serif",
              "fontSize": 14,
              "fontBold": false,
              "padding": 5,
              "margin": 5,
              "borderRadius": 5,
              "animationDuration": 300,
              "enableAnimations": true,
              "maxMessages": 50,
              "showTimestamp": true,
              "emoteSizePixels": 28,
              "badgeSizePixels": 18,
              "showUserAvatars": false,
              "userAvatarSizePixels": 32,
              "showMessageImages": false,
              "messageImageRoles": "Broadcaster, Moderator, Vip",
              "messageImageAllowedHosts": [
                "i.imgur.com",
                "imgur.com",
                "static-cdn.jtvnw.net",
                "clips-media-assets2.twitch.tv",
                "cdn.7tv.app",
                "media.discordapp.net"
              ],
              "messageImageMaxHeightPixels": 160,
              "showUserTypeBorders": true,
              "highlightFirstTimeUsers": true,
              "highlightMentions": true,
              "enableMessageShadows": true,
              "enableSpecialEffects": true,
              "enableSmoothScroll": true,
              "scrollAnimationDuration": 300,
              "autoScrollEnabled": true,
              "scrollToBottomThreshold": 100,
              "scrollPauseAfterUserMs": 3000,
              "userMessageAnimation": "slide-in-right",
              "botMessageAnimation": "fade-in-up",
              "systemMessageAnimation": "fade-in-up",
              "broadcasterMessageAnimation": "slide-in-left",
              "firstTimeUserMessageAnimation": "bounce-in",
              "enableMessageFadeOut": true,
              "messageLifetimeSeconds": 30,
              "fadeOutAnimationType": "fade-out",
              "fadeOutAnimationDurationMs": 1000
            }
            """);

        Assert.That(new ObsChatStore(Substitute.For<IEventBus>(), NullLogger<ObsChatStore>.Instance, path)
                .Load()
                .BackgroundColor,
            Is.EqualTo(Color.FromArgb(179, 0, 0, 0)));
    }

    [Test]
    public void ObsIntegrationStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "obs-integration.json");
        var store = new ObsIntegrationStore(Substitute.For<IEventBus>(), NullLogger<ObsIntegrationStore>.Instance, path);

        store.Save(new());

        AssertFileMatches(path,
            """
            {
              "enabled": false,
              "autoConnect": true,
              "autoProvisionBrowserSource": false,
              "applySceneOnProfile": false,
              "applyProfileOnScene": false,
              "host": "127.0.0.1",
              "port": 4455,
              "password": "",
              "sceneName": "",
              "dashboardMicrophoneName": "",
              "dashboardSourceNames": [],
              "dashboardVolumeMeterDelayMs": 120,
              "sourceName": "PoproshaykaBot Chat",
              "width": 1920,
              "height": 1080,
              "chatRefreshSources": [],
              "refreshChatSourcesOnStreamStart": false
            }
            """);
    }

    [Test]
    public void AccountsStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "accounts.json");
        var store = new AccountsStore(NullLogger<AccountsStore>.Instance, path);

        store.Mutate(TwitchOAuthRole.Bot, account =>
        {
            account.AccessToken = "bot-access";
            account.RefreshToken = "bot-refresh";
            account.Login = "thebot";
            account.UserId = "1234";
            account.AccessTokenExpiresAt = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        });

        AssertFileMatches(path,
            """
            {
              "botAccount": {
                "accessToken": "bot-access",
                "refreshToken": "bot-refresh",
                "login": "thebot",
                "userId": "1234",
                "scopes": [
                  "user:read:chat",
                  "user:write:chat",
                  "user:bot"
                ],
                "storedScopes": [],
                "accessTokenExpiresAt": "2026-09-03T10:00:00+00:00"
              },
              "broadcasterAccount": {
                "accessToken": "",
                "refreshToken": "",
                "login": "",
                "userId": "",
                "scopes": [
                  "channel:bot",
                  "channel:manage:broadcast",
                  "channel:manage:polls",
                  "channel:read:polls"
                ],
                "storedScopes": [],
                "accessTokenExpiresAt": null
              }
            }
            """);
    }

    [Test]
    public void RecentCategoriesStore_WritesTheFormatFixedBeforeTheSharedLayer()
    {
        var path = Path.Combine(_directory.FullName, "recent-categories.json");

        File.WriteAllText(path,
            """{"items":[{"id":"509658","name":"Just Chatting","lastUsedAt":"2026-09-03T10:00:00+00:00"}]}""");

        new RecentCategoriesStore(NullLogger<RecentCategoriesStore>.Instance, path).Save();

        AssertFileMatches(path,
            """
            {
              "items": [
                {
                  "id": "509658",
                  "name": "Just Chatting",
                  "lastUsedAt": "2026-09-03T10:00:00+00:00"
                }
              ]
            }
            """);
    }

    [TestCase("""{"items":null}""")]
    [TestCase("{}")]
    public void RecentCategoriesStore_FileWithoutAUsableList_StartsEmptyInsteadOfThrowing(string content)
    {
        var path = Path.Combine(_directory.FullName, "recent-categories.json");
        File.WriteAllText(path, content);

        var store = new RecentCategoriesStore(NullLogger<RecentCategoriesStore>.Instance, path);
        store.Remember(new("509658", "Just Chatting", "https://art"));

        Assert.That(store.Load().Select(entry => entry.Id), Is.EqualTo(new[] { "509658" }),
            "Список, отданный как null, обязан читаться пустым: это разобранный JSON, битым файлом он не считается и в бэкап не уходит.");
    }

    [Test]
    public void MutateWritesTheSameBytesAsSave()
    {
        var savedPath = Path.Combine(_directory.FullName, "saved.json");
        var mutatedPath = Path.Combine(_directory.FullName, "mutated.json");

        new PollsStore(NullLogger<PollsStore>.Instance, savedPath).Save(BuildPolls());

        new PollsStore(NullLogger<PollsStore>.Instance, mutatedPath).Mutate(polls =>
        {
            var source = BuildPolls();
            polls.Profiles.AddRange(source.Profiles);
            polls.ChatTemplates = source.ChatTemplates;
            polls.AutoTriggerKillSwitchDateUtc = source.AutoTriggerKillSwitchDateUtc;
            polls.HistoryMaxItems = source.HistoryMaxItems;
        });

        Assert.That(File.ReadAllBytes(mutatedPath), Is.EqualTo(File.ReadAllBytes(savedPath)),
            "Мутация и полная замена обязаны давать один и тот же файл, включая BOM и переводы строк.");
    }

    private static PollsSettings BuildPolls()
    {
        return new()
        {
            Profiles =
            [
                new()
                {
                    Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    Name = "Утренний опрос",
                    Title = "Что смотрим?",
                    Choices = ["Кино", "Игры"],
                    DurationSeconds = 90,
                    ChannelPointsVotingEnabled = true,
                    ChannelPointsPerVote = 250,
                },
            ],
            ChatTemplates = new()
            {
                StartEnabled = true,
                StartTemplate = "📊 Старт «{title}»",
                ProgressEnabled = false,
                ProgressTemplate = "Лидер: {leader}",
                EndEnabled = true,
                EndTemplate = "Финиш – {winner}",
                TerminatedEnabled = false,
                TerminatedTemplate = "⛔ Стоп",
                ArchivedEnabled = true,
                ArchivedTemplate = "Архив",
                ProgressAnnounceIntervalSeconds = 30,
            },
            AutoTriggerKillSwitchDateUtc = new(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
            HistoryMaxItems = 250,
        };
    }

    private static BroadcastProfilesSettings BuildProfiles()
    {
        return new()
        {
            Profiles =
            [
                new()
                {
                    Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    Name = "Стрим «Бег»",
                    Title = "Забег #12",
                    GameId = "509658",
                    GameName = "Just Chatting",
                    BroadcasterLanguage = "ru",
                    Tags = ["ru", "бег"],
                    ObsSceneName = "Игра",
                    CurrentNumber = 12,
                    LastApplyAt = new(2026, 8, 31, 10, 0, 0, TimeSpan.FromHours(3)),
                    LastAutoAdvanceAt = null,
                },
            ],
            LastAppliedProfileId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        };
    }

    private static void AssertFileMatches(string path, string expected)
    {
        var golden = expected.Replace("{astral}", AstralEscape, StringComparison.Ordinal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(path).ReplaceLineEndings("\n"),
                Is.EqualTo(golden.ReplaceLineEndings("\n")),
                $"Формат {Path.GetFileName(path)} зафиксирован до переезда на общий слой и меняться не должен.");

            Assert.That(File.ReadAllBytes(path).Take(3), Is.EqualTo(Encoding.UTF8.GetPreamble()),
                $"{Path.GetFileName(path)} пишется с BOM: без него старые версии приложения прочитают файл иначе.");
        }
    }
}
