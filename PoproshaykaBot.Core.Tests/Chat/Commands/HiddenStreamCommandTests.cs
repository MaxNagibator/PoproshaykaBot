using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Tests.Chat.Commands;

[TestFixture]
public sealed class HiddenStreamCommandTests
{
    [SetUp]
    public void SetUp()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"stream-history-{Guid.NewGuid():N}.json");
        _store = new(NullLogger<StreamSessionHistoryStore>.Instance, _tempFile);

        var visible = Record("Видимый", 10, 5, DateTimeOffset.UtcNow.AddHours(-5));
        var hidden = Record(HiddenTitle, HiddenNumber, (int)HiddenNumber, DateTimeOffset.UtcNow.AddHours(-2));

        _store.Append(visible);
        _store.Append(hidden);
        _store.TrySetHidden(hidden.Id, true);
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
    }

    private const string HiddenTitle = "СкрытыйНаплывБотов";
    private const long HiddenNumber = 9999;

    private static readonly TestCaseData[] Commands =
    [
        new TestCaseData(
            (Func<StreamSessionHistoryStore, IChatCommand>)(store => new LastStreamCommand(store)),
            "Видимый").SetName("Последний стрим"),
        new TestCaseData(
            (Func<StreamSessionHistoryStore, IChatCommand>)(store => new PeakStreamCommand(store)),
            "👥 5").SetName("Рекорд стрима"),
        new TestCaseData(
            (Func<StreamSessionHistoryStore, IChatCommand>)(store => new StreamsCountCommand(store, TimeProvider.System)),
            "Всего стримов: 1").SetName("Счётчик стримов"),
        new TestCaseData(
            (Func<StreamSessionHistoryStore, IChatCommand>)(store => new TopStreamsCommand(store)),
            "1. Видимый").SetName("Топ стримов"),
        new TestCaseData(
            (Func<StreamSessionHistoryStore, IChatCommand>)(store => new WasThereCommand(store)),
            "на 1 стримах из 1").SetName("Я там был"),
    ];

    private StreamSessionHistoryStore _store = null!;
    private string _tempFile = null!;

    private static StreamSessionRecord Record(string title, long messages, int peakViewers, DateTimeOffset startedAt)
    {
        return new()
        {
            Channel = "chan",
            StartedAt = startedAt,
            EndedAt = startedAt.AddHours(1),
            Title = title,
            Game = "Игра",
            MessageCount = messages,
            ChatterCount = 1,
            PeakViewers = peakViewers,
            AverageViewers = 1,
            Chatters =
            [
                new() { UserId = "u1", DisplayName = "Alice", MessageCount = messages },
            ],
        };
    }

    [TestCaseSource(nameof(Commands))]
    public async Task Скрытый_стрим_не_попадает_в_ответ(Func<StreamSessionHistoryStore, IChatCommand> factory, string expected)
    {
        var command = factory(_store);

        var response = await command.ExecuteAsync(new()
        {
            MessageId = "m1",
            UserId = "u1",
            Username = "alice",
            DisplayName = "Alice",
            Arguments = [],
        }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Text, Does.Not.Contain(HiddenTitle));
            Assert.That(response.Text, Does.Not.Contain(HiddenNumber.ToString()));
            Assert.That(response.Text, Does.Contain(expected));
        }
    }

    [Test]
    public void LoadVisible_Отдаёт_только_видимые_а_Load_всю_историю()
    {
        var visible = _store.LoadVisible().Sessions;
        var all = _store.Load().Sessions;

        visible.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all.Exists(session => session.IsHidden), Is.True);
            Assert.That(_store.LoadVisible().Sessions.Select(session => session.Title), Is.EquivalentTo(new[] { "Видимый" }));
        }
    }
}
