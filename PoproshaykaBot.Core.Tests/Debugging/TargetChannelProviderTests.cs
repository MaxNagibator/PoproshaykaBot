using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Debugging;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Tests.Debugging;

[TestFixture]
public class TargetChannelProviderTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot.DebugChannelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _settings = new()
        {
            Twitch =
            {
                Channel = OwnChannel,
            },
        };

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(_settings);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private const string OwnChannel = "bobito217";

    private string _tempDirectory = null!;
    private AppSettings _settings = null!;
    private SettingsManager _settingsManager = null!;

    [Test]
    public void Current_WithoutDebugSession_UsesOwnChannelAndAllowsSending()
    {
        var provider = CreateProvider(new(), DebugChannelOverride.None);

        var state = provider.Current;

        Assert.Multiple(() =>
        {
            Assert.That(state.Login, Is.EqualTo(OwnChannel));
            Assert.That(state.IsDebugSession, Is.False);
            Assert.That(state.IsForeign, Is.False);
            Assert.That(state.IsSendingAllowed, Is.True);
        });
    }

    [Test]
    public void Current_WithCommandLineOverride_WinsOverStoredSession()
    {
        var stored = new DebugChannelSettings
        {
            IsEnabled = true,
            Channel = "fromsettings",
            AllowSending = true,
        };

        var provider = CreateProvider(stored, DebugChannelOverride.Parse(["--debug-channel", "https://twitch.tv/FromCommandLine"]));

        var state = provider.Current;

        Assert.Multiple(() =>
        {
            Assert.That(state.Login, Is.EqualTo("fromcommandline"));
            Assert.That(state.IsForeign, Is.True);
            Assert.That(state.IsSendingAllowed, Is.False,
                "ключ --debug-channel без --allow-send означает наблюдение, и разрешение из файла настроек его не снимает");
        });
    }

    [Test]
    public void Current_WithStoredSession_TakesChannelAndReadOnlyMode()
    {
        var stored = new DebugChannelSettings
        {
            IsEnabled = true,
            Channel = "FromSettings",
        };

        var provider = CreateProvider(stored, DebugChannelOverride.None);

        var state = provider.Current;

        Assert.Multiple(() =>
        {
            Assert.That(state.Login, Is.EqualTo("fromsettings"));
            Assert.That(state.OwnChannel, Is.EqualTo(OwnChannel));
            Assert.That(state.IsForeign, Is.True);
            Assert.That(state.IsSendingAllowed, Is.False);
        });
    }

    [Test]
    public void Current_WithStoredSessionOnOwnChannel_IsNotForeignButStaysReadOnly()
    {
        var stored = new DebugChannelSettings
        {
            IsEnabled = true,
            Channel = OwnChannel,
        };

        var provider = CreateProvider(stored, DebugChannelOverride.None);

        var state = provider.Current;

        Assert.Multiple(() =>
        {
            Assert.That(state.IsDebugSession, Is.True);
            Assert.That(state.IsForeign, Is.False, "права на свой канал есть, поэтому голосования и правка канала остаются доступны");
            Assert.That(state.IsSendingAllowed, Is.False);
        });
    }

    [Test]
    public void Current_WithInvalidStoredChannel_StaysOnOwnChannelAndKeepsSilent()
    {
        var stored = new DebugChannelSettings
        {
            IsEnabled = true,
            Channel = "не канал",
            AllowSending = true,
        };

        var provider = CreateProvider(stored, DebugChannelOverride.None);

        var state = provider.Current;

        Assert.Multiple(() =>
        {
            Assert.That(state.Login, Is.EqualTo(OwnChannel));
            Assert.That(state.IsDebugSession, Is.True, "отладка включена, поэтому пользователь ждёт режим наблюдателя, а не обычную работу");
            Assert.That(state.IsSendingAllowed, Is.False, "канал не разобран – молчание безопаснее, чем рассылка в свой живой чат");
        });
    }

    [Test]
    public void Current_WithinSession_KeepsTheChannelTheBotConnectedWith()
    {
        var store = new DebugChannelStore(null, Path.Combine(_tempDirectory, "debug-channel.json"));

        store.Save(new()
        {
            IsEnabled = true,
            Channel = "fromsettings",
        });

        var provider = new TargetChannelProvider(_settingsManager, store, DebugChannelOverride.None, null);
        provider.BeginSession();

        store.Save(new());

        Assert.Multiple(() =>
        {
            Assert.That(provider.Current.Login, Is.EqualTo("fromsettings"),
                "чтение чата привязано к каналу на всю EventSub-сессию, поэтому отправка и статистика не должны уехать на другой канал до переподключения");

            Assert.That(provider.Current.IsSendingAllowed, Is.False);
        });

        provider.EndSession();

        Assert.That(provider.Current.Login, Is.EqualTo(OwnChannel));
    }

    [Test]
    public void Current_WithAllowSendArgumentOnly_LiftsReadOnlyOfStoredSession()
    {
        var stored = new DebugChannelSettings
        {
            IsEnabled = true,
            Channel = "fromsettings",
        };

        var provider = CreateProvider(stored, DebugChannelOverride.Parse(["--allow-send"]));

        Assert.That(provider.Current.IsSendingAllowed, Is.True);
    }

    private TargetChannelProvider CreateProvider(DebugChannelSettings stored, DebugChannelOverride commandLine)
    {
        var store = new DebugChannelStore(null, Path.Combine(_tempDirectory, "debug-channel.json"));
        store.Save(stored);

        return new(_settingsManager, store, commandLine, null);
    }
}
