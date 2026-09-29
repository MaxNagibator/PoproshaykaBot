using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Chat;

namespace PoproshaykaBot.Core.Tests.Settings.Stores;

[TestFixture]
public sealed class SettingsWriteFactTests
{
    private DirectoryInfo _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("settings-write-fact");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(true);
    }

    public static IEnumerable<TestCaseData> Writers()
    {
        yield return Case("accounts.json", "AccountsStore.SaveAll", (path, gate) =>
            new AccountsStore(null, path, gate).SaveAll(new() { Login = "bot" }, new() { Login = "streamer" }));

        yield return Case("broadcast-profiles.json", "BroadcastProfilesStore.Save", (path, gate) =>
            new BroadcastProfilesStore(null, path, gate).Save(new()));

        yield return Case("broadcast-profiles.json", "BroadcastProfilesStore.Mutate", (path, gate) =>
            new BroadcastProfilesStore(null, path, gate).Mutate(settings => settings.LastAppliedProfileId = Guid.NewGuid()));

        yield return Case("broadcast-profiles.json", "BroadcastProfilesManager.Upsert", (path, gate) =>
            CreateProfilesManager(path, gate).Upsert(new() { Name = "Стрим" }));

        yield return Case("broadcast-profiles.json", "BroadcastProfilesManager.Remove", (path, gate) =>
            CreateProfilesManager(path, gate).Remove(Guid.NewGuid()));

        yield return Case("polls.json", "PollsStore.Save", (path, gate) =>
            new PollsStore(null, path, gate).Save(new()));

        yield return Case("polls.json", "PollsStore.Mutate", (path, gate) =>
            new PollsStore(null, path, gate).Mutate(settings => settings.HistoryMaxItems = 42));

        yield return Case("polls.json", "PollProfilesManager.Upsert", (path, gate) =>
            CreatePollsManager(path, gate).Upsert(CreatePoll()));

        yield return Case("polls.json", "PollProfilesManager.ReplaceAll", (path, gate) =>
            CreatePollsManager(path, gate).ReplaceAll([CreatePoll()]));

        yield return Case("polls.json", "PollProfilesManager.Remove", (path, gate) =>
            CreatePollsManager(path, gate).Remove(Guid.NewGuid()));
    }

    [TestCaseSource(nameof(Writers))]
    public async Task Обёртка_отдаёт_наружу_факт_записи(string fileName, Func<string, SettingsWriteGate, bool> write)
    {
        var openPath = Path.Combine(_directory.FullName, "open", fileName);
        var closedPath = Path.Combine(_directory.FullName, "closed", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(openPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(closedPath)!);

        var closedGate = await CreateClosedGateAsync(fileName);

        var writtenOpen = write(openPath, new());
        var writtenClosed = write(closedPath, closedGate);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(writtenOpen, Is.True, "Открытый гейт – файл записан, и вызывающий об этом знает");
            Assert.That(File.Exists(openPath), Is.True);
            Assert.That(writtenClosed, Is.False, "Закрытый гейт – вызывающий должен узнать, что файл не переписан");
            Assert.That(File.Exists(closedPath), Is.False, "Гейт не пустил запись на диск");
        }
    }

    [Test]
    public async Task Незаписанная_правка_профиля_действует_в_памяти()
    {
        var path = Path.Combine(_directory.FullName, "broadcast-profiles.json");
        var manager = CreateProfilesManager(path, await CreateClosedGateAsync("broadcast-profiles.json"));

        var written = manager.Upsert(new() { Name = "Стрим" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(written, Is.False);
            Assert.That(manager.GetAll().Select(profile => profile.Name), Is.EquivalentTo(new[] { "Стрим" }),
                "Снятое право отнимает запись на диск, а не правку в памяти");
        }
    }

    private static TestCaseData Case(string fileName, string name, Func<string, SettingsWriteGate, bool> write)
    {
        return new TestCaseData(fileName, write).SetArgDisplayNames(name);
    }

    private static async Task<SettingsWriteGate> CreateClosedGateAsync(string fileName)
    {
        var gate = new SettingsWriteGate();
        await gate.RunExternalWriteAsync(() => Task.FromResult(0), _ => new[] { fileName });

        return gate;
    }

    private static BroadcastProfilesManager CreateProfilesManager(string path, SettingsWriteGate gate)
    {
        return new(new(null, path, gate),
            Substitute.For<IChannelInformationApplier>(),
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            TimeProvider.System,
            NullLogger<BroadcastProfilesManager>.Instance);
    }

    private static PollProfilesManager CreatePollsManager(string path, SettingsWriteGate gate)
    {
        return new(new(null, path, gate),
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            NullLogger<PollProfilesManager>.Instance);
    }

    private static PollProfile CreatePoll()
    {
        return new()
        {
            Name = "Опрос",
            Title = "Что дальше?",
            Choices = ["Да", "Нет"],
        };
    }
}
