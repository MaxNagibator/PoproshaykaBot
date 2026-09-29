using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Hosting;

[TestFixture]
public sealed class BotConnectionManagerTests
{
    public enum Interruption
    {
        None = 0,
        CancelDuringStart = 1,
        FailDuringStart = 2,
        CancelOnJoined = 3,
    }

    [TestCase(Interruption.CancelDuringStart, BotLifecyclePhase.Cancelled, new[] { "start:sender", "start:slow", "stop:sender" })]
    [TestCase(Interruption.FailDuringStart, BotLifecyclePhase.Failed, new[] { "start:sender", "start:slow", "stop:sender" })]
    [TestCase(Interruption.CancelOnJoined, BotLifecyclePhase.Cancelled, new[] { "start:sender", "start:slow", "stop:slow", "stop:sender" })]
    public async Task Connect_Interrupted_StopsStartedComponentsAndAllowsReconnect(Interruption interruption, BotLifecyclePhase interruptedPhase, string[] expectedTrace)
    {
        var trace = new List<string>();
        BotConnectionManager? manager = null;
        var active = interruption;

        var eventBus = Substitute.For<IEventBus>();
        eventBus.PublishAsync(Arg.Any<BotJoinedChannel>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (active == Interruption.CancelOnJoined)
                {
                    manager!.CancelConnection();
                    call.ArgAt<CancellationToken>(1).ThrowIfCancellationRequested();
                }

                return Task.CompletedTask;
            });

        var components = new IHostedComponent[]
        {
            new RecordingComponent("sender", 50, trace, () => { }),
            new RecordingComponent("slow", 200, trace, () =>
            {
                switch (active)
                {
                    case Interruption.CancelDuringStart:
                        manager!.CancelConnection();
                        break;

                    case Interruption.FailDuringStart:
                        throw new InvalidOperationException("сбой запуска компонента");
                }
            }),
        };

        var tokenService = Substitute.For<ITwitchOAuthService>();
        tokenService.GetAccessTokenAsync(TwitchOAuthRole.Bot, Arg.Any<CancellationToken>()).Returns("token");

        var targetChannel = Substitute.For<ITargetChannelProvider>();
        targetChannel.Current.Returns(new TargetChannelState("channel", "channel", false, true));

        manager = new(tokenService,
            targetChannel,
            null!,
            new(components, NullLogger<AppHost>.Instance),
            eventBus,
            NullLogger<BotConnectionManager>.Instance);

        manager.StartConnection();
        await manager.WaitForConnectionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(manager.CurrentPhase, Is.EqualTo(interruptedPhase));
            Assert.That(trace, Is.EqualTo(expectedTrace), "Прерванное подключение останавливает уже запущенные компоненты");
        });

        active = Interruption.None;
        trace.Clear();
        manager.StartConnection();
        await manager.WaitForConnectionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(manager.CurrentPhase, Is.EqualTo(BotLifecyclePhase.Connected));
            Assert.That(trace, Is.EqualTo(["start:sender", "start:slow"]));
        });
    }

    private sealed class RecordingComponent(string name, int order, List<string> trace, Action onStart) : IHostedComponent
    {
        public string Name { get; } = name;

        public int StartOrder { get; } = order;

        public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
        {
            trace.Add($"start:{Name}");
            onStart();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
        {
            trace.Add($"stop:{Name}");
            return Task.CompletedTask;
        }
    }
}
