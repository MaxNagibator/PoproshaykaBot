using NSubstitute.ExceptionExtensions;
using PoproshaykaBot.Core.Infrastructure.Events;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Events;

[TestFixture]
public sealed class InMemoryEventBusTests
{
    [SetUp]
    public void SetUp()
    {
        _bus = new(NullLogger<InMemoryEventBus>.Instance);
    }

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private InMemoryEventBus _bus = null!;

    [Test]
    public void PublishAsync_NoSubscribers_CompletesWithoutError()
    {
        Assert.DoesNotThrowAsync(() => _bus.PublishAsync(new TestEvent("hello")));
    }

    [Test]
    public async Task PublishAsync_DeliversEventToLambdaSubscriber()
    {
        TestEvent? received = null;

        _bus.Subscribe<TestEvent>((e, _) =>
        {
            received = e;
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(new TestEvent("hello"));

        Assert.That(received, Is.Not.Null);
        Assert.That(received!.Payload, Is.EqualTo("hello"));
    }

    [Test]
    public async Task PublishAsync_DeliversEventToHandlerImplementation()
    {
        var handler = new RecordingHandler();
        _bus.Subscribe(handler);

        await _bus.PublishAsync(new TestEvent("first"));
        await _bus.PublishAsync(new TestEvent("second"));

        Assert.That(handler.Received, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Received[0].Payload, Is.EqualTo("first"));
            Assert.That(handler.Received[1].Payload, Is.EqualTo("second"));
        }
    }

    [Test]
    public async Task PublishAsync_FansOutToMultipleSubscribers()
    {
        var first = new RecordingHandler();
        var second = new RecordingHandler();

        _bus.Subscribe(first);
        _bus.Subscribe(second);

        await _bus.PublishAsync(new TestEvent("broadcast"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Received, Has.Count.EqualTo(1));
            Assert.That(second.Received, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task PublishAsync_HandlerException_DoesNotPropagateOrBreakOtherHandlers()
    {
        var faultyCalls = 0;
        var goodHandler = new RecordingHandler();

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            faultyCalls++;
            throw new InvalidOperationException("boom");
        });

        _bus.Subscribe(goodHandler);

        Assert.DoesNotThrowAsync(async () => await _bus.PublishAsync(new TestEvent("survive")));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(faultyCalls, Is.EqualTo(1));
            Assert.That(goodHandler.Received, Has.Count.EqualTo(1), "Subsequent handler must still receive event after a faulty one.");
        }
    }

    [Test]
    public async Task Dispose_OfSubscription_StopsFurtherDelivery()
    {
        var handler = new RecordingHandler();
        var subscription = _bus.Subscribe(handler);

        await _bus.PublishAsync(new TestEvent("before"));

        subscription.Dispose();

        await _bus.PublishAsync(new TestEvent("after"));

        Assert.That(handler.Received, Has.Count.EqualTo(1));
        Assert.That(handler.Received[0].Payload, Is.EqualTo("before"));
    }

    [Test]
    public void Dispose_OfSubscription_IsIdempotent()
    {
        var handler = new RecordingHandler();
        var subscription = _bus.Subscribe(handler);

        subscription.Dispose();

        Assert.DoesNotThrow(subscription.Dispose);
    }

    [Test]
    public async Task Subscribe_DuringPublish_DoesNotAffectInFlightDelivery()
    {
        var lateHandler = new RecordingHandler();
        IDisposable? lateSubscription = null;

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            lateSubscription = _bus.Subscribe(lateHandler);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(new TestEvent("first"));

        Assert.That(lateHandler.Received, Is.Empty,
            "Subscriptions added mid-publish must not see the in-flight event (snapshot semantics).");

        await _bus.PublishAsync(new TestEvent("second"));

        Assert.That(lateHandler.Received, Has.Count.EqualTo(1));
        Assert.That(lateHandler.Received[0].Payload, Is.EqualTo("second"));

        lateSubscription?.Dispose();
    }

    [Test]
    public async Task PublishAsync_DifferentEventTypes_AreIsolated()
    {
        var aHandler = new RecordingHandler();
        var bHandler = new OtherEventRecordingHandler();

        _bus.Subscribe(aHandler);
        _bus.Subscribe(bHandler);

        await _bus.PublishAsync(new TestEvent("a"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(aHandler.Received, Has.Count.EqualTo(1));
            Assert.That(bHandler.Received, Is.Empty);
        }
    }

    [Test]
    public void Subscribe_NullHandler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _bus.Subscribe((IEventHandler<TestEvent>)null!));
        Assert.Throws<ArgumentNullException>(() => _bus.Subscribe((Func<TestEvent, CancellationToken, Task>)null!));
    }

