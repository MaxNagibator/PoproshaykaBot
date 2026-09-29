using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Debugging;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class SettingsDescriberTests
{
    private static readonly string[] _sentinels = ["SECRET-1", "SECRET-2", "SECRET-3", "SECRET-4"];

    [TestCaseSource(nameof(GetDescriptionCases))]
    public void Describe_KeepsSecretsOut(DescriptionCase testCase)
    {
        Assert.That(testCase.Description, Does.Not.Contain("SECRET"));

        foreach (var sentinel in _sentinels)
        {
            Assert.That(testCase.Description, Does.Not.Contain(sentinel));
        }
    }

    [TestCaseSource(nameof(GetDescriptionCases))]
    public void Describe_CarriesKeyValues(DescriptionCase testCase)
    {
        foreach (var expected in testCase.Expected)
        {
            Assert.That(testCase.Description, Does.Contain(expected));
        }
    }

    private static IEnumerable<DescriptionCase> GetDescriptionCases()
    {
        yield return new("AppSettings",
            SettingsDescriber.Describe(CreateAppSettings()),
            ["testchannel", "clientId есть", "clientSecret есть", "порт 9099", "лимит 111", "приветствие вкл", "прощание выкл", "каждые 42 мин", "автоподключение на онлайн вкл", "пользователей 2", "рангов 3"]);

        yield return new("TwitchAccountSettings",
            SettingsDescriber.Describe(CreateAccount()),
            ["логин botlogin", "userId 4242", "access-токен есть", "refresh-токен есть", "скоупов 2"]);

        yield return new("ObsIntegrationSettings",
            SettingsDescriber.Describe(CreateObsIntegrationSettings()),
            ["интеграция вкл", "obs-host:4466", "пароль есть", "сцена MainScene", "источников дашборда 2"]);

        yield return new("ObsChatSettings",
            SettingsDescriber.Describe(new ObsChatSettings { MaxMessages = 77, ShowTimestamp = false, MessageLifetimeSeconds = 88 }),
            ["сообщений на экране 77", "время выкл", "через 88 с"]);

        yield return new("UpdateSettings",
            SettingsDescriber.Describe(new UpdateSettings
            {
                AutoCheckEnabled = false,
                CheckIntervalHours = 13,
                RepositoryOverride = "owner/fork",
                SkippedVersion = "9.9.9",
                AllowFrameworkDependentUpdate = true,
            }),
            ["автопроверка выкл", "каждые 13 ч", "owner/fork", "9.9.9", "framework-dependent вкл"]);

        yield return new("DebugChannelSettings",
            SettingsDescriber.Describe(new DebugChannelSettings { IsEnabled = true, Channel = "mrbeast", AllowSending = true }),
            ["отладочный канал вкл", "mrbeast", "отправка сообщений вкл"]);

        yield return new("BroadcastProfilesSettings",
            SettingsDescriber.Describe(new BroadcastProfilesSettings { Profiles = [new(), new(), new()] }),
            ["профилей 3"]);

        yield return new("PollsSettings",
            SettingsDescriber.Describe(new PollsSettings
            {
                ChatTemplates = new() { StartEnabled = true, ProgressEnabled = false, ProgressAnnounceIntervalSeconds = 45 },
                HistoryMaxItems = 33,
            }),
            ["объявление старта вкл", "прогресс выкл каждые 45 с", "история до 33"]);
    }

    private static AppSettings CreateAppSettings()
    {
        var settings = new AppSettings();
        settings.Twitch.Channel = "testchannel";
        settings.Twitch.ClientId = "client-id-value";
        settings.Twitch.ClientSecret = "SECRET-1";
        settings.Twitch.HttpServerPort = 9099;
        settings.Twitch.RedirectUri = "http://localhost:9099";
        settings.Twitch.MessagesAllowedInPeriod = 111;
        settings.Twitch.ThrottlingPeriodSeconds = 22;
        settings.Twitch.Messages.WelcomeEnabled = true;
        settings.Twitch.Messages.FarewellEnabled = false;
        settings.Twitch.AutoBroadcast.AutoBroadcastEnabled = true;
        settings.Twitch.AutoBroadcast.BroadcastIntervalMinutes = 42;
        settings.Twitch.BotLifecycleAutomation.AutoConnectOnStreamOnline = true;
        settings.SpecialCommands.AllowedUsers = ["one", "two"];
        settings.Ranks.Ranks = [new("A", "A", 300), new("B", "B", 200), new("C", "C", 100)];

        return settings;
    }

    private static TwitchAccountSettings CreateAccount()
    {
        return new()
        {
            Login = "botlogin",
            UserId = "4242",
            AccessToken = "SECRET-3",
            RefreshToken = "SECRET-4",
            Scopes = ["chat:read", "chat:edit"],
            AccessTokenExpiresAt = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
        };
    }

    private static ObsIntegrationSettings CreateObsIntegrationSettings()
    {
        return new()
        {
            Enabled = true,
            Host = "obs-host",
            Port = 4466,
            Password = "SECRET-2",
            SceneName = "MainScene",
            DashboardSourceNames = ["mic", "desktop"],
        };
    }

    public sealed record DescriptionCase(string Name, string Description, IReadOnlyList<string> Expected)
    {
        public override string ToString()
        {
            return Name;
        }
    }
}
