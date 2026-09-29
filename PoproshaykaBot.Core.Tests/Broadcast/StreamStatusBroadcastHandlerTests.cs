using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Tests.Broadcast;

[TestFixture]
public sealed class StreamStatusBroadcastHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        _messenger = Substitute.For<IChatMessenger>();
        _scheduler = Substitute.For<IBroadcastScheduler>();
        _streamStatus = Substitute.For<IStreamStatus>();

        _settings = new()
        {
            Twitch =
            {
                Channel = Channel,
                AutoBroadcast =
                {
                    AutoBroadcastEnabled = true,
                    BroadcastIntervalMinutes = 15,
                    StreamStatusNotificationsEnabled = true,
                    StreamStartMessage = "Стрим начался",
                    StreamStopMessage = "Стрим закончился",
                },
            },
        };

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);

        _settingsManager.Current.Returns(_settings);

        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);

        _targetChannel = new(Channel);

        _handler = new(_scheduler,
            _messenger,
            _streamStatus,
            _targetChannel,
            _settingsManager,
            _eventBus,
            NullLogger<StreamStatusBroadcastHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
    }

    private const string Channel = "test-channel";

    private IChatMessenger _messenger = null!;
    private IBroadcastScheduler _scheduler = null!;
    private IStreamStatus _streamStatus = null!;
    private FakeTargetChannelProvider _targetChannel = null!;
    private SettingsManager _settingsManager = null!;
    private AppSettings _settings = null!;
    private InMemoryEventBus _eventBus = null!;
    private StreamStatusBroadcastHandler _handler = null!;

    [Test]
    public async Task StreamWentOnline_BotDisconnected_DoesNotStartScheduler()
    {
        _scheduler.IsActive.Returns(false);

        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);

        _scheduler.DidNotReceive().Start(Arg.Any<string>());
        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task StreamWentOnline_AfterBotConnected_StartsSchedulerAndSendsStartMessage()
    {
        _scheduler.IsActive.Returns(false);

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);
        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);

        _scheduler.Received(1).Start(Channel);
        _messenger.Received(1).Send("Стрим начался");
    }

    [Test]
    public async Task BotConnects_WhileStreamIsOnline_StartsSchedulerOnConnect()
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);

        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);
        _scheduler.DidNotReceive().Start(Arg.Any<string>());

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _scheduler.Received(1).Start(Channel);
    }

    [Test]
    public async Task BotConnects_WhileStreamIsOffline_DoesNotStartScheduler()
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Offline);

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _scheduler.DidNotReceive().Start(Arg.Any<string>());
    }

    [Test]
    public async Task StreamWentOffline_BotDisconnected_DoesNotSendStopMessage()
    {
        _scheduler.IsActive.Returns(false);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task StreamWentOffline_AfterBotConnected_StopsSchedulerAndSendsMessage()
    {
        _scheduler.IsActive.Returns(true);

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        _scheduler.Received(1).Stop();
        _messenger.Received(1).Send("Стрим закончился");
    }

    [Test]
    public async Task StreamWentOffline_WithIsCatchUpTrue_DoesNotSendStopMessage()
    {
        _scheduler.IsActive.Returns(true);

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);
        await _handler.HandleAsync(new StreamWentOffline(Channel, true), CancellationToken.None);

        _scheduler.DidNotReceive().Stop();
        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task StreamWentOnline_WithIsCatchUpTrue_StartsSchedulerButDoesNotSendStartMessage()
    {
        _scheduler.IsActive.Returns(false);

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);
        await _handler.HandleAsync(new StreamWentOnline(Channel, null, true), CancellationToken.None);

        _scheduler.Received(1).Start(Channel);
        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task BotConnects_WhileStreamIsOnline_DoesNotSendStartMessage()
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _scheduler.Received(1).Start(Channel);
        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task StreamWentOnline_BotDisconnected_SendsStartMessageOnceOnConnect()
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);

        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _messenger.Received(1).Send("Стрим начался");

        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Disconnected), CancellationToken.None);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _messenger.Received(1).Send("Стрим начался");
    }

    [Test]
    public async Task StreamWentOnline_StatusUnknownOnConnect_DropsPendingStartNotification()
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);

        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);

        _streamStatus.CurrentStatus.Returns(StreamStatus.Unknown);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());

        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Disconnected), CancellationToken.None);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task StreamWentOnline_BotDisconnectedWithCatchUp_DoesNotSendStartMessageOnConnect()
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);

        await _handler.HandleAsync(new StreamWentOnline(Channel, null, true), CancellationToken.None);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _scheduler.Received(1).Start(Channel);
        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StreamWentOffline_BotDisconnected_DropsPendingStartNotification(bool isCatchUpOffline)
    {
        _scheduler.IsActive.Returns(false);
        _streamStatus.CurrentStatus.Returns(StreamStatus.Online);

        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);
        await _handler.HandleAsync(new StreamWentOffline(Channel, isCatchUpOffline), CancellationToken.None);
        await _handler.HandleAsync(new BotLifecyclePhaseChanged(BotLifecyclePhase.Connected), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }
}