    [Test]
    public async Task PublishAsync_InvokesMockedHandlerWithExactEventInstance()
    {
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        _bus.Subscribe(handler);

        var @event = new TestEvent("payload");
        await _bus.PublishAsync(@event);

        await handler.Received(1).HandleAsync(@event, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishAsync_PropagatesCancellationTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        _bus.Subscribe(handler);

        await _bus.PublishAsync(new TestEvent("x"), cts.Token);

        await handler.Received(1).HandleAsync(Arg.Any<TestEvent>(), cts.Token);
    }

    [Test]
    public async Task PublishAsync_AsyncHandlerThatThrows_DoesNotBlockOtherHandlers()
    {
        var faulty = Substitute.For<IEventHandler<TestEvent>>();
        faulty.HandleAsync(Arg.Any<TestEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("async boom"));

        var good = Substitute.For<IEventHandler<TestEvent>>();

        _bus.Subscribe(faulty);
        _bus.Subscribe(good);

        await _bus.PublishAsync(new TestEvent("x"));

        await good.Received(1).HandleAsync(Arg.Any<TestEvent>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Subscription_Dispose_StopsMockedHandlerInvocation()
    {
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        var subscription = _bus.Subscribe(handler);

        subscription.Dispose();
        await _bus.PublishAsync(new TestEvent("x"));

        await handler.DidNotReceive().HandleAsync(Arg.Any<TestEvent>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ContinueAfterPublish_FromHandler_RunsAfterEveryHandlerOfThePublication()
    {
        var order = new List<string>();
        var finished = CreateSignal();

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            order.Add("первый обработчик");

            _bus.ContinueAfterPublish(() =>
            {
                order.Add("продолжение");
                finished.SetResult();
                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        });

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            order.Add("второй обработчик");
            return Task.CompletedTask;
        });

        _bus.Subscribe<TestEvent>(async (_, _) =>
        {
            await Task.Yield();
            order.Add("третий обработчик");
        });

        await _bus.PublishAsync(new TestEvent("offline"));
        await finished.Task.WaitAsync(WaitTimeout);

        Assert.That(order, Is.EqualTo(new[] { "первый обработчик", "второй обработчик", "третий обработчик", "продолжение" }));
    }

    [Test]
    public async Task PublishAsync_DoesNotWaitForContinuations()
    {
        var started = CreateSignal();
        var release = CreateSignal();

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            _bus.ContinueAfterPublish(async () =>
            {
                started.SetResult();
                await release.Task;
            });

            return Task.CompletedTask;
        });

        await _bus.PublishAsync(new TestEvent("offline")).WaitAsync(WaitTimeout);
        await started.Task.WaitAsync(WaitTimeout);

        release.SetResult();
    }

    [Test]
    public void ContinueAfterPublish_OutsidePublish_RunsImmediately()
    {
        var executed = false;

        _bus.ContinueAfterPublish(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.That(executed, Is.True, "Вне публикации продолжение выполняется сразу, без ожидания события.");
    }

    [Test]
    public void ContinueAfterPublish_OutsidePublish_ExceptionIsSwallowed()
    {
        Assert.DoesNotThrow(() => _bus.ContinueAfterPublish(() => throw new InvalidOperationException("boom")));
    }

    [Test]
    public async Task ContinueAfterPublish_FaultyContinuation_DoesNotBreakTheNextOne()
    {
        var order = new List<string>();
        var finished = CreateSignal();

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            _bus.ContinueAfterPublish(() => throw new InvalidOperationException("boom"));

            _bus.ContinueAfterPublish(() =>
            {
                order.Add("второе продолжение");
                finished.SetResult();
                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        });

        Assert.DoesNotThrowAsync(async () => await _bus.PublishAsync(new TestEvent("offline")));

        await finished.Task.WaitAsync(WaitTimeout);

        Assert.That(order, Is.EqualTo(new[] { "второе продолжение" }));
    }

    [Test]
    public async Task ContinueAfterPublish_NestedPublish_RunsAtTheEndOfTheNestedPublication()
    {
        var nestedStarted = CreateSignal();
        var outerStarted = CreateSignal();
        var outerRanBeforeHandlerReturned = true;

        _bus.Subscribe<OtherEvent>((_, _) =>
        {
            _bus.ContinueAfterPublish(() =>
            {
                nestedStarted.SetResult();
                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        });

        _bus.Subscribe<TestEvent>(async (_, ct) =>
        {
            _bus.ContinueAfterPublish(() =>
            {
                outerStarted.SetResult();
                return Task.CompletedTask;
            });

            await _bus.PublishAsync(new OtherEvent(1), ct);
            await nestedStarted.Task.WaitAsync(WaitTimeout);
            outerRanBeforeHandlerReturned = outerStarted.Task.IsCompleted;
        });

        await _bus.PublishAsync(new TestEvent("offline"));
        await outerStarted.Task.WaitAsync(WaitTimeout);

        Assert.That(outerRanBeforeHandlerReturned, Is.False,
            "Продолжение вложенной публикации выполняется в конце вложенной, а внешнее ждёт возврата обработчиков внешней.");
    }

    [Test]
    public async Task ContinueAfterPublish_FromContinuationOfNestedPublish_DoesNotLeakIntoOuterPublication()
    {
        var chained = CreateSignal();
        var outerHandlerReturned = CreateSignal();

        _bus.Subscribe<OtherEvent>((_, _) =>
        {
            _bus.ContinueAfterPublish(() =>
            {
                _bus.ContinueAfterPublish(() =>
                {
                    chained.SetResult();
                    return Task.CompletedTask;
                });

                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        });

        _bus.Subscribe<TestEvent>(async (_, ct) =>
        {
            await _bus.PublishAsync(new OtherEvent(1), ct);
            await chained.Task.WaitAsync(WaitTimeout);
            outerHandlerReturned.SetResult();
        });

        await _bus.PublishAsync(new TestEvent("offline"));

        Assert.That(outerHandlerReturned.Task.IsCompleted, Is.True,
            "Продолжение, зарегистрированное из продолжения вложенной публикации, выполняется сразу, а не в конце внешней.");
    }

    [Test]
    public async Task ContinueAfterPublish_FromContextThatOutlivedItsPublication_RunsImmediately()
    {
        var gate = CreateSignal();
        var executed = false;
        Task? escaped = null;

        _bus.Subscribe<TestEvent>((_, _) =>
        {
            escaped = Task.Run(async () =>
            {
                await gate.Task;

                _bus.ContinueAfterPublish(() =>
                {
                    executed = true;
                    return Task.CompletedTask;
                });
            });

            return Task.CompletedTask;
        });

        await _bus.PublishAsync(new TestEvent("offline"));

        gate.SetResult();
        await escaped!.WaitAsync(WaitTimeout);

        Assert.That(executed, Is.True, "Продолжение из контекста, пережившего свою публикацию, теряться не должно.");
    }

    [Test]
    public void ContinueAfterPublish_NullContinuation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _bus.ContinueAfterPublish(null!));
    }

    private static TaskCompletionSource CreateSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed record TestEvent(string Payload) : EventBase;

    public sealed record OtherEvent(int Value) : EventBase;

    public sealed class RecordingHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = [];

        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    public sealed class OtherEventRecordingHandler : IEventHandler<OtherEvent>
    {
        public List<OtherEvent> Received { get; } = [];

        public Task HandleAsync(OtherEvent @event, CancellationToken cancellationToken)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }
}
