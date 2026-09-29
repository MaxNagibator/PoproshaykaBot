using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Tests.Statistics;

[TestFixture]
public sealed class StreamSessionMergeTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 6, 12, 53, TimeSpan.Zero);

    private static StreamSessionRecord Record(
        DateTimeOffset startedAt,
        TimeSpan duration,
        string channel = "bobito217",
        long messageCount = 0,
        int peakViewers = 0,
        int averageViewers = 0,
        string? game = null,
        string? title = null)
    {
        var record = new StreamSessionRecord
        {
            Channel = channel,
            StartedAt = startedAt,
            EndedAt = startedAt + duration,
            Title = title,
            Game = game,
            MessageCount = messageCount,
            PeakViewers = peakViewers,
            AverageViewers = averageViewers,
        };

        record.EnsureSegments();

        return record;
    }

    private static IEnumerable<TestCaseData> JoinCases()
    {
        yield return new TestCaseData(TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(-4), true)
            .SetName("Пересекающиеся интервалы склеиваются");

        yield return new TestCaseData(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(4), true)
            .SetName("Расхождение стартов в пределах допуска склеивается без пересечения");

        yield return new TestCaseData(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(6), false)
            .SetName("Разрыв больше допуска не склеивается");

        yield return new TestCaseData(TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(100), false)
            .SetName("Соседняя запись через час после конца не склеивается");
    }

    [TestCaseSource(nameof(JoinCases))]
    public void Merge_РешаетПоПересечениюИлиДопускуСтарта(TimeSpan firstDuration, TimeSpan secondOffset, bool joins)
    {
        var first = Record(Start, firstDuration);
        var second = Record(Start + secondOffset, TimeSpan.FromHours(1));

        var result = StreamSessionMerge.Merge([first, second]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Sessions, Has.Count.EqualTo(joins ? 1 : 2));
            Assert.That(result.MergedPairs, Is.EqualTo(joins ? 1 : 0));
        }
    }

    [Test]
    public void Merge_ПараСПересекающимисяИнтервалами_СкладываетСообщенияИБерётМаксимумПика()
    {
        var helix = Record(Start, TimeSpan.FromMinutes(47), messageCount: 120, peakViewers: 31, game: "Minecraft", title: "эфир");
        var local = Record(Start.AddSeconds(13), TimeSpan.FromMinutes(6), messageCount: 34, peakViewers: 44, game: "Minecraft");

        var result = StreamSessionMerge.Merge([helix, local]);
        var merged = result.Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MergedPairs, Is.EqualTo(1));
            Assert.That(merged.Id, Is.EqualTo(helix.Id));
            Assert.That(merged.Channel, Is.EqualTo("bobito217"));
            Assert.That(merged.StartedAt, Is.EqualTo(helix.StartedAt));
            Assert.That(merged.EndedAt, Is.EqualTo(helix.EndedAt));
            Assert.That(merged.MessageCount, Is.EqualTo(154));
            Assert.That(merged.PeakViewers, Is.EqualTo(44));
            Assert.That(merged.Title, Is.EqualTo("эфир"));
            Assert.That(merged.Segments, Has.Count.EqualTo(1), "Короткая запись целиком лежит внутри длинной – своего куска эфира у неё нет");
            Assert.That(merged.IsHidden, Is.False);
        }
    }

    [Test]
    public void Merge_ПоглощённыйСегмент_ОтдаётСчётчикиПоглотившемуИНеУдваиваетЭфир()
    {
        var helix = Record(Start, TimeSpan.FromMinutes(60), messageCount: 120, peakViewers: 31, averageViewers: 10, game: "Minecraft");
        var local = Record(Start.AddSeconds(13), TimeSpan.FromMinutes(20), messageCount: 34, peakViewers: 44, averageViewers: 70, game: "Minecraft");

        var merged = StreamSessionMerge.Merge([helix, local]).Sessions.Single();
        var segment = merged.Segments.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Airtime(merged), Is.EqualTo(merged.Duration), "Сумма сегментов равна эфиру записи – перекрытие не посчитано дважды");
            Assert.That(merged.Segments.Sum(item => item.MessageCount), Is.EqualTo(merged.MessageCount));
            Assert.That(segment.PeakViewers, Is.EqualTo(44));
            Assert.That(segment.AverageViewers, Is.EqualTo(25), "Среднее поглотившего пересчитано с весом исходных длительностей");
        }
    }

    [Test]
    public void Merge_ЧастичноеПерекрытие_ПодрезаетСегментПоНачалуИДержитСчётчикиПриСебе()
    {
        var first = Record(Start, TimeSpan.FromMinutes(60), messageCount: 10, averageViewers: 7, game: "Just Chatting");
        var second = Record(Start.AddMinutes(45), TimeSpan.FromMinutes(45), messageCount: 20, averageViewers: 9, game: "Minecraft");

        var merged = StreamSessionMerge.Merge([first, second]).Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged.Segments, Has.Count.EqualTo(2));
            Assert.That(merged.Segments[1].StartedAt, Is.EqualTo(merged.Segments[0].EndedAt));
            Assert.That(Airtime(merged), Is.EqualTo(merged.Duration));
            Assert.That(merged.Segments.Sum(item => item.MessageCount), Is.EqualTo(30));
            Assert.That(merged.Segments[1].AverageViewers, Is.EqualTo(9));
        }
    }

    private static TimeSpan Airtime(StreamSessionRecord record)
    {
        return record.Segments.Aggregate(TimeSpan.Zero, (sum, segment) => sum + segment.Duration);
    }

    [Test]
    public void Merge_ЦепочкаИзТрёх_СклеиваетсяЦеликомИСохраняетСменуКатегории()
    {
        var first = Record(Start, TimeSpan.FromMinutes(35), messageCount: 10, game: "Just Chatting");
        var second = Record(Start.AddMinutes(32), TimeSpan.FromMinutes(90), messageCount: 20, game: "Escape from Tarkov");
        var third = Record(Start.AddMinutes(34), TimeSpan.FromMinutes(200), messageCount: 30, game: "Escape from Tarkov");

        var result = StreamSessionMerge.Merge([first, second, third]);
        var merged = result.Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MergedPairs, Is.EqualTo(2));
            Assert.That(merged.Id, Is.EqualTo(first.Id));
            Assert.That(merged.StartedAt, Is.EqualTo(first.StartedAt));
            Assert.That(merged.EndedAt, Is.EqualTo(third.EndedAt));
            Assert.That(merged.MessageCount, Is.EqualTo(60));
            Assert.That(merged.Game, Is.EqualTo("Escape from Tarkov"));
            Assert.That(merged.Segments.Select(segment => segment.StartedAt), Is.Ordered);
            Assert.That(merged.Segments, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void Merge_РазныеКаналы_НеСклеиваются()
    {
        var first = Record(Start, TimeSpan.FromHours(3), "bobito217");
        var second = Record(Start.AddMinutes(1), TimeSpan.FromHours(3), "someone_else");

        var result = StreamSessionMerge.Merge([first, second]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Sessions, Has.Count.EqualTo(2));
            Assert.That(result.MergedPairs, Is.Zero);
        }
    }

    [Test]
    public void Merge_СкрытаяЗапись_НеУчаствуетВСклейке()
    {
        var hidden = Record(Start, TimeSpan.FromHours(3), messageCount: 1000);
        hidden.IsHidden = true;

        var visible = Record(Start.AddMinutes(1), TimeSpan.FromHours(3), messageCount: 7);

        var result = StreamSessionMerge.Merge([hidden, visible]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MergedPairs, Is.Zero);
            Assert.That(result.Sessions, Has.Count.EqualTo(2));
            Assert.That(result.Sessions[0].IsHidden, Is.True);
            Assert.That(result.Sessions[0].MessageCount, Is.EqualTo(1000));
            Assert.That(result.Sessions[1].MessageCount, Is.EqualTo(7));
        }
    }

    [Test]
    public void Merge_СреднееПоЗрителям_ВзвешиваетсяДлительностью()
    {
        var long3Hours = Record(Start, TimeSpan.FromHours(3), averageViewers: 10);
        var short1Hour = Record(Start.AddMinutes(10), TimeSpan.FromHours(1), averageViewers: 50);

        var merged = StreamSessionMerge.Merge([long3Hours, short1Hour]).Sessions.Single();

        Assert.That(merged.AverageViewers, Is.EqualTo(20));
    }

    [Test]
    public void Merge_НулеваяДлительность_ДаётПростоеСреднееНенулевых()
    {
        var first = Record(Start, TimeSpan.Zero, averageViewers: 0);
        var second = Record(Start.AddMinutes(1), TimeSpan.Zero, averageViewers: 9);
        var third = Record(Start.AddMinutes(2), TimeSpan.Zero, averageViewers: 5);

        var merged = StreamSessionMerge.Merge([first, second, third]).Sessions.Single();

        Assert.That(merged.AverageViewers, Is.EqualTo(7));
    }

    [Test]
    public void Merge_Чаттеры_СливаютсяПоUserIdИЗадаютЧисло()
    {
        var first = Record(Start, TimeSpan.FromHours(2));
        first.ChatterCount = 2;
        first.Chatters =
        [
            new() { UserId = "1", DisplayName = "alpha", MessageCount = 4 },
            new() { UserId = "2", DisplayName = "beta", MessageCount = 1 },
        ];

        var second = Record(Start.AddMinutes(30), TimeSpan.FromHours(2));
        second.ChatterCount = 2;
        second.Chatters =
        [
            new() { UserId = "1", DisplayName = "alpha", MessageCount = 3 },
            new() { UserId = "3", DisplayName = "gamma", MessageCount = 6 },
        ];

        var merged = StreamSessionMerge.Merge([first, second]).Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged.ChatterCount, Is.EqualTo(3));
            Assert.That(merged.Chatters.Select(chatter => chatter.DisplayName), Is.EqualTo(["alpha", "gamma", "beta"]));
            Assert.That(merged.Chatters[0].MessageCount, Is.EqualTo(7));
        }
    }

    [Test]
    public void Merge_БезСписковЧаттеров_БерётМаксимумИзИсходныхСчётчиков()
    {
        var first = Record(Start, TimeSpan.FromHours(2));
        first.ChatterCount = 12;

        var second = Record(Start.AddMinutes(30), TimeSpan.FromHours(2));
        second.ChatterCount = 30;

        var merged = StreamSessionMerge.Merge([first, second]).Sessions.Single();

        Assert.That(merged.ChatterCount, Is.EqualTo(30));
    }

    [Test]
    public void Merge_НазваниеИКатегория_ИдутОтДлиннойЗаписиИУступаютНепустому()
    {
        var shortWithTitle = Record(Start, TimeSpan.FromMinutes(6), game: "Minecraft", title: "короткий кусок");
        var longWithoutTitle = Record(Start.AddMinutes(1), TimeSpan.FromHours(4), game: null, title: null);

        var merged = StreamSessionMerge.Merge([shortWithTitle, longWithoutTitle]).Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged.Title, Is.EqualTo("короткий кусок"));
            Assert.That(merged.Game, Is.EqualTo("Minecraft"));
        }
    }

    [Test]
    public void Merge_БезСклейки_ВозвращаетИсходныеЗаписиВТомЖеПорядке()
    {
        var first = Record(Start, TimeSpan.FromHours(2));
        var second = Record(Start.AddDays(1), TimeSpan.FromHours(2));

        var result = StreamSessionMerge.Merge([first, second]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MergedPairs, Is.Zero);
            Assert.That(result.Sessions, Is.EqualTo(new[] { first, second }).AsCollection);
        }
    }

    [Test]
    public void Склейка_сливает_интервалы_учёта_и_не_даёт_учёту_превысить_эфир()
    {
        var first = Record(Start, TimeSpan.FromMinutes(40));
        first.TrackedIntervals = [Interval(Start, TimeSpan.FromMinutes(40))];

        var second = Record(Start.AddMinutes(30), TimeSpan.FromMinutes(50));
        second.TrackedIntervals =
        [
            Interval(Start.AddMinutes(30), TimeSpan.FromMinutes(20)),
            Interval(Start.AddMinutes(60), TimeSpan.FromMinutes(40)),
        ];

        var merged = StreamSessionMerge.Merge([first, second]).Sessions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged.Duration, Is.EqualTo(TimeSpan.FromMinutes(80)));

            Assert.That(merged.TrackedIntervals!.Select(interval => (interval.StartedAt, interval.EndedAt)),
                Is.EqualTo(new[]
                {
                    (Start, Start.AddMinutes(50)),
                    (Start.AddMinutes(60), Start.AddMinutes(80)),
                }));

            Assert.That(merged.TrackedDuration, Is.EqualTo(TimeSpan.FromMinutes(70)));
            Assert.That(merged.TrackedDuration, Is.LessThanOrEqualTo(merged.Duration));
        }
    }

    [Test]
    public void Склейка_с_записью_без_учёта_оставляет_учёт_неизвестным()
    {
        var first = Record(Start, TimeSpan.FromMinutes(40));

        var second = Record(Start.AddMinutes(30), TimeSpan.FromMinutes(50));
        second.TrackedIntervals = [Interval(Start.AddMinutes(30), TimeSpan.FromMinutes(50))];

        var merged = StreamSessionMerge.Merge([first, second]).Sessions.Single();

        Assert.That(merged.TrackedIntervals, Is.Null);
    }

    private static StreamSessionInterval Interval(DateTimeOffset startedAt, TimeSpan duration)
    {
        return new() { StartedAt = startedAt, EndedAt = startedAt + duration };
    }
}
