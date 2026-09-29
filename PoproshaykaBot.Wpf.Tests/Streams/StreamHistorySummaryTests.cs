using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.ViewModels;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
public class StreamHistorySummaryTests
{
    private static readonly DateTimeOffset Start = new(new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Local));

    [Test]
    public void Пустая_история_даёт_пустую_сводку()
    {
        var summary = StreamHistorySummary.Build([]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories, Is.Empty);
            Assert.That(summary.Records, Is.Empty);
        }
    }

    [Test]
    public void Одна_сессия_даёт_категорию_но_не_даёт_рекордов()
    {
        var summary = StreamHistorySummary.Build([Session(0, hours: 3, ("Minecraft", 3))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories, Has.Count.EqualTo(1));
            Assert.That(summary.Categories[0].Game, Is.EqualTo("Minecraft"));
            Assert.That(summary.Categories[0].AirTime, Is.EqualTo(TimeSpan.FromHours(3)));
            Assert.That(summary.Categories[0].SessionCount, Is.EqualTo(1));
            Assert.That(summary.Categories[0].Share, Is.EqualTo(1).Within(0.001));
            Assert.That(summary.Records, Is.Empty);
        }
    }

    [Test]
    public void Сессия_с_тремя_играми_делится_на_три_доли()
    {
        var summary = StreamHistorySummary.Build(
        [
            Session(0, hours: 6, ("Just Chatting", 1), ("Minecraft", 3), ("Dota 2", 2)),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories.Select(category => category.Game), Is.EqualTo(new[] { "Minecraft", "Dota 2", "Just Chatting" }));
            Assert.That(summary.Categories.Select(category => category.AirTime), Is.EqualTo(new[] { TimeSpan.FromHours(3), TimeSpan.FromHours(2), TimeSpan.FromHours(1) }));
            Assert.That(summary.Categories.Sum(category => category.Share), Is.EqualTo(1).Within(0.001));
            Assert.That(summary.Categories.Select(category => category.SessionCount), Is.All.EqualTo(1));
        }
    }

    [Test]
    public void Эфир_по_категориям_совпадает_с_суммарным_эфиром()
    {
        var sessions = new List<StreamSessionRecord>
        {
            Session(0, hours: 6, ("Just Chatting", 1), ("Minecraft", 5)),
            Session(1, hours: 4, ("Minecraft", 4)),
        };

        var summary = StreamHistorySummary.Build(sessions);
        var total = sessions.Aggregate(TimeSpan.Zero, (sum, session) => sum + session.Duration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories.Aggregate(TimeSpan.Zero, (sum, category) => sum + category.AirTime), Is.EqualTo(total));
            Assert.That(summary.Categories[0].Game, Is.EqualTo("Minecraft"));
            Assert.That(summary.Categories[0].SessionCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void Сегмент_созданный_хранилищем_считается_как_обычный()
    {
        var legacy = new StreamSessionRecord
        {
            Channel = "bobito217",
            StartedAt = Start,
            EndedAt = Start.AddHours(2),
            Title = "старый эфир",
            Game = "Minecraft",
            MessageCount = 10,
            ChatterCount = 2,
            PeakViewers = 7,
            AverageViewers = 3,
        };

        legacy.EnsureSegments();

        var summary = StreamHistorySummary.Build([legacy]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories, Has.Count.EqualTo(1));
            Assert.That(summary.Categories[0].Game, Is.EqualTo("Minecraft"));
            Assert.That(summary.Categories[0].AirTime, Is.EqualTo(TimeSpan.FromHours(2)));
        }
    }

    [Test]
    public void Категорий_в_топе_не_больше_пяти()
    {
        var segments = Enumerable.Range(0, 7).Select(index => ((string?)$"Игра {index}", index + 1)).ToArray();
        var summary = StreamHistorySummary.Build([Session(0, hours: 28, segments)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories, Has.Count.EqualTo(StreamHistorySummary.MaxCategories));
            Assert.That(summary.Categories[0].Game, Is.EqualTo("Игра 6"));
            Assert.That(summary.Categories[^1].Game, Is.EqualTo("Игра 2"));
        }
    }

    [Test]
    public void Сегменты_без_категории_в_топ_не_идут()
    {
        var summary = StreamHistorySummary.Build([Session(0, hours: 2, (null, 2)), Session(1, hours: 2, (null, 2))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Categories, Is.Empty);
            Assert.That(summary.Records, Is.Not.Empty);
        }
    }

    [TestCase(StreamRecordKind.Duration, "долгий")]
    [TestCase(StreamRecordKind.PeakViewers, "зрители")]
    [TestCase(StreamRecordKind.Messages, "сообщения")]
    [TestCase(StreamRecordKind.Chatters, "чаттеры")]
    public void Рекорд_называет_свою_сессию(StreamRecordKind kind, string expectedTitle)
    {
        var summary = StreamHistorySummary.Build(
        [
            Titled("долгий", hours: 9, messages: 10, peak: 10, chatters: 10),
            Titled("зрители", hours: 1, messages: 10, peak: 90, chatters: 10),
            Titled("сообщения", hours: 1, messages: 900, peak: 10, chatters: 10),
            Titled("чаттеры", hours: 1, messages: 10, peak: 10, chatters: 90),
        ]);

        var record = summary.Records.Single(item => item.Kind == kind);

        Assert.That(record.Session.Title, Is.EqualTo(expectedTitle));
    }

    [Test]
    public void Метрика_без_значений_рекорда_не_даёт()
    {
        var summary = StreamHistorySummary.Build(
        [
            Titled("первый", hours: 2, messages: 10, peak: 0, chatters: 1),
            Titled("второй", hours: 2, messages: 20, peak: 0, chatters: 2),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Records.Select(record => record.Kind), Does.Not.Contain(StreamRecordKind.PeakViewers));
            Assert.That(summary.Records, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void При_равных_значениях_рекорд_остаётся_за_свежей_сессией()
    {
        var summary = StreamHistorySummary.Build(
        [
            Titled("старый", hours: 2, messages: 50, peak: 5, chatters: 5),
            Titled("свежий", hours: 2, messages: 50, peak: 5, chatters: 5, dayOffset: 4),
        ]);

        Assert.That(summary.Records.Single(record => record.Kind == StreamRecordKind.Messages).Session.Title, Is.EqualTo("свежий"));
    }

    private static StreamSessionRecord Titled(
        string title,
        int hours,
        long messages,
        int peak,
        int chatters,
        int dayOffset = 0)
    {
        var started = Start.AddDays(dayOffset);

        return new()
        {
            Channel = "bobito217",
            StartedAt = started,
            EndedAt = started.AddHours(hours),
            Title = title,
            Game = "Minecraft",
            MessageCount = messages,
            ChatterCount = chatters,
            PeakViewers = peak,
            AverageViewers = peak / 2,
            Segments =
            [
                new()
                {
                    StartedAt = started,
                    EndedAt = started.AddHours(hours),
                    Title = title,
                    Game = "Minecraft",
                    MessageCount = messages,
                    PeakViewers = peak,
                    AverageViewers = peak / 2,
                },
            ],
        };
    }

    private static StreamSessionRecord Session(int index, int hours, params (string? Game, int Hours)[] segments)
    {
        var started = Start.AddDays(index);
        var session = new StreamSessionRecord
        {
            Channel = "bobito217",
            StartedAt = started,
            EndedAt = started.AddHours(hours),
            Title = "эфир",
            Game = segments.Length > 0 ? segments[0].Game : null,
            MessageCount = 100,
            ChatterCount = 3,
            PeakViewers = 10,
            AverageViewers = 5,
        };

        var cursor = started;

        foreach (var (game, length) in segments)
        {
            session.Segments.Add(new()
            {
                StartedAt = cursor,
                EndedAt = cursor.AddHours(length),
                Title = "эфир",
                Game = game,
                MessageCount = 10,
                PeakViewers = 10,
                AverageViewers = 5,
            });

            cursor = cursor.AddHours(length);
        }

        return session;
    }
}
