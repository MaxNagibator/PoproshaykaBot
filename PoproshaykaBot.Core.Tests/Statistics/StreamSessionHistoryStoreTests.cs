using PoproshaykaBot.Core.Statistics;

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

        foreach (var backup in Directory.GetFiles(directory, prefix + ".invalid-*"))
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
        }
    }
}
