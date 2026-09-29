using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Core.Infrastructure.Events;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Events;

[TestFixture]
public sealed class EventBusMetricsTests
{
    [SetUp]
    public void SetUp()
    {
        _metrics = new();
        _bus = new(NullLogger<InMemoryEventBus>.Instance, _metrics);
    }

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private EventBusMetrics _metrics = null!;
    private InMemoryEventBus _bus = null!;

    [Test]
    public async Task Публикация_без_подписчиков_считается()
    {
        await _bus.PublishAsync(new TestEvent("без подписчиков"));

        var statistics = _metrics.Snapshot();

        Assert.Multiple(() =>
        {
            Assert.That(statistics.PublishedTotal, Is.EqualTo(1));
            Assert.That(statistics.HandlerFailures, Is.Zero);
            Assert.That(ForType(statistics, nameof(TestEvent)).PublishCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Упавший_обработчик_не_теряет_публикацию_и_растит_счётчик_ошибок()
    {
        _bus.Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("обработчик упал"));
        _bus.Subscribe<TestEvent>((_, _) => Task.CompletedTask);

        await _bus.PublishAsync(new TestEvent("падение"));

        var statistics = _metrics.Snapshot();
        var byType = ForType(statistics, nameof(TestEvent));

        Assert.Multiple(() =>
        {
            Assert.That(statistics.PublishedTotal, Is.EqualTo(1));
            Assert.That(statistics.HandlerFailures, Is.EqualTo(1));
            Assert.That(byType.PublishCount, Is.EqualTo(1));
            Assert.That(byType.HandlerFailureCount, Is.EqualTo(1));
            Assert.That(byType.LastPublishedAt, Is.GreaterThan(DateTimeOffset.MinValue));
        });
    }

    [Test]
    public async Task Продолжения_считаются_отдельно_от_обработчиков()
    {
        _bus.Subscribe<TestEvent>((_, _) =>
        {
            _bus.ContinueAfterPublish(() => Task.CompletedTask);
            _bus.ContinueAfterPublish(() => throw new InvalidOperationException("продолжение упало"));
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(new TestEvent("продолжения"));
        await WaitForAsync(() => _metrics.Snapshot().ContinuationFailures == 1);

        var statistics = _metrics.Snapshot();

        Assert.Multiple(() =>
        {
            Assert.That(statistics.PublishedTotal, Is.EqualTo(1));
            Assert.That(statistics.HandlerFailures, Is.Zero);
            Assert.That(statistics.ContinuationsStarted, Is.EqualTo(2));
            Assert.That(statistics.ContinuationFailures, Is.EqualTo(1));
            Assert.That(ForType(statistics, nameof(TestEvent)).HandlerFailureCount, Is.Zero);
        });
    }

    [Test]
    public void Время_последней_публикации_не_откатывается_назад()
    {
        var later = DateTimeOffset.UtcNow;
        var earlier = later - TimeSpan.FromSeconds(30);

        _metrics.RecordPublish(nameof(TestEvent), TimeSpan.FromMilliseconds(5), later);
        _metrics.RecordPublish(nameof(TestEvent), TimeSpan.FromMilliseconds(1), earlier);

        Assert.That(ForType(_metrics.Snapshot(), nameof(TestEvent)).LastPublishedAt, Is.EqualTo(later));
    }

    [TestCase(4, 50)]
    [TestCase(8, 100)]
    public async Task Счётчики_переживают_конкурентную_публикацию(int workers, int publicationsPerWorker)
    {
        _bus.Subscribe<TestEvent>((_, _) => Task.CompletedTask);
        _bus.Subscribe<OtherEvent>((_, _) => throw new InvalidOperationException("обработчик упал"));

        var publishers = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(async () =>
            {
                for (var index = 0; index < publicationsPerWorker; index++)
                {
                    await _bus.PublishAsync(new TestEvent("параллельно"));
                    await _bus.PublishAsync(new OtherEvent(index));
                }
            }))
            .ToArray();

        await Task.WhenAll(publishers);

        var statistics = _metrics.Snapshot();
        var expected = workers * publicationsPerWorker;

        Assert.Multiple(() =>
        {
            Assert.That(statistics.PublishedTotal, Is.EqualTo(expected * 2));
            Assert.That(statistics.HandlerFailures, Is.EqualTo(expected));
            Assert.That(ForType(statistics, nameof(TestEvent)).PublishCount, Is.EqualTo(expected));
            Assert.That(ForType(statistics, nameof(OtherEvent)).PublishCount, Is.EqualTo(expected));
            Assert.That(ForType(statistics, nameof(OtherEvent)).HandlerFailureCount, Is.EqualTo(expected));
            Assert.That(statistics.ByType, Has.Count.EqualTo(2));
        });
    }

    private static EventTypeStatistics ForType(EventBusStatistics statistics, string eventType)
    {
        var byType = statistics.ByType.SingleOrDefault(item => item.EventType == eventType);

        Assert.That(byType, Is.Not.Null, $"В снимке шины нет записи о событии {eventType}");

        return byType!;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Счётчики шины не сошлись за отведённое время");
    }

    private sealed record TestEvent(string Payload) : EventBase;

    private sealed record OtherEvent(int Value) : EventBase;
}
