using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Debugging;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Tests.Debugging;

[TestFixture]
public class DebugChannelRecentTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private string _tempDirectory = null!;
    private string _filePath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot.DebugRecentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _filePath = Path.Combine(_tempDirectory, "debug-channel.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public void Повторный_канал_поднимается_наверх_без_дубля_и_без_учёта_регистра()
    {
        var store = new DebugChannelStore(null, _filePath);

        store.RecordRecent("dunduk", Start);
        store.RecordRecent("mrbeast", Start.AddMinutes(1));
        store.RecordRecent("DunDuk", Start.AddMinutes(2));

        var recent = new DebugChannelStore(null, _filePath).LoadRecent();

        Assert.That(recent.Select(channel => (channel.Login, channel.LastUsedAt)),
            Is.EqualTo(new[] { ("dunduk", Start.AddMinutes(2)), ("mrbeast", Start.AddMinutes(1)) }));
    }

    [Test]
    public void Список_держит_не_больше_десяти_самых_свежих_каналов()
    {
        var store = new DebugChannelStore(null, _filePath);

        for (var index = 0; index < DebugChannelStore.MaxRecentChannels + 3; index++)
        {
            store.RecordRecent($"channel{index}", Start.AddMinutes(index));
        }

        var recent = store.LoadRecent();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recent, Has.Count.EqualTo(DebugChannelStore.MaxRecentChannels));
            Assert.That(recent[0].Login, Is.EqualTo("channel12"));
            Assert.That(recent[^1].Login, Is.EqualTo("channel3"));
            Assert.That(store.Load().RecentChannels, Has.Count.EqualTo(DebugChannelStore.MaxRecentChannels), "потолок держит и файл, а не только выдача");
        }
    }

    [TestCase("""{"isEnabled":true,"channel":"dunduk","allowSending":true}""")]
    [TestCase("""{"isEnabled":true,"channel":"dunduk","allowSending":true,"recentChannels":null}""")]
    [TestCase("""{"isEnabled":true,"channel":"dunduk","allowSending":true,"recentChannels":[null,{"login":"не канал"},{"login":""}]}""")]
    public void Файл_без_списка_или_с_мусором_в_нём_читается_как_раньше(string json)
    {
        File.WriteAllText(_filePath, json);

        var store = new DebugChannelStore(null, _filePath);
        var settings = store.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.IsEnabled, Is.True);
            Assert.That(settings.Channel, Is.EqualTo("dunduk"));
            Assert.That(settings.AllowSending, Is.True);
            Assert.That(store.LoadRecent(), Is.Empty);
        }

        store.RecordRecent("mrbeast", Start);

        Assert.That(new DebugChannelStore(null, _filePath).LoadRecent().Single().Login, Is.EqualTo("mrbeast"));
    }

    [Test]
    public void Сохранение_черновика_настроек_не_откатывает_список_записанный_после_его_снятия()
    {
        var store = new DebugChannelStore(null, _filePath);
        var draft = store.Load();

        store.RecordRecent("dunduk", Start);

        draft.IsEnabled = true;
        draft.Channel = "mrbeast";
        store.Save(draft);

        var reread = new DebugChannelStore(null, _filePath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reread.Load().Channel, Is.EqualTo("mrbeast"));
            Assert.That(reread.LoadRecent().Select(channel => channel.Login), Is.EqualTo(new[] { "dunduk" }),
                "черновик раздела снят до начала сессии и списка не владеет – иначе «Сохранить» стирал бы только что записанный канал");
        }
    }

    [Test]
    public void Удаление_из_списка_не_трогает_остальные_поля_файла()
    {
        var store = new DebugChannelStore(null, _filePath);
        store.Save(new() { IsEnabled = true, Channel = "dunduk", AllowSending = true });
        store.RecordRecent("dunduk", Start);
        store.RecordRecent("mrbeast", Start.AddMinutes(1));

        var removed = store.RemoveRecent("@MrBeast");

        var reread = new DebugChannelStore(null, _filePath);
        var settings = reread.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.True);
            Assert.That(reread.LoadRecent().Select(channel => channel.Login), Is.EqualTo(new[] { "dunduk" }));
            Assert.That(settings.IsEnabled, Is.True);
            Assert.That(settings.Channel, Is.EqualTo("dunduk"));
            Assert.That(settings.AllowSending, Is.True);
        }
    }

    [TestCase(true, "dunduk", true)]
    [TestCase(true, OwnChannel, false)]
    [TestCase(false, "dunduk", false)]
    public void Начало_сессии_записывает_только_чужой_отладочный_канал(bool isEnabled, string channel, bool recorded)
    {
        var store = new DebugChannelStore(null, _filePath);
        store.Save(new() { IsEnabled = isEnabled, Channel = channel });

        var provider = new TargetChannelProvider(CreateSettingsManager(), store, DebugChannelOverride.None, null, new FixedTime(Start));
        provider.BeginSession();

        var expected = recorded ? new[] { (channel, Start) } : Array.Empty<(string, DateTimeOffset)>();

        Assert.That(store.LoadRecent().Select(entry => (entry.Login, entry.LastUsedAt)), Is.EqualTo(expected));
    }

    [Test]
    public void Канал_из_командной_строки_тоже_попадает_в_недавние()
    {
        var store = new DebugChannelStore(null, _filePath);

        var provider = new TargetChannelProvider(CreateSettingsManager(),
            store,
            DebugChannelOverride.Parse(["--debug-channel", "twitch.tv/FromCommandLine"]),
            null,
            new FixedTime(Start));

        provider.BeginSession();

        Assert.That(store.LoadRecent().Single().Login, Is.EqualTo("fromcommandline"));
    }

    private const string OwnChannel = "bobito217";

    private static SettingsManager CreateSettingsManager()
    {
        var settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        settingsManager.Current.Returns(new AppSettings { Twitch = { Channel = OwnChannel } });
        return settingsManager;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
