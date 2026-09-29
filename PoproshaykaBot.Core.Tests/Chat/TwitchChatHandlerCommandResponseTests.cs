using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Tests.Chat;

[TestFixture]
public sealed class TwitchChatHandlerCommandResponseTests
{
    private const string SentinelText = "маячок";
    private const string WelcomeText = "добро пожаловать";
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-chat-handler-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _sentTexts = [];
        _helix = Substitute.For<ITwitchHelixClient>();

        _helix.SendChatMessageAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                lock (_sentTexts)
                {
                    _sentTexts.Add(call.ArgAt<string>(2));
                    return Task.FromResult<string?>($"msg-{_sentTexts.Count}");
                }
            });

        _whispers = [];

        _helix.SendWhisperAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                lock (_whispers)
                {
                    _whispers.Add(call.ArgAt<string>(2));
                }

                return Task.CompletedTask;
            });

        var broadcasterIdProvider = Substitute.For<IBroadcasterIdProvider>();
        var botUserIdProvider = Substitute.For<IBotUserIdProvider>();
        broadcasterIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns("1");
        botUserIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns("2");

        _time = new()
        {
            UtcNow = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
        };

        _tracker = new(_time);
        _bus = new(NullLogger<InMemoryEventBus>.Instance);
        _collector = new();
        _bus.Subscribe(_collector);

        _sender = new(_helix,
            broadcasterIdProvider,
            botUserIdProvider,
            new FakeTargetChannelProvider(),
            _tracker,
            NullLogger<ChatSender>.Instance);

        _messenger = new(_sender);
        _commandSettingsStore = new(null, Path.Combine(_root, "commands.json"));

        _settings = new();
        _settings.Twitch.Messages.WelcomeEnabled = false;

        var settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        settingsManager.Current.Returns(_settings);

        var obsChatStore = new ObsChatStore(_bus,
            NullLogger<ObsChatStore>.Instance,
            Path.Combine(_root, "obs-chat.json"));

        _command = new();
        _accountsStore = new(null, Path.Combine(_root, "accounts.json"));

        _accountsStore.Mutate(TwitchOAuthRole.Bot, account =>
            account.StoredScopes = [..TwitchScopes.BotRequired, ..TwitchScopes.BotOptional]);

        _handlerLogger = new();

        var processor = new ChatCommandProcessor([_command],
            _commandSettingsStore,
            new(_time),
            NullLogger<ChatCommandProcessor>.Instance);

        _handler = new(settingsManager,
            obsChatStore,
            _commandSettingsStore,
            _accountsStore,
            new AudienceTracker(settingsManager),
            new ChatDecorationsProvider(_helix),
            processor,
            _tracker,
            _messenger,
            _bus,
            _handlerLogger);
    }

    [TearDown]
    public async Task TearDown()
    {
        _handler.Dispose();
        await _sender.StopAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private string _root = null!;
    private AppSettings _settings = null!;
    private List<string> _sentTexts = null!;
    private List<string> _whispers = null!;
    private ITwitchHelixClient _helix = null!;
    private TestTimeProvider _time = null!;
    private CommandResponseTracker _tracker = null!;
    private InMemoryEventBus _bus = null!;
    private ChatMessageCollector _collector = null!;
    private ChatSender _sender = null!;
    private TwitchChatMessenger _messenger = null!;
    private CommandSettingsStore _commandSettingsStore = null!;
    private FakeCommand _command = null!;
    private AccountsStore _accountsStore = null!;
    private TwitchChatHandler _handler = null!;
    private RecordingLogger<TwitchChatHandler> _handlerLogger = null!;

    [Test]
    public async Task Цель_чат_отправляет_ответ_в_твич_и_синтетического_сообщения_не_публикует()
    {
        SetTarget(CommandResponseTarget.Chat);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { FakeCommand.ResponseText, SentinelText }));
            Assert.That(_collector.Events.Count(x => x.IsBot), Is.Zero, "Ответ в чат виден только эхом EventSub");
        });
    }

    [Test]
    public async Task Цель_оверлей_в_твич_не_пишет_и_публикует_ответ_сама()
    {
        SetTarget(CommandResponseTarget.Overlay);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        var published = _collector.Events.SingleOrDefault(x => x.IsBot);

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { SentinelText }), "В чат Twitch ответ уходить не должен");
            Assert.That(published, Is.Not.Null);
            Assert.That(published!.Text, Is.EqualTo(FakeCommand.ResponseText));
            Assert.That(published.CommandResponse, Is.EqualTo(new CommandResponseMark("ранг", CommandResponseTarget.Overlay)));
            Assert.That(published.HistoryEntry.MessageType, Is.EqualTo(ChatMessageType.BotResponse));
        });
    }

    [Test]
    public async Task Цель_молча_не_отправляет_ответ_никуда()
    {
        SetTarget(CommandResponseTarget.None);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { SentinelText }));
            Assert.That(_collector.Events.Count(x => x.IsBot), Is.Zero);
        });
    }

    [Test]
    public async Task Эхо_ответа_команды_помечается_по_message_id_а_чужое_эхо_нет()
    {
        SetTarget(CommandResponseTarget.Chat | CommandResponseTarget.Overlay);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _handler.HandleAsync(CreateBotEcho("msg-1", FakeCommand.ResponseText), CancellationToken.None);
        await _handler.HandleAsync(CreateBotEcho("msg-2", SentinelText), CancellationToken.None);

        await WaitForAsync(() => _collector.Events.Count(x => x.IsBot) == 2);

        var echoes = _collector.Events.Where(x => x.IsBot).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(echoes[0].CommandResponse,
                Is.EqualTo(new CommandResponseMark("ранг", CommandResponseTarget.Chat | CommandResponseTarget.Overlay)));

            Assert.That(echoes[1].CommandResponse, Is.Null, "Обычное сообщение бота ответом команды не считается");
        });
    }

    [Test]
    public async Task Ответ_реплаем_уходит_на_исходное_сообщение_и_его_эхо_помечено()
    {
        SetTarget(CommandResponseTarget.Chat | CommandResponseTarget.Overlay);
        _command.RepliesToSender = true;

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.Received()
            .SendChatMessageAsync("1", "2", FakeCommand.ResponseText, "incoming-1", Arg.Any<CancellationToken>());

        await _handler.HandleAsync(CreateBotEcho("msg-1", FakeCommand.ResponseText), CancellationToken.None);
        await WaitForAsync(() => _collector.Events.Any(x => x.IsBot));

        Assert.That(_collector.Events.First(x => x.IsBot).CommandResponse,
            Is.EqualTo(new CommandResponseMark("ранг", CommandResponseTarget.Chat | CommandResponseTarget.Overlay)),
            "Ответ реплаем идёт тем же путём message_id, что и обычный, – пометка обязана встать.");
    }

    [Test]
    public async Task Цель_ответом_на_сообщение_шлёт_реплай_даже_команде_отвечающей_обычным_сообщением()
    {
        SetTarget(CommandSettings.CallerOnly);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.Received()
            .SendChatMessageAsync("1", "2", FakeCommand.ResponseText, "incoming-1", Arg.Any<CancellationToken>());

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { FakeCommand.ResponseText, SentinelText }),
                "Отдельной строкой ответ не дублируется");

            Assert.That(_collector.Events.Count(x => x.IsBot), Is.Zero, "Ответ в чат виден только эхом EventSub");
        });
    }

    [Test]
    public async Task Эхо_позже_срока_приходит_без_пометки()
    {
        SetTarget(CommandResponseTarget.Chat);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        _time.UtcNow += CommandResponseTracker.Lifetime + TimeSpan.FromSeconds(1);

        await _handler.HandleAsync(CreateBotEcho("msg-1", FakeCommand.ResponseText), CancellationToken.None);
        await WaitForAsync(() => _collector.Events.Any(x => x.IsBot));

        Assert.That(_collector.Events.Single(x => x.IsBot).CommandResponse, Is.Null);
    }

    [TestCase(CommandResponseTarget.Overlay, new[] { FakeCommand.ResponseText })]
    [TestCase(CommandResponseTarget.None, new string[0])]
    public async Task Приветствие_новичка_уходит_в_чат_когда_ответ_команды_идёт_мимо_чата(CommandResponseTarget target, string[] expectedPublished)
    {
        EnableWelcome();
        SetTarget(target);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.Received()
            .SendChatMessageAsync("1", "2", WelcomeText, "incoming-1", Arg.Any<CancellationToken>());

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { WelcomeText, SentinelText }),
                "В чат уходит одно приветствие, ответ команды – только по своей цели");

            Assert.That(_collector.Events.Where(x => x.IsBot).Select(x => x.Text), Is.EqualTo(expectedPublished),
                "Приветствие не сливается с ответом, который в чат не идёт");
        });
    }

    [TestCase(CommandResponseTarget.Chat, false, null)]
    [TestCase(CommandResponseTarget.Chat, true, "incoming-1")]
    [TestCase(CommandSettings.CallerOnly, false, "incoming-1")]
    [TestCase(CommandResponseTarget.Chat | CommandResponseTarget.Overlay, false, null)]
    public async Task Приветствие_новичка_сливается_с_ответом_который_идёт_в_чат(CommandResponseTarget target, bool repliesToSender, string? replyTo)
    {
        EnableWelcome();
        SetTarget(target);
        _command.RepliesToSender = repliesToSender;

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        var merged = $"{WelcomeText} {FakeCommand.ResponseText}";

        await _helix.Received()
            .SendChatMessageAsync("1", "2", merged, replyTo, Arg.Any<CancellationToken>());

        Assert.That(SentTexts(), Is.EqualTo(new[] { merged, SentinelText }));
    }

    [Test]
    public async Task Приветствие_новичка_уходит_в_чат_когда_команда_ничего_не_ответила()
    {
        EnableWelcome();
        SetTarget(CommandResponseTarget.Chat);
        _command.ReturnsNothing = true;

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.Received()
            .SendChatMessageAsync("1", "2", WelcomeText, "incoming-1", Arg.Any<CancellationToken>());

        Assert.That(SentTexts(), Is.EqualTo(new[] { WelcomeText, SentinelText }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Цель_шёпотом_отвечает_лично_а_в_чате_и_оверлее_ответа_нет(bool repliesToSender)
    {
        SetTarget(CommandSettings.WhisperToCaller);
        _command.RepliesToSender = repliesToSender;

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.Received(1).SendWhisperAsync("2", "u1", FakeCommand.ResponseText, Arg.Any<CancellationToken>());

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { SentinelText }), "В чат Twitch ответ не уходит");
            Assert.That(_collector.Events.Count(x => x.IsBot), Is.Zero, "Шёпот не публикуется ни в чат приложения, ни в оверлей");
        });
    }

    [Test]
    public async Task Бот_без_права_на_шёпот_отвечает_реплаем_не_спрашивая_Twitch()
    {
        _accountsStore.Mutate(TwitchOAuthRole.Bot, account =>
            account.StoredScopes = [TwitchScopes.UserReadChat, TwitchScopes.UserWriteChat, TwitchScopes.UserBot]);

        SetTarget(CommandSettings.WhisperToCaller);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.DidNotReceiveWithAnyArgs().SendWhisperAsync(default!, default!, default!, default);
        await _helix.Received(1).SendChatMessageAsync("1", "2", FakeCommand.ResponseText, "incoming-1", Arg.Any<CancellationToken>());

        await _handler.HandleAsync(CreateBotEcho("msg-1", FakeCommand.ResponseText), CancellationToken.None);
        await WaitForAsync(() => _collector.Events.Any(x => x.IsBot));

        var mark = _collector.Events.First(x => x.IsBot).CommandResponse;

        Assert.Multiple(() =>
        {
            Assert.That(mark, Is.EqualTo(new CommandResponseMark("ранг", CommandSettings.WhisperToCaller)),
                "Эхо отката опознаётся как ответ команды");

            Assert.That(mark!.Target.GoesToOverlay(), Is.False, "Откат шёпота в оверлей не попадает");
        });
    }

    [Test]
    public async Task Право_вписанное_но_не_выданное_шёпота_не_даёт_а_отказ_предупреждает_один_раз()
    {
        _accountsStore.Mutate(TwitchOAuthRole.Bot, account =>
        {
            account.Scopes = [..TwitchScopes.BotRequired, ..TwitchScopes.BotOptional];
            account.StoredScopes = [];
        });

        SetTarget(CommandSettings.WhisperToCaller);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.DidNotReceiveWithAnyArgs().SendWhisperAsync(default!, default!, default!, default);

        var fallbacks = _handlerLogger.Entries
            .Where(entry => entry.Message.Contains(TwitchScopes.UserManageWhispers, StringComparison.Ordinal))
            .Select(entry => entry.Level)
            .ToArray();

        Assert.That(fallbacks, Is.EqualTo(new[] { LogLevel.Warning, LogLevel.Debug }),
            "Первый откат – предупреждение, повтор – Debug");
    }

    [Test]
    public async Task Приветствие_новичка_уходит_в_чат_а_ответ_шёпотом_без_него()
    {
        EnableWelcome();
        SetTarget(CommandSettings.WhisperToCaller);

        await _sender.StartAsync(new Progress<string>(), CancellationToken.None);
        await _handler.HandleAsync(CreateRaw("!ранг"), CancellationToken.None);

        await SendSentinelAsync();

        await _helix.Received(1)
            .SendChatMessageAsync("1", "2", WelcomeText, "incoming-1", Arg.Any<CancellationToken>());

        Assert.Multiple(() =>
        {
            Assert.That(SentTexts(), Is.EqualTo(new[] { WelcomeText, SentinelText }));
            Assert.That(Whispers(), Is.EqualTo(new[] { FakeCommand.ResponseText }), "Приветствие с личным ответом не сливается");
        });
    }

    private string[] Whispers()
    {
        lock (_whispers)
        {
            return _whispers.ToArray();
        }
    }

    private void EnableWelcome()
    {
        _settings.Twitch.Messages.WelcomeEnabled = true;
        _settings.Twitch.Messages.Welcome = WelcomeText;
    }

    private static RawChatMessageReceived CreateRaw(string text)
    {
        return new(new("test-channel",
                "incoming-1",
                "u1",
                "alice",
                "Алиса",
                text,
                [],
                [],
                false,
                false,
                false,
                false),
            DateTimeOffset.UtcNow);
    }

    private static RawChatMessageReceived CreateBotEcho(string messageId, string text)
    {
        return new(new("test-channel",
                messageId,
                "2",
                "bot",
                "Бот",
                text,
                [],
                [],
                false,
                false,
                false,
                false,
                true),
            DateTimeOffset.UtcNow);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.That(condition(), Is.True, "Событие не пришло за отведённое время");
    }

    private void SetTarget(CommandResponseTarget target)
    {
        _commandSettingsStore.Mutate(settings => settings.Commands["ранг"] = new()
        {
            ResponseTarget = target,
        });
    }

    private string[] SentTexts()
    {
        lock (_sentTexts)
        {
            return _sentTexts.ToArray();
        }
    }

    private async Task SendSentinelAsync()
    {
        _messenger.Send(SentinelText);

        await WaitForAsync(() => SentTexts().Contains(SentinelText, StringComparer.Ordinal));
    }

    private sealed class FakeCommand : IChatCommand
    {
        public const string ResponseText = "твой ранг – первый";

        public string Canonical => "ранг";

        public IReadOnlyCollection<string> Aliases => [];

        public string Description => "ранг пользователя";

        public bool CanExecute(CommandContext context)
        {
            return true;
        }

        public bool RepliesToSender { get; set; }

        public bool ReturnsNothing { get; set; }

        public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            if (ReturnsNothing)
            {
                return Task.FromResult<OutgoingMessage?>(null);
            }

            return Task.FromResult<OutgoingMessage?>(RepliesToSender
                ? OutgoingMessage.Reply(ResponseText, context.MessageId)
                : OutgoingMessage.Normal(ResponseText));
        }
    }

    private sealed class ChatMessageCollector : IEventHandler<ChatMessageReceived>
    {
        public List<ChatMessageReceived> Events { get; } = [];

        public Task HandleAsync(ChatMessageReceived @event, CancellationToken cancellationToken)
        {
            lock (Events)
            {
                Events.Add(@event);
            }

            return Task.CompletedTask;
        }
    }
}
