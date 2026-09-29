using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.ViewModels.Settings;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
public sealed class RecentDebugChannelsTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot.RecentDebugChannelsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public void Выбор_недавнего_канала_ставит_его_в_черновик_и_включает_отладку()
    {
        var (section, _) = Create("dunduk");
        section.LoadSettings(new() { IsEnabled = false, Channel = "other" });

        section.Recent.UseCommand.Execute(section.Recent.Rows.Single());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(section.Channel, Is.EqualTo("dunduk"));
            Assert.That(section.IsEnabled, Is.True);
        }
    }

    [Test]
    public void Удаление_из_списка_пишется_в_файл_сразу_и_не_трогает_настройки_раздела()
    {
        var (section, store) = Create("dunduk", "mrbeast");
        store.Save(new() { IsEnabled = true, Channel = "dunduk", AllowSending = true });
        section.LoadSettings(store.Load());

        section.Recent.RemoveCommand.Execute(section.Recent.Rows.Single(row => row.Login == "dunduk"));

        var reread = new DebugChannelStore(null, Path.Combine(_directory, "debug-channel.json"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(section.Recent.Rows.Select(row => row.Login), Is.EqualTo(new[] { "mrbeast" }));
            Assert.That(reread.LoadRecent().Select(channel => channel.Login), Is.EqualTo(new[] { "mrbeast" }));
            Assert.That(reread.Load().Channel, Is.EqualTo("dunduk"));
            Assert.That(reread.Load().AllowSending, Is.True);
        }
    }

    [Test]
    public async Task Без_ключей_приложения_состояние_неизвестно_и_причина_названа()
    {
        var (section, _) = Create("dunduk");
        section.LoadSettings(new());

        await section.Recent.RefreshCommand.ExecuteAsync(null);

        var row = section.Recent.Rows.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.StateText, Is.EqualTo("неизвестно"));
            Assert.That(row.Details, Is.EqualTo(ChannelLiveStatusReader.MissingCredentialsReason));
            Assert.That(section.Recent.Note, Does.Contain(ChannelLiveStatusReader.MissingCredentialsReason));
            Assert.That(section.Recent.IsRefreshing, Is.False);
        }
    }

    private (DebugChannelSectionViewModel Section, DebugChannelStore Store) Create(params string[] recent)
    {
        var store = new DebugChannelStore(null, Path.Combine(_directory, "debug-channel.json"));
        var start = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        for (var index = 0; index < recent.Length; index++)
        {
            store.RecordRecent(recent[index], start.AddMinutes(index));
        }

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory, "settings.json"));
        var reader = new ChannelLiveStatusReader(new NoNetwork(), settings, TimeProvider.System, NullLogger<ChannelLiveStatusReader>.Instance);

        var section = new DebugChannelSectionViewModel(DebugChannelOverride.None,
            store,
            reader,
            TimeProvider.System,
            NullLogger<DebugChannelSectionViewModel>.Instance);

        return (section, store);
    }

    private sealed class NoNetwork : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            throw new AssertionException("Без Client ID и Client Secret запрос в сеть не должен уходить");
        }
    }
}
