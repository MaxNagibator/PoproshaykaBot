using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Statistics;

[TestFixture]
public sealed class StreamSessionHistoryStoreTests
{
    [SetUp]
    public void SetUp()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"stream-history-{Guid.NewGuid():N}.json");
        _store = new(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var suffix in new[] { string.Empty, ".bak", ".old", ".tmp" })
        {
            var path = _tempFile + suffix;

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        var directory = Path.GetDirectoryName(_tempFile)!;
        var prefix = Path.GetFileNameWithoutExtension(_tempFile);

        foreach (var backup in Directory.GetFiles(directory, prefix + ".*-*"))
        {
            File.Delete(backup);
        }
    }

    private StreamSessionHistoryStore _store = null!;
    private string _tempFile = null!;

    private static StreamSessionRecord Record(string channel, long messages)
    {
        return new()
        {
            Channel = channel,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
            EndedAt = DateTimeOffset.UtcNow,
            Title = "Заголовок",
            Game = "Игра",
            MessageCount = messages,
            ChatterCount = 2,
            PeakViewers = 5,
            AverageViewers = 3,
            Chatters =
            [
                new() { UserId = "u1", DisplayName = "Alice", MessageCount = 3 },
                new() { UserId = "u2", DisplayName = "Bob", MessageCount = 1 },
            ],
        };
    }

    [Test]
    public void Append_Persists()
    {
        _store.Append(Record("chan", 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_store.Load().Sessions, Has.Count.EqualTo(1));
            Assert.That(File.Exists(_tempFile), Is.True);
        }
    }

    [Test]
    public void Append_DoesNotShareReferenceWithCaller()
    {
        var record = Record("chan", 10);
        _store.Append(record);

        record.MessageCount = 999;

        Assert.That(_store.Load().Sessions[0].MessageCount, Is.EqualTo(10));
    }

    [Test]
    public void Reload_RestoresEntriesFromDisk()
    {
        _store.Append(Record("chan-a", 10));
        _store.Append(Record("chan-b", 20));

        var reloaded = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        var sessions = reloaded.Load().Sessions;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sessions, Has.Count.EqualTo(2));
            Assert.That(sessions.Select(s => s.Channel), Is.EquivalentTo(["chan-a", "chan-b"]));
            Assert.That(sessions[0].Chatters.Select(c => c.DisplayName), Is.EqualTo(["Alice", "Bob"]));
            Assert.That(sessions[1].MessageCount, Is.EqualTo(20));
        }
    }

    [Test]
    public void Load_RecordWithoutSegments_BackfillsSingleSegmentFromFlatFields()
    {
        var legacy = Record("legacy", 50);
        legacy.Segments.Clear();

        var history = new StreamSessionHistory { Sessions = [legacy] };
        File.WriteAllText(_tempFile, JsonSerializer.Serialize(history, JsonStoreOptions.Default));

        var store = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        var session = store.Load().Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session.Segments, Has.Count.EqualTo(1));
            Assert.That(session.Segments[0].Game, Is.EqualTo("Игра"));
            Assert.That(session.Segments[0].MessageCount, Is.EqualTo(50));
            Assert.That(session.Segments[0].StartedAt, Is.EqualTo(session.StartedAt));
            Assert.That(session.Segments[0].EndedAt, Is.EqualTo(session.EndedAt));
        }
    }

    [Test]
    public void CorruptFile_IsBackedUpAndTreatedAsEmpty()
    {
        File.WriteAllText(_tempFile, "{ this is not valid json");

        var store = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);

        var directory = Path.GetDirectoryName(_tempFile)!;
        var prefix = Path.GetFileNameWithoutExtension(_tempFile);
        var backups = Directory.GetFiles(directory, prefix + ".invalid-*");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.Load().Sessions, Is.Empty);
            Assert.That(backups, Is.Not.Empty);
            Assert.That(store.IsLoaded, Is.False);
        }
    }

    [Test]
    public void FailedRead_KeepsEveryWritePathOutOfTheFile()
    {
        const string Broken = "{ this is not valid json";
        File.WriteAllText(_tempFile, Broken);

        var store = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        store.Append(Record("chan", 10));
        var hidden = store.TrySetHidden(Guid.NewGuid(), true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.IsLoaded, Is.False);
            Assert.That(hidden, Is.False, "Скрывать нечего: сессии с таким идентификатором в памяти нет");
            Assert.That(store.Load().Sessions, Has.Count.EqualTo(1),
                "Снятое право – это про файл: сессия остаётся в памяти, а выбросить её значило бы потерять её безвозвратно");
            Assert.That(File.ReadAllText(_tempFile), Is.EqualTo(Broken),
                "Повреждённый файл не переписывается ни одним путём записи");
        }
    }

    [Test]
    public void Append_SameChannelAndStart_IsRejected()
    {
        var first = Record("chan", 10);
        _store.Append(first);

        var duplicate = Record("CHAN", 999);
        duplicate.StartedAt = first.StartedAt;
        duplicate.EndedAt = first.EndedAt.AddMinutes(3);
        _store.Append(duplicate);

        var sessions = _store.Load().Sessions;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sessions, Has.Count.EqualTo(1));
            Assert.That(sessions[0].MessageCount, Is.EqualTo(10));
        }
    }

    [Test]
    public void Load_InterruptedStream_IsMergedOnceAndBackedUp()
    {
        var startedAt = new DateTimeOffset(2026, 8, 17, 6, 12, 53, TimeSpan.Zero);

        var helix = Record("bobito217", 120);
        helix.StartedAt = startedAt;
        helix.EndedAt = startedAt.AddMinutes(47);
        helix.PeakViewers = 31;

        var local = Record("bobito217", 34);
        local.StartedAt = startedAt.AddSeconds(13);
        local.EndedAt = startedAt.AddMinutes(6);
        local.PeakViewers = 44;

        var history = new StreamSessionHistory { Sessions = [helix, local] };
        File.WriteAllText(_tempFile, JsonSerializer.Serialize(history, JsonStoreOptions.Default));

        var store = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        var merged = store.Load().Sessions.Single();

        var onDisk = JsonSerializer.Deserialize<StreamSessionHistory>(File.ReadAllText(_tempFile), JsonStoreOptions.Default)!;

        var directory = Path.GetDirectoryName(_tempFile)!;
        var prefix = Path.GetFileNameWithoutExtension(_tempFile);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged.Id, Is.EqualTo(helix.Id));
            Assert.That(merged.MessageCount, Is.EqualTo(154));
            Assert.That(merged.PeakViewers, Is.EqualTo(44));
            Assert.That(merged.EndedAt, Is.EqualTo(helix.EndedAt));
            Assert.That(onDisk.Sessions, Has.Count.EqualTo(1));
            Assert.That(Directory.GetFiles(directory, prefix + ".premerge-*"), Is.Not.Empty);
        }
    }

    [Test]
    public void Load_LegacyRecordWithoutHiddenFlag_ReadsAsVisible()
    {
        File.WriteAllText(_tempFile,
            """
            {
              "sessions": [
                {
                  "id": "8f2b1f6a-6d0e-4a58-9a6c-7d2f0c3a1b44",
                  "channel": "bobito217",
                  "startedAt": "2026-08-17T06:12:53+00:00",
                  "endedAt": "2026-08-17T07:00:37+00:00",
                  "title": "эфир",
                  "game": "Minecraft",
                  "messageCount": 120,
                  "chatterCount": 2,
                  "peakViewers": 31,
                  "averageViewers": 12
                }
              ]
            }
            """);

        var store = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        var session = store.Load().Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.IsLoaded, Is.True);
            Assert.That(session.IsHidden, Is.False);
            Assert.That(session.Game, Is.EqualTo("Minecraft"));
            Assert.That(session.MessageCount, Is.EqualTo(120));
            Assert.That(session.Segments, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Запись_прежней_сборки_читается_без_учёта_и_переписывается_без_нового_поля()
    {
        var legacy = Record("legacy", 50);
        File.WriteAllText(_tempFile, JsonSerializer.Serialize(new StreamSessionHistory { Sessions = [legacy] }, JsonStoreOptions.Default));

        var store = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        var fresh = Record("fresh", 10);
        fresh.StartedAt = legacy.StartedAt.AddDays(1);
        fresh.EndedAt = fresh.StartedAt.AddHours(1);
        store.Append(fresh);

        using var file = JsonDocument.Parse(File.ReadAllText(_tempFile));
        var rewrittenLegacy = file.RootElement.GetProperty("sessions")[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.Load().Sessions[0].TrackedIntervals, Is.Null);
            Assert.That(store.Load().Sessions[0].TrackedDuration, Is.Null);
            Assert.That(rewrittenLegacy.TryGetProperty("trackedIntervals", out _), Is.False);
        }
    }

    [Test]
    public void Новый_файл_с_учётом_читается_прежней_сборкой_и_переживает_перечитывание()
    {
        var record = Record("chan", 10);
        record.TrackedIntervals = [new() { StartedAt = record.StartedAt.AddMinutes(5), EndedAt = record.EndedAt }];
        _store.Append(record);

        var previousBuild = JsonSerializer.Deserialize<PreviousBuildHistory>(File.ReadAllText(_tempFile), JsonStoreOptions.Default);
        var reloaded = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile).Load().Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(previousBuild!.Sessions.Single().MessageCount, Is.EqualTo(10));
            Assert.That(previousBuild.Sessions.Single().Chatters, Has.Count.EqualTo(2));
            Assert.That(reloaded.TrackedIntervals, Has.Count.EqualTo(1));
            Assert.That(reloaded.TrackedDuration, Is.EqualTo(record.Duration - TimeSpan.FromMinutes(5)));
        }
    }

    [Test]
    public void Испорченный_учёт_в_файле_не_превышает_эфир()
    {
        File.WriteAllText(_tempFile,
            """
            {
              "sessions": [
                {
                  "channel": "bobito217",
                  "startedAt": "2026-08-17T06:00:00+00:00",
                  "endedAt": "2026-08-17T07:00:00+00:00",
                  "trackedIntervals": [
                    { "startedAt": "2026-08-17T05:00:00+00:00", "endedAt": "2026-08-17T06:30:00+00:00" },
                    { "startedAt": "2026-08-17T06:20:00+00:00", "endedAt": "2026-08-17T09:00:00+00:00" },
                    { "startedAt": "2026-08-17T06:50:00+00:00", "endedAt": "2026-08-17T06:10:00+00:00" },
                    null
                  ]
                }
              ]
            }
            """);

        var session = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile).Load().Sessions.Single();

        Assert.That(session.TrackedDuration, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    private sealed class PreviousBuildHistory
    {
        public List<PreviousBuildRecord> Sessions { get; set; } = [];
    }

    private sealed class PreviousBuildRecord
    {
        public Guid Id { get; set; }
        public string Channel { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public long MessageCount { get; set; }
        public bool IsHidden { get; set; }
        public List<StreamSessionChatter> Chatters { get; set; } = [];
        public List<StreamSessionSegment> Segments { get; set; } = [];
    }

    [Test]
    public void TrySetHidden_SurvivesReload()
    {
        var record = Record("chan", 10);
        _store.Append(record);

        var hidden = _store.TrySetHidden(record.Id, true);

        var reloaded = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);
        var restored = reloaded.Load().Sessions.Single();

        var shown = reloaded.TrySetHidden(record.Id, false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hidden, Is.True);
            Assert.That(restored.IsHidden, Is.True);
            Assert.That(shown, Is.True);
            Assert.That(reloaded.Load().Sessions.Single().IsHidden, Is.False);
        }
    }

    [Test]
    public void TrySetHidden_WriteFails_RollsBackFlagAndReportsFailure()
    {
        var record = Record("chan", 10);
        _store.Append(record);

        var blocker = _tempFile + ".tmp";
        Directory.CreateDirectory(blocker);

        try
        {
            var result = _store.TrySetHidden(record.Id, true);
            var reloaded = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False, "Отказ записи виден вызывающему, а не летит исключением в интерфейс");
                Assert.That(_store.Load().Sessions.Single().IsHidden, Is.False, "Память откачена – страница не показывает убранным то, что на диске осталось видимым");
                Assert.That(reloaded.Load().Sessions.Single().IsHidden, Is.False);
            }
        }
        finally
        {
            Directory.Delete(blocker);
        }
    }

    [Test]
    public void TrySetHidden_UnknownId_ChangesNothing()
    {
        _store.Append(Record("chan", 10));

        var result = _store.TrySetHidden(Guid.NewGuid(), true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(_store.Load().Sessions.Single().IsHidden, Is.False);
        }
    }
}
