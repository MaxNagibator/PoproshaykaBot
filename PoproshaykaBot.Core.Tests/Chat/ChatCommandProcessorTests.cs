using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Tests.Server;

namespace PoproshaykaBot.Core.Tests.Chat;

[TestFixture]
public sealed class ChatCommandProcessorTests
{
    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-commands-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _settingsStore = new(null, Path.Combine(_root, "commands.json"));

        _time = new()
        {
            UtcNow = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
        };

        _usage = new(_time);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private string _root = null!;
    private CommandSettingsStore _settingsStore = null!;
    private TestTimeProvider _time = null!;
    private CommandUsageRepository _usage = null!;

    [Test]
    public async Task Выключенная_команда_не_выполняется_и_не_пишется_в_файл_неизвестных()
    {
        var command = new FakeCommand("ранг");
        var processor = CreateProcessor(command);

        _settingsStore.Mutate(settings => settings.Commands["ранг"] = new()
        {
            Enabled = false,
        });

        var unknownFile = AppPaths.Combine("unknown_commands.txt");
        var sizeBefore = File.Exists(unknownFile) ? new FileInfo(unknownFile).Length : 0;

        var result = await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        var sizeAfter = File.Exists(unknownFile) ? new FileInfo(unknownFile).Length : 0;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCommand, Is.False);
            Assert.That(command.Calls, Is.Zero);
            Assert.That(sizeAfter, Is.EqualTo(sizeBefore), "Выключенная команда – не неизвестная, в файл она не пишется");
            Assert.That(_usage.GetSnapshot(), Is.Empty);
        });
    }

    [TestCase("?", "?ранг", true)]
    [TestCase("?", "!ранг", false)]
    [TestCase("  ", "!ранг", true)]
    public async Task Префикс_разбора_виден_снаружи_и_решает_судьбу_сообщения(string prefix, string message, bool handled)
    {
        var command = new FakeCommand("ранг");
        var processor = new ChatCommandProcessor(
            [command],
            _settingsStore,
            _usage,
            NullLogger<ChatCommandProcessor>.Instance,
            prefix);

        var result = await processor.TryProcessAsync(message, CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processor.Prefix, Is.EqualTo(string.IsNullOrWhiteSpace(prefix) ? ChatCommandProcessor.DefaultPrefix : prefix));
            Assert.That(message.StartsWith(processor.Prefix, StringComparison.Ordinal), Is.EqualTo(handled));
            Assert.That(result.IsCommand, Is.EqualTo(handled));
        });
    }

    [Test]
    public void Ограничение_особым_списком_объявляет_команда_а_не_хост()
    {
        Assert.Multiple(() =>
        {
            Assert.That(((IChatCommand)new FakeCommand("ранг")).IsRestrictedToAllowedUsers, Is.False, "Обычная команда особым списком не ограничена");
            Assert.That(
                new TrumpCommand(new(NullLogger<SettingsManager>.Instance, Path.Combine(_root, "settings.json"))).IsRestrictedToAllowedUsers,
                Is.True);
        });
    }

    [Test]
    public async Task Включение_команды_обратно_действует_без_перезапуска()
    {
        var command = new FakeCommand("ранг");
        var processor = CreateProcessor(command);

        _settingsStore.Mutate(settings => settings.Commands["ранг"] = new()
        {
            Enabled = false,
        });

        await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        _settingsStore.Mutate(settings => settings.Commands["ранг"].Enabled = true);

        var result = await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCommand, Is.True);
            Assert.That(command.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Успешный_вызов_растит_счётчик_и_запоминает_последнего()
    {
        var processor = CreateProcessor(new FakeCommand("ранг"));

        await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);
        await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        var record = _usage.GetSnapshot().Single();

        Assert.Multiple(() =>
        {
            Assert.That(record.Canonical, Is.EqualTo("ранг"));
            Assert.That(record.TotalCount, Is.EqualTo(2));
            Assert.That(record.StreamCount, Is.EqualTo(2));
            Assert.That(record.LastUsedBy, Is.EqualTo("Алиса"));
            Assert.That(record.LastUsedAt, Is.EqualTo(_time.UtcNow));
        });
    }

    [Test]
    public async Task Упавшая_команда_в_счётчик_не_попадает_и_отличима_от_успеха_без_ответа()
    {
        var processor = CreateProcessor(new FakeCommand("ранг")
        {
            Throws = true,
        });

        var result = await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCommand, Is.True);
            Assert.That(result.Executed, Is.False);
            Assert.That(result.Canonical, Is.EqualTo("ранг"));
            Assert.That(_usage.GetSnapshot(), Is.Empty);
        });
    }

    [Test]
    public async Task Успех_без_ответа_считается_выполнением()
    {
        var processor = CreateProcessor(new FakeCommand("ранг")
        {
            Response = null,
        });

        var result = await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Executed, Is.True);
            Assert.That(result.Response, Is.Null);
            Assert.That(_usage.GetSnapshot().Single().TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Выключенная_команда_не_попадает_в_список_доступных()
    {
        var processor = CreateProcessor(new FakeCommand("ранг"), new FakeCommand("донат"));

        _settingsStore.Mutate(settings => settings.Commands["донат"] = new()
        {
            Enabled = false,
        });

        var enabled = processor.GetEnabledCommands().Select(x => x.Canonical).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(enabled, Does.Contain("ранг"));
            Assert.That(enabled, Does.Not.Contain("донат"));
            Assert.That(processor.GetAllCommands(), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Помощь_перечисляет_только_включённые_команды()
    {
        var processor = CreateProcessor(new FakeCommand("ранг"), new FakeCommand("донат"));
        processor.Register(new HelpCommand(processor.GetEnabledCommands, processor.Prefix));

        _settingsStore.Mutate(settings => settings.Commands["донат"] = new()
        {
            Enabled = false,
        });

        var result = await processor.TryProcessAsync("!помощь", CreateContext(), CancellationToken.None);

        Assert.That(result.Response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Response!.Text, Does.Contain("!ранг"));
            Assert.That(result.Response.Text, Does.Not.Contain("!донат"));
        });
    }

    [TestCase("!", "!помощь ранг")]
    [TestCase("!", "!помощь !ранг")]
    [TestCase("?", "?помощь ранг")]
    [TestCase("?", "?помощь ?ранг")]
    public async Task Помощь_печатает_имена_префиксом_процессора(string prefix, string message)
    {
        var processor = new ChatCommandProcessor(
            [new FakeCommand("ранг")],
            _settingsStore,
            _usage,
            NullLogger<ChatCommandProcessor>.Instance,
            prefix);

        processor.Register(new HelpCommand(processor.GetEnabledCommands, processor.Prefix));

        var list = await processor.TryProcessAsync($"{prefix}помощь", CreateContext(), CancellationToken.None);
        var about = await processor.TryProcessAsync(message, CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(list.Response!.Text, Does.Contain($"{prefix}ранг"));
            Assert.That(about.Response!.Text, Does.Contain($"{prefix}ранг: тестовая команда"), "Аргумент разбирается с префиксом и без него");
            Assert.That(
                list.Response.Text.Contains("!ранг", StringComparison.Ordinal),
                Is.EqualTo(prefix == ChatCommandProcessor.DefaultPrefix),
                "При нестандартном префиксе решётки в выводе помощи не остаётся");
        });
    }

    [Test]
    public async Task Настройка_прав_отнимает_команду_у_зрителя_и_снятие_возвращает_её()
    {
        var command = new FakeCommand("ранг");
        var processor = CreateProcessor(command);

        _settingsStore.Mutate(settings => settings.Commands["ранг"] = new()
        {
            Access = CommandAccessLevel.Moderators,
        });

        var refused = await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);
        var allowedToModerator = await processor.TryProcessAsync("!ранг", CreateContext(isModerator: true), CancellationToken.None);

        _settingsStore.Mutate(settings => settings.Commands["ранг"].Access = null);

        var returned = await processor.TryProcessAsync("!ранг", CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(refused.IsCommand, Is.False, "Отказ по правам выглядит так же, как отказ CanExecute");
            Assert.That(allowedToModerator.IsCommand, Is.True);
            Assert.That(returned.IsCommand, Is.True, "Снятое право возвращается без перезапуска");
            Assert.That(command.Calls, Is.EqualTo(2));
            Assert.That(_usage.GetSnapshot().Single().TotalCount, Is.EqualTo(2), "Отказ по правам вызовом не считается");
        });
    }

    [TestCase(null)]
    [TestCase(CommandAccessLevel.Everyone)]
    [TestCase(CommandAccessLevel.Moderators)]
    [TestCase(CommandAccessLevel.Broadcaster)]
    [TestCase((CommandAccessLevel)(-3))]
    [TestCase((CommandAccessLevel)99)]
    public async Task Зрителю_и_модератору_нельзя_выдать_команду_стримера_никакой_настройкой(CommandAccessLevel? access)
    {
        var command = new FakeCommand("название")
        {
            BroadcasterOnly = true,
        };

        var processor = CreateProcessor(command);

        _settingsStore.Mutate(settings => settings.Commands["название"] = new()
        {
            Access = access,
        });

        var viewer = await processor.TryProcessAsync("!название", CreateContext(), CancellationToken.None);
        var moderator = await processor.TryProcessAsync("!название", CreateContext(isModerator: true), CancellationToken.None);
        var broadcaster = await processor.TryProcessAsync("!название", CreateContext(isBroadcaster: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(viewer.IsCommand, Is.False, "Настройка прав сужает доступ, а не расширяет его");
            Assert.That(moderator.IsCommand, Is.False);
            Assert.That(broadcaster.IsCommand, Is.True,
                "Строже стримера настройка стать не может, поэтому владелец канала команду сохраняет");

            Assert.That(command.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Настройка_сужает_доступ_поверх_ограничения_в_коде()
    {
        var command = new FakeCommand("игра")
        {
            ModeratorsOnly = true,
        };

        var processor = CreateProcessor(command);

        _settingsStore.Mutate(settings => settings.Commands["игра"] = new()
        {
            Access = CommandAccessLevel.Broadcaster,
        });

        var moderator = await processor.TryProcessAsync("!игра", CreateContext(isModerator: true), CancellationToken.None);
        var broadcaster = await processor.TryProcessAsync("!игра", CreateContext(isBroadcaster: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(moderator.IsCommand, Is.False, "Настройка вправе поднять планку выше кодовой");
            Assert.That(broadcaster.IsCommand, Is.True);
            Assert.That(command.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Токены_настоящей_регистрации_команд_не_пересекаются_и_помощь_называет_каждую_один_раз()
    {
        var commands = new ServiceCollection()
            .AddChatPipeline()
            .Where(descriptor => descriptor.ServiceType == typeof(IChatCommand))
            .Select(descriptor => (IChatCommand)RuntimeHelpers.GetUninitializedObject(descriptor.ImplementationType!))
            .ToList();

        var logger = new RecordingLogger<ChatCommandProcessor>();
        var processor = new ChatCommandProcessor(commands, _settingsStore, _usage, logger);
        processor.Register(new HelpCommand(processor.GetEnabledCommands, processor.Prefix));

        var tokens = processor.GetAllCommands()
            .SelectMany(command => command.Aliases.Prepend(command.Canonical))
            .ToList();

        var help = await processor.TryProcessAsync("!помощь", CreateContext(), CancellationToken.None);
        var listed = help.Response!.Text["📋 Команды: ".Length..].Split(", ");

        Assert.Multiple(() =>
        {
            Assert.That(commands, Has.Count.GreaterThan(1), "Регистрация отдала команды");
            Assert.That(processor.GetAllCommands(), Has.Count.EqualTo(commands.Count + 1), "Ни одна команда не потеряла канон");
            Assert.That(tokens, Is.Unique.Using((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase), "Токен принадлежит одной команде");
            Assert.That(logger.Entries.Where(entry => entry.Level >= LogLevel.Warning), Is.Empty);
            Assert.That(listed, Is.Unique);
            Assert.That(listed, Has.Length.EqualTo(commands.Count + 1));

            var canonicals = processor.GetAllCommands().Select(command => command.Canonical).ToList();

            Assert.That(CommandRenames.All.Select(rename => rename.To), Is.SubsetOf(canonicals),
                "Перенос ключа ведёт к живому канону");

            Assert.That(CommandRenames.All.Select(rename => rename.From).Intersect(canonicals, StringComparer.OrdinalIgnoreCase),
                Is.Empty,
                "Прежнее имя не может снова стать каноном – его ключ в файле ушёл бы к чужой команде");
        });
    }

    [TestCase("profile", new string[0], "profile", new string[0], "!profile")]
    [TestCase("profile", new[] { "профиль" }, "мойпрофиль", new[] { "profile" }, "!profile")]
    [TestCase("мойпрофиль", new[] { "profile" }, "profile", new string[0], "!PROFILE")]
    [TestCase("ранг", new[] { "rank" }, "ранги", new[] { "RANK" }, "!rank")]
    public async Task Занятый_токен_остаётся_за_первой_командой_и_дубль_пишется_в_журнал(
        string firstCanonical,
        string[] firstAliases,
        string secondCanonical,
        string[] secondAliases,
        string message)
    {
        var first = new FakeCommand(firstCanonical, firstAliases);
        var second = new FakeCommand(secondCanonical, secondAliases);
        var logger = new RecordingLogger<ChatCommandProcessor>();
        var processor = new ChatCommandProcessor([first, second], _settingsStore, _usage, logger);

        await processor.TryProcessAsync(message, CreateContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.Calls, Is.EqualTo(1), "Токен не перезаписывается следующей командой");
            Assert.That(second.Calls, Is.Zero);
            Assert.That(logger.Entries.Count(entry => entry.Level == LogLevel.Warning), Is.EqualTo(1), "Дубль не молчит");
        });
    }

    private static CommandContext CreateContext(bool isBroadcaster = false, bool isModerator = false)
    {
        return new()
        {
            Channel = "test-channel",
            MessageId = "msg-1",
            UserId = "u1",
            Username = "alice",
            DisplayName = "Алиса",
            IsBroadcaster = isBroadcaster,
            IsModerator = isModerator,
        };
    }

    private ChatCommandProcessor CreateProcessor(params IChatCommand[] commands)
    {
        return new(commands, _settingsStore, _usage, NullLogger<ChatCommandProcessor>.Instance);
    }

    private sealed class FakeCommand(string canonical, params string[] aliases) : IChatCommand
    {
        public string Canonical => canonical;

        public IReadOnlyCollection<string> Aliases => aliases;

        public string Description => "тестовая команда";

        public bool Throws { get; init; }

        public bool BroadcasterOnly { get; init; }

        public bool ModeratorsOnly { get; init; }

        public OutgoingMessage? Response { get; init; } = OutgoingMessage.Normal("ответ");

        public int Calls { get; private set; }

        public bool CanExecute(CommandContext context)
        {
            if (BroadcasterOnly)
            {
                return context.IsBroadcaster;
            }

            return !ModeratorsOnly || context.IsBroadcaster || context.IsModerator;
        }

        public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Calls++;

            if (Throws)
            {
                throw new InvalidOperationException("команда упала");
            }

            return Task.FromResult(Response);
        }
    }
}
