using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Polls;

[TestFixture]
public class PollHistoryStoreTests
{
    [SetUp]
    public void SetUp()
    {
        _polls = new();
        _pollsStore = Substitute.For<PollsStore>(NullLogger<PollsStore>.Instance, null, null);
        _pollsStore.Load().Returns(_polls);
        _helix = Substitute.For<ITwitchHelixClient>();
        _broadcasterIdProvider = Substitute.For<IBroadcasterIdProvider>();
        _broadcasterIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns("1");
        _directory = Path.Combine(Path.GetTempPath(), $"poll-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _tempFile = Path.Combine(_directory, "polls-history.json");
        _store = new(_pollsStore, _helix, _broadcasterIdProvider, new FakeTargetChannelProvider(), NullLogger<PollHistoryStore>.Instance, _tempFile);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }

    private PollsStore _pollsStore = null!;
    private PollsSettings _polls = null!;
    private ITwitchHelixClient _helix = null!;
    private IBroadcasterIdProvider _broadcasterIdProvider = null!;
    private PollHistoryStore _store = null!;
    private string _directory = null!;
    private string _tempFile = null!;

    private static PollHistoryEntry Entry(string id)
    {
        return new()
        {
            PollId = id,
            Title = "T",
            StartedAtUtc = DateTime.UtcNow,
            EndedAtUtc = DateTime.UtcNow,
            FinalStatus = PollSnapshotStatus.Completed,
        };
    }

    [Test]
    public async Task BackfillAsync_OnForeignChannel_DoesNotTouchTheBroadcasterApi()
    {
        var store = new PollHistoryStore(_pollsStore,
            _helix,
            _broadcasterIdProvider,
            FakeTargetChannelProvider.Foreign("someone-else", "bobito217"),
            NullLogger<PollHistoryStore>.Instance,
            _tempFile);

        var added = await store.BackfillAsync(CancellationToken.None);

        Assert.That(added, Is.Zero);

        await _helix.DidNotReceiveWithAnyArgs().GetPollsAsync(default!, default, default);
    }

    [Test]
    public void TryAdd_Unique_Persists()
    {
        var added = _store.TryAdd(Entry("p1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.True);
            Assert.That(_store.GetAll(), Has.Count.EqualTo(1));
            Assert.That(File.Exists(_tempFile), Is.True);
        }
    }

    [Test]
    public void TryAdd_Duplicate_Ignored()
    {
        _store.TryAdd(Entry("p1"));
        var added = _store.TryAdd(Entry("p1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.False);
            Assert.That(_store.GetAll(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void TryAdd_TruncatesToHistoryMaxItems()
    {
        _polls.HistoryMaxItems = 3;

        _store.TryAdd(Entry("p1"));
        _store.TryAdd(Entry("p2"));
        _store.TryAdd(Entry("p3"));
        _store.TryAdd(Entry("p4"));

        var all = _store.GetAll();
        Assert.That(all, Has.Count.EqualTo(3));
        Assert.That(all.Select(e => e.PollId), Is.EquivalentTo(["p2", "p3", "p4"]));
    }

    [Test]
    public void Reload_FileWithANullChoiceList_ReadsItAsEmpty()
    {
        File.WriteAllText(_tempFile, """{"entries":[{"pollId":"p1","finalChoices":null}]}""");

        var entry = new PollHistoryStore(_pollsStore, _helix, _broadcasterIdProvider, new FakeTargetChannelProvider(), NullLogger<PollHistoryStore>.Instance, _tempFile)
            .GetAll()
            .Single();

        Assert.That(entry.FinalChoices, Is.Empty,
            "История голосований лежит в своём файле и со своими опциями сериализации, но правило про null у ненулевого свойства действует и здесь.");
    }

    [Test]
    public void Reload_RestoresEntriesFromDisk()
    {
        _store.TryAdd(Entry("p1"));
        _store.TryAdd(Entry("p2"));

        var second = new PollHistoryStore(_pollsStore, _helix, _broadcasterIdProvider, new FakeTargetChannelProvider(), NullLogger<PollHistoryStore>.Instance, _tempFile);
        var all = second.GetAll();

        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Select(e => e.PollId), Is.EquivalentTo(["p1", "p2"]));
    }

    [Test]
    public async Task BackfillAsync_SkipsActivePolls_AddsCompleted()
    {
        var started = DateTime.UtcNow.AddMinutes(-5);
        var ended = DateTime.UtcNow.AddMinutes(-4);

        _helix.GetPollsAsync("1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new("active", "1", "Active?", [new("c1", "A", 0, 0, 0)], false, 0, "ACTIVE", 60,
                    started, null),
                new("done", "1", "Done?", [new("c1", "A", 5, 0, 0), new("c2", "B", 3, 0, 0)], false, 0, "COMPLETED", 60,
                    started, ended),
            ]);

        var added = await _store.BackfillAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.EqualTo(1));
            Assert.That(_store.GetAll().Select(e => e.PollId), Is.EquivalentTo(["done"]));
        }
    }

    [Test]
    public async Task BackfillAsync_SkipsUnknownStatusPolls()
    {
        var started = DateTime.UtcNow.AddMinutes(-5);
        var ended = DateTime.UtcNow.AddMinutes(-4);

        _helix.GetPollsAsync("1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new("unknown", "1", "Unknown?", [new("c1", "A", 7, 0, 0)], false, 0, "SOMETHING_NEW", 60,
                    started, ended),
                new("done", "1", "Done?", [new("c1", "A", 5, 0, 0), new("c2", "B", 3, 0, 0)], false, 0, "COMPLETED", 60,
                    started, ended),
            ]);

        var added = await _store.BackfillAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.EqualTo(1));
            Assert.That(_store.GetAll().Select(e => e.PollId), Is.EquivalentTo(["done"]));
        }
    }

    [Test]
    public async Task BackfillAsync_Dedups_AgainstExisting()
    {
        _store.TryAdd(Entry("done"));
        var started = DateTime.UtcNow.AddMinutes(-5);
        var ended = DateTime.UtcNow.AddMinutes(-4);

        _helix.GetPollsAsync("1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new("done", "1", "Done?", [new("c1", "A", 5, 0, 0)], false, 0, "COMPLETED", 60,
                    started, ended),
            ]);

        var added = await _store.BackfillAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.EqualTo(0));
            Assert.That(_store.GetAll(), Has.Count.EqualTo(1));
        }
    }

    [TestCase("{ это не json", TestName = "Сорвавшееся чтение истории голосований не даёт права писать (мусор вместо json)")]
    [TestCase("null", TestName = "Сорвавшееся чтение истории голосований не даёт права писать (файл разобран в пустое значение)")]
    [TestCase("", TestName = "Сорвавшееся чтение истории голосований не даёт права писать (файл нулевой длины)")]
    [TestCase("   \r\n", TestName = "Сорвавшееся чтение истории голосований не даёт права писать (файл из одних пробелов)")]
    public void Сорвавшееся_чтение_не_даёт_права_переписывать_файл(string content)
    {
        File.WriteAllText(_tempFile, content);

        var logger = new RecordingLogger<PollHistoryStore>();
        var store = Create(logger);

        var added = store.TryAdd(Entry("p1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.IsLoaded, Is.False,
                "Существующий файл, не разобравшийся в историю, – признак оборвавшейся записи, и класть память поверх него нельзя; законная пустота только у отсутствующего файла");
            Assert.That(File.ReadAllText(_tempFile), Is.EqualTo(content),
                "Повреждённый файл остаётся как был – в нём могут быть данные, которые ещё вытащат руками");
            Assert.That(InvalidCopies(), Is.Not.Empty, "Рядом с непрочитанным файлом ложится копия с суффиксом invalid");
            Assert.That(added, Is.True);
            Assert.That(store.GetAll().Select(entry => entry.PollId), Is.EquivalentTo(["p1"]),
                "Снятое право – про файл, а не про память: запись копится и не теряется молча");
            Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Error), Is.True,
                "Пользователь узнаёт о случившемся из журнала");
        }
    }

    [Test]
    public void Отсутствующий_файл_это_законная_пустота()
    {
        var store = Create();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.GetAll(), Is.Empty);
            Assert.That(store.IsLoaded, Is.True, "Свежий профиль без файла – не отказ чтения, писать в него можно");
        }

        store.TryAdd(Entry("p1"));

        Assert.That(File.Exists(_tempFile), Is.True);
    }

    [Test]
    public void Снятое_право_копит_записи_в_памяти_и_возврат_сбрасывает_их_на_диск()
    {
        _store.TryAdd(Entry("p0"));
        var onDisk = File.ReadAllText(_tempFile);

        var wasLoaded = _store.Invalidate();
        var added = _store.TryAdd(Entry("p1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wasLoaded, Is.True);
            Assert.That(added, Is.True);
            Assert.That(_store.IsLoaded, Is.False);
            Assert.That(_store.TryFlush(), Is.False, "Без права записи повтор не пытается писать вовсе");
            Assert.That(File.ReadAllText(_tempFile), Is.EqualTo(onDisk),
                "Принесённую переносом историю нельзя переписывать памятью стора");
        }

        _store.Restore(wasLoaded);

        Assert.That(PollIdsOnDisk(), Is.EquivalentTo(["p0", "p1"]),
            "Возврат права сбрасывает накопленное на диск одной записью");
    }

    [Test]
    public void Гашение_до_первого_чтения_читает_файл_и_оставляет_память_доимпортной()
    {
        File.WriteAllText(_tempFile, HistoryJson("p0"));

        var store = Create();
        var wasLoaded = store.Invalidate();

        File.WriteAllText(_tempFile, HistoryJson("принесённый"));
        store.TryAdd(Entry("p1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wasLoaded, Is.True,
                "Чтение ленивое, поэтому гашение само доводит его до конца – иначе стор прочитал бы уже принесённый файл");
            Assert.That(store.GetAll().Select(entry => entry.PollId), Is.EquivalentTo(["p0", "p1"]));
            Assert.That(PollIdsOnDisk(), Is.EquivalentTo(["принесённый"]),
                "Принесённое вступает в силу после перезапуска, а до него файл не трогают");
        }
    }

    [Test]
    public void Сорвавшаяся_запись_дотаскивается_ближайшим_повтором()
    {
        var store = Create();
        var obstacle = _tempFile + ".tmp";
        Directory.CreateDirectory(obstacle);

        store.TryAdd(Entry("p1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(_tempFile), Is.False, "Запись сорвалась, файла нет");
            Assert.That(store.TryFlush(), Is.False, "Пока препятствие на месте, повтор честно отвечает отказом");
        }

        Directory.Delete(obstacle);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.TryFlush(), Is.True);
            Assert.That(PollIdsOnDisk(), Is.EquivalentTo(["p1"]),
                "Несохранённое дотаскивается повтором, а не теряется до перезапуска");
        }
    }

    private static string HistoryJson(string pollId)
    {
        return $$"""{"version":1,"entries":[{"pollId":"{{pollId}}","title":"T","finalStatus":1}]}""";
    }

    private PollHistoryStore Create(ILogger<PollHistoryStore>? logger = null)
    {
        return new(_pollsStore,
            _helix,
            _broadcasterIdProvider,
            new FakeTargetChannelProvider(),
            logger ?? NullLogger<PollHistoryStore>.Instance,
            _tempFile);
    }

    private string[] InvalidCopies()
    {
        return Directory.GetFiles(_directory, "polls-history.invalid-*.json");
    }

    private string[] PollIdsOnDisk()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(_tempFile));

        return [.. document.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("pollId").GetString()!)];
    }
}
