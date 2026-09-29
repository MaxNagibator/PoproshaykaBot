using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Infrastructure.Events;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Events;

[TestFixture]
public sealed class EventSubscriberRegistrationExtensionsTests
{
    [Test]
    public void AddEventSubscribers_RegistersAllConcreteSubscribersFromAssembly()
    {
        var services = new ServiceCollection();

        services.AddEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(SampleSubscriber)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(AnotherSampleSubscriber)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(NonSubscriberClass)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(AbstractBaseSubscriber)), Is.False,
                "Abstract types must be skipped – they cannot be instantiated.");
        }
    }

    [Test]
    public void AddEventSubscribers_RegistersAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);

        var sampleDescriptor = services.Single(d => d.ServiceType == typeof(SampleSubscriber));

        Assert.That(sampleDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public void ActivateEventSubscribers_ResolvesEachSubscriber_TriggeringSubscriptionInCtor()
    {
        var bus = Substitute.For<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton(bus);
        services.AddEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);

        var provider = services.BuildServiceProvider();
        provider.ActivateEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);

        bus.Received().Subscribe(Arg.Any<IEventHandler<SampleEvent>>());
        bus.Received().Subscribe(Arg.Any<IEventHandler<AnotherEvent>>());
    }

    [Test]
    public void ActivateEventSubscribers_ResolvesEachSubscriberOnce_OnRepeatedCalls()
    {
        var bus = Substitute.For<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton(bus);
        services.AddEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);

        var provider = services.BuildServiceProvider();
        provider.ActivateEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);
        provider.ActivateEventSubscribers(typeof(EventSubscriberRegistrationExtensionsTests).Assembly);

        bus.Received(1).Subscribe(Arg.Any<IEventHandler<SampleEvent>>());
        bus.Received(1).Subscribe(Arg.Any<IEventHandler<AnotherEvent>>());
    }

    private sealed record SampleEvent(string Value) : EventBase;

    private sealed record AnotherEvent(int Value) : EventBase;

    private abstract class AbstractBaseSubscriber : IEventSubscriber;

    private sealed class SampleSubscriber : IEventHandler<SampleEvent>, IEventSubscriber
    {
        public SampleSubscriber(IEventBus eventBus)
        {
            eventBus.Subscribe(this);
        }

        public Task HandleAsync(SampleEvent @event, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherSampleSubscriber : IEventHandler<AnotherEvent>, IEventSubscriber
    {
        public AnotherSampleSubscriber(IEventBus eventBus)
        {
            eventBus.Subscribe(this);
        }

        public Task HandleAsync(AnotherEvent @event, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NonSubscriberClass;
}
