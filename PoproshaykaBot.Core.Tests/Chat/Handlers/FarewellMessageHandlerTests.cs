using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Handlers;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Tests.Chat.Handlers;

[TestFixture]
public sealed class FarewellMessageHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        _channelProvider = Substitute.For<IChannelProvider>();
        _channelProvider.Channel.Returns("qp_illson");

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settings = new();
        _settingsManager.Current.Returns(_settings);

        _audienceTracker = new(_settingsManager);

        _messenger = Substitute.For<IChatMessenger>();
        _eventBus = Substitute.For<IEventBus>();
        _eventBus.Subscribe(Arg.Any<IEventHandler<BotLifecyclePhaseChanged>>()).Returns(Substitute.For<IDisposable>());

        _handler = new(_channelProvider,
            _audienceTracker,
            _messenger,
            _settingsManager,
            _eventBus,
            NullLogger<FarewellMessageHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
    }

    private IChannelProvider _channelProvider = null!;
    private SettingsManager _settingsManager = null!;
    private AudienceTracker _audienceTracker = null!;
    private IChatMessenger _messenger = null!;
    private IEventBus _eventBus = null!;
    private AppSettings _settings = null!;
    private FarewellMessageHandler _handler = null!;

    [Test]
    public async Task DoesNothingForNonDisconnectingPhase()
    {
        _settings.Twitch.Messages.FarewellEnabled = true;
        _settings.Twitch.Messages.DisconnectionEnabled = true;
        _audienceTracker.OnUserMessage("u1", "Alice");

        await _handler.HandleAsync(new(BotLifecyclePhase.Connected), CancellationToken.None);
        await _handler.HandleAsync(new(BotLifecyclePhase.Connecting), CancellationToken.None);
        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnected), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task SkipsAllMessagesWhenChannelIsUnknown()
    {
        _channelProvider.Channel.Returns((string?)null);
        _settings.Twitch.Messages.FarewellEnabled = true;
        _settings.Twitch.Messages.DisconnectionEnabled = true;
        _audienceTracker.OnUserMessage("u1", "Alice");

        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnecting), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task SendsCollectiveFarewellAndDisconnectionJoined()
    {
        _settings.Twitch.Messages.FarewellEnabled = true;
        _settings.Twitch.Messages.Farewell = "До свидания, {username}!";
        _settings.Twitch.Messages.DisconnectionEnabled = true;
        _settings.Twitch.Messages.Disconnection = "Пока-пока!";
        _audienceTracker.OnUserMessage("u1", "Alice");
        _audienceTracker.OnUserMessage("u2", "Bob");

        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnecting), CancellationToken.None);

        _messenger.Received(1).Send("До свидания, Alice, Bob! Пока-пока!");
    }

    [Test]
    public async Task SendsOnlyDisconnectionWhenFarewellIsDisabled()
    {
        _settings.Twitch.Messages.FarewellEnabled = false;
        _settings.Twitch.Messages.DisconnectionEnabled = true;
        _settings.Twitch.Messages.Disconnection = "Пока-пока!";
        _audienceTracker.OnUserMessage("u1", "Alice");

        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnecting), CancellationToken.None);

        _messenger.Received(1).Send("Пока-пока!");
    }

    [Test]
    public async Task SendsOnlyDisconnectionWhenAudienceTrackerIsEmpty()
    {
        _settings.Twitch.Messages.FarewellEnabled = true;
        _settings.Twitch.Messages.Farewell = "До свидания, {username}!";
        _settings.Twitch.Messages.DisconnectionEnabled = true;
        _settings.Twitch.Messages.Disconnection = "Пока-пока!";

        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnecting), CancellationToken.None);

        _messenger.Received(1).Send("Пока-пока!");
    }

    [Test]
    public async Task SendsOnlyCollectiveFarewellWhenDisconnectionDisabled()
    {
        _settings.Twitch.Messages.FarewellEnabled = true;
        _settings.Twitch.Messages.Farewell = "Спасибо, {username}!";
        _settings.Twitch.Messages.DisconnectionEnabled = false;
        _audienceTracker.OnUserMessage("u1", "Alice");

        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnecting), CancellationToken.None);

        _messenger.Received(1).Send("Спасибо, Alice!");
    }

    [Test]
    public async Task SendsNothingWhenBothMessagesAreDisabledAndTrackerEmpty()
    {
        _settings.Twitch.Messages.FarewellEnabled = false;
        _settings.Twitch.Messages.DisconnectionEnabled = false;

        await _handler.HandleAsync(new(BotLifecyclePhase.Disconnecting), CancellationToken.None);

        _messenger.DidNotReceive().Send(Arg.Any<string>());
    }
}
