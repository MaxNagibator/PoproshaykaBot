using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Tests.Streaming;

[TestFixture]
public sealed class BotLifecycleAutomationHandlerTests
{
    private const string Channel = "streamer";

    [SetUp]
    public void SetUp()
    {
        _order = [];

        _settings = new()
        {
            Twitch =
            {
                Channel = Channel,
                BotLifecycleAutomation =
                {
                    AutoConnectOnStreamOnline = true,
                    AutoDisconnectOnStreamOffline = true,
                },
            },
        };

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(_settings);

        _connectionController = Substitute.For<IBotConnectionController>();

        _connectionController.StopAsync(Arg.Any<BotStopMode>()).Returns(_ =>
        {
            _order.Add("стоп бота");
            return Task.CompletedTask;
        });

        _connectionController.When(x => x.StartConnection()).Do(_ => _order.Add("старт бота"));

        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);

        _handler = new(_connectionController,
            _settingsManager,
            _eventBus,
            NullLogger<BotLifecycleAutomationHandler>.Instance);

        _continuationsDone = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _eventBus.Subscribe<StreamWentOffline>(async (_, _) =>
        {
            await Task.Yield();
            _order.Add("итоговая статистика поставлена в очередь");

            _eventBus.ContinueAfterPublish(() =>
            {
                _order.Add("проба продолжения");
                _continuationsDone.SetResult();
                return Task.CompletedTask;
            });
        });

        _eventBus.Subscribe<StreamWentOnline>((_, _) =>
        {
            _order.Add("поздний подписчик онлайна");
            return Task.CompletedTask;
        });
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
    }

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private List<string> _order = null!;
    private TaskCompletionSource _continuationsDone = null!;
    private AppSettings _settings = null!;
    private SettingsManager _settingsManager = null!;
    private IBotConnectionController _connectionController = null!;
    private InMemoryEventBus _eventBus = null!;
    private BotLifecycleAutomationHandler _handler = null!;

    [Test]
    public async Task StreamWentOffline_StopsBotAfterEveryOtherSubscriberOfThePublication()
    {
        await SetPhaseAsync(BotLifecyclePhase.Connected);

        await _eventBus.PublishAsync(new StreamWentOffline(Channel));
        await _continuationsDone.Task.WaitAsync(WaitTimeout);

        Assert.That(_order, Is.EqualTo(new[] { "итоговая статистика поставлена в очередь", "стоп бота", "проба продолжения" }),
            "Авто-стоп обгонит итоговую статистику, если запускать его прямо в обработчике.");

        await _connectionController.Received(1).StopAsync(BotStopMode.Graceful);
    }

    [Test]
    public async Task StreamWentOffline_PublishDoesNotWaitForTheStop()
    {
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _connectionController.StopAsync(Arg.Any<BotStopMode>()).Returns(async _ =>
        {
            stopStarted.SetResult();
            await releaseStop.Task;
        });

        await SetPhaseAsync(BotLifecyclePhase.Connected);

        await _eventBus.PublishAsync(new StreamWentOffline(Channel)).WaitAsync(WaitTimeout);
        await stopStarted.Task.WaitAsync(WaitTimeout);

        releaseStop.SetResult();
        await _continuationsDone.Task.WaitAsync(WaitTimeout);
    }

    [Test]
    public async Task StreamWentOffline_StopFailure_DoesNotBreakPublicationOrTheNextContinuation()
    {
        await SetPhaseAsync(BotLifecyclePhase.Connected);

        _connectionController.StopAsync(Arg.Any<BotStopMode>())
            .Returns(_ => Task.FromException(new InvalidOperationException("boom")));

        Assert.DoesNotThrowAsync(async () => await _eventBus.PublishAsync(new StreamWentOffline(Channel)));

        await _continuationsDone.Task.WaitAsync(WaitTimeout);

        Assert.That(_order, Is.EqualTo(new[] { "итоговая статистика поставлена в очередь", "проба продолжения" }));
    }

    [Test]
    public async Task StreamWentOffline_StreamBackOnlineBeforeTheContinuation_DoesNotStopBot()
    {
        await SetPhaseAsync(BotLifecyclePhase.Connected);

        using var backOnline = _eventBus.Subscribe<StreamWentOffline>((_, cancellationToken) =>
            _eventBus.PublishAsync(new StreamWentOnline(Channel, null), cancellationToken));

        await _eventBus.PublishAsync(new StreamWentOffline(Channel));
        await _continuationsDone.Task.WaitAsync(WaitTimeout);

        await _connectionController.DidNotReceive().StopAsync(Arg.Any<BotStopMode>());
        _connectionController.DidNotReceive().StartConnection();

        Assert.That(_order, Is.EqualTo(new[]
        {
            "итоговая статистика поставлена в очередь",
            "поздний подписчик онлайна",
            "проба продолжения",
        }));
    }

    [TestCase(false, BotLifecyclePhase.Connected, false, TestName = "Авто-отключение выключено настройкой")]
    [TestCase(true, BotLifecyclePhase.Connected, true, TestName = "Офлайн-снимок при подключении (catch-up)")]
    [TestCase(true, BotLifecyclePhase.Idle, false, TestName = "Бот ещё не подключался")]
    [TestCase(true, BotLifecyclePhase.Disconnected, false, TestName = "Бот уже отключён")]
    [TestCase(true, BotLifecyclePhase.Connecting, false, TestName = "Бот в процессе подключения")]
    public async Task StreamWentOffline_DoesNotStopBot(bool autoDisconnect, BotLifecyclePhase phase, bool isCatchUp)
    {
        _settings.Twitch.BotLifecycleAutomation.AutoDisconnectOnStreamOffline = autoDisconnect;
        await SetPhaseAsync(phase);

        await _eventBus.PublishAsync(new StreamWentOffline(Channel, isCatchUp));
        await _continuationsDone.Task.WaitAsync(WaitTimeout);

        await _connectionController.DidNotReceive().StopAsync(Arg.Any<BotStopMode>());
        Assert.That(_order, Is.EqualTo(new[] { "итоговая статистика поставлена в очередь", "проба продолжения" }));
    }

    [TestCase(BotLifecyclePhase.Idle)]
    [TestCase(BotLifecyclePhase.Disconnected)]
    [TestCase(BotLifecyclePhase.Cancelled)]
    [TestCase(BotLifecyclePhase.Failed)]
    public async Task StreamWentOnline_StartsConnectionFromDisconnectedPhase(BotLifecyclePhase phase)
    {
        await SetPhaseAsync(phase);

        await _eventBus.PublishAsync(new StreamWentOnline(Channel, null));

        _connectionController.Received(1).StartConnection();
    }

    [TestCase(false, BotLifecyclePhase.Idle, false, TestName = "Авто-подключение выключено настройкой")]
    [TestCase(true, BotLifecyclePhase.Connected, false, TestName = "Бот уже подключён")]
    [TestCase(true, BotLifecyclePhase.Idle, true, TestName = "Менеджер подключений занят")]
    public async Task StreamWentOnline_DoesNotStartConnection(bool autoConnect, BotLifecyclePhase phase, bool isBusy)
    {
        _settings.Twitch.BotLifecycleAutomation.AutoConnectOnStreamOnline = autoConnect;
        _connectionController.IsBusy.Returns(isBusy);
        await SetPhaseAsync(phase);

        await _eventBus.PublishAsync(new StreamWentOnline(Channel, null));

        _connectionController.DidNotReceive().StartConnection();
    }

    private Task SetPhaseAsync(BotLifecyclePhase phase)
    {
        return _eventBus.PublishAsync(new BotLifecyclePhaseChanged(phase));
    }
}
