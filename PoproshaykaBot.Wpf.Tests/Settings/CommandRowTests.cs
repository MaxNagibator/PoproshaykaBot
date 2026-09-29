using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;
using System.IO;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
public class CommandRowTests
{
    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-commands-page-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _prompt = new();
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

    private FakeUnsavedChangesPrompt _prompt = null!;

    private sealed class FakeCommand : IChatCommand
    {
        public string Canonical => "помощь";

        public IReadOnlyCollection<string> Aliases => ["help", "h"];

        public string Description => "список команд бота";

        public bool CanExecute(CommandContext context) => true;

        public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<OutgoingMessage?>(null);
        }
    }

    private sealed class RestrictedCommand(string canonical = "x2illson", bool canExecute = true) : IChatCommand
    {
        public string Canonical => canonical;

        public IReadOnlyCollection<string> Aliases => [];

        public string Description => "курс монеты для своих";

        public bool IsRestrictedToAllowedUsers => true;

        public bool CanExecute(CommandContext context) => canExecute;

        public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<OutgoingMessage?>(null);
        }
    }

    private sealed class ModeratorsCommand : IChatCommand
    {
        public string Canonical => "игра";

        public IReadOnlyCollection<string> Aliases => [];

        public string Description => "сменить категорию";

        public bool CanExecute(CommandContext context) => context.IsModerator || context.IsBroadcaster;

        public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<OutgoingMessage?>(null);
        }
    }

    private static CommandRowViewModel Row()
    {
        return new(new FakeCommand(), "!", CommandAccessLevel.Everyone);
    }

    [Test]
    public void Reads_the_inherited_target_from_the_common_default()
    {
        var settings = new CommandSettings
        {
            DefaultResponseTarget = CommandResponseTarget.Overlay,
        };

        var row = Row();
        row.ApplySettings(settings);

        Assert.Multiple(() =>
        {
            Assert.That(row.TargetOption, Is.SameAs(CommandResponseTargetOption.Inherit));
            Assert.That(row.IsTargetInherited, Is.True);
            Assert.That(row.EffectiveTargetText, Is.EqualTo(CommandResponseTargetOption.Overlay.Title));
        });
    }

    [TestCase(CommandResponseTarget.Chat)]
    [TestCase(CommandResponseTarget.Overlay)]
    [TestCase(CommandResponseTarget.None)]
    [TestCase(CommandSettings.ChatAndOverlay)]
    [TestCase(CommandSettings.CallerOnly)]
    [TestCase(CommandSettings.WhisperToCaller)]
    public void Reads_the_own_target_of_a_command(CommandResponseTarget target)
    {
        var settings = new CommandSettings();
        settings.Commands["помощь"] = new()
        {
            ResponseTarget = target,
        };

        var row = Row();
        row.ApplySettings(settings);

        Assert.Multiple(() =>
        {
            Assert.That(row.TargetOption.Target, Is.EqualTo(target));
            Assert.That(row.IsTargetInherited, Is.False);
        });
    }

    [TestCase(CommandSettings.CallerOnly, "Ответом на сообщение")]
    [TestCase(CommandSettings.WhisperToCaller, "Лично (шёпотом)")]
    public void Цели_ответом_и_шёпотом_предлагаются_команде_и_общему_умолчанию(CommandResponseTarget target, string title)
    {
        var option = CommandResponseTargetOption.ForDefaultTarget(target);

        Assert.Multiple(() =>
        {
            Assert.That(option.Target, Is.EqualTo(target));
            Assert.That(option.Title, Is.EqualTo(title));
            Assert.That(CommandResponseTargetOption.ForCommand, Does.Contain(option));
            Assert.That(CommandResponseTargetOption.ForDefault, Does.Contain(option));
        });
    }

    [TestCase(CommandResponseTarget.Whisper)]
    [TestCase(CommandResponseTarget.Whisper | CommandResponseTarget.Overlay)]
    public void Шёпот_из_правленого_руками_файла_показывается_шёпотом(CommandResponseTarget target)
    {
        Assert.That(CommandResponseTargetOption.Describe(target), Is.EqualTo(CommandResponseTargetOption.Whisper.Title));
    }

    [Test]
    public void Ответ_на_сообщение_не_обещает_приватности()
    {
        Assert.That(CommandResponseTargetOption.Caller.Title + CommandResponseTargetOption.Caller.Hint,
            Does.Not.Contain("только").IgnoreCase.And.Contain("все зрители"));
    }

    [Test]
    public void Reading_the_file_does_not_look_like_a_user_edit()
    {
        var settings = new CommandSettings();
        settings.Commands["помощь"] = new()
        {
            Enabled = false,
            ResponseTarget = CommandResponseTarget.Chat,
            Access = CommandAccessLevel.Moderators,
        };

        var row = Row();
        var edits = 0;

        row.EnabledChanged += (_, _) => edits++;
        row.TargetChanged += (_, _) => edits++;
        row.AccessChanged += (_, _) => edits++;

        row.ApplySettings(settings);

        Assert.That(edits, Is.Zero, "Загрузка настроек не должна писать их обратно в файл");
    }

    [Test]
    public void A_user_edit_asks_the_page_to_save_it()
    {
        var row = Row();
        var enabledEdits = 0;
        var targetEdits = 0;
        var accessEdits = 0;

        row.EnabledChanged += (_, _) => enabledEdits++;
        row.TargetChanged += (_, _) => targetEdits++;
        row.AccessChanged += (_, _) => accessEdits++;

        row.IsEnabled = false;
        row.TargetOption = CommandResponseTargetOption.Silent;
        row.AccessOption = CommandAccessOption.Broadcaster;

        Assert.Multiple(() =>
        {
            Assert.That(enabledEdits, Is.EqualTo(1));
            Assert.That(targetEdits, Is.EqualTo(1));
            Assert.That(accessEdits, Is.EqualTo(1));
        });
    }

    [TestCase("помощь", true)]
    [TestCase("ПОМОЩЬ", true)]
    [TestCase("!h", true)]
    [TestCase("список", true)]
    [TestCase("донат", false)]
    public void Finds_a_command_by_name_alias_or_description(string query, bool expected)
    {
        Assert.That(Row().Matches(query), Is.EqualTo(expected));
    }

    [Test]
    public void Shows_a_dash_until_the_command_is_called()
    {
        var row = Row();
        row.ApplyUsage(null);

        Assert.Multiple(() =>
        {
            Assert.That(row.HasLastUse, Is.False);
            Assert.That(row.LastUsedAtText, Is.EqualTo("–"));
            Assert.That(row.TotalCountText, Is.EqualTo("0"));
        });
    }

    [Test]
    public void Names_the_caller_of_the_last_use()
    {
        var row = Row();

        row.ApplyUsage(new CommandUsageRecord
        {
            Canonical = "помощь",
            TotalCount = 1284,
            StreamCount = 37,
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastUsedBy = "qp_illson",
        });

        Assert.Multiple(() =>
        {
            Assert.That(row.HasLastUse, Is.True);
            Assert.That(row.LastUsedAtText, Is.EqualTo("5 мин. назад"));
            Assert.That(row.LastUsedByText, Is.EqualTo("qp_illson"));
            Assert.That(row.LastUsedSummary, Does.Contain("qp_illson"));
        });
    }

    [Test]
    public void Права_различают_особый_список_и_берут_префикс_у_процессора()
    {
        var page = CreatePage("?",
            new FakeCommand(),
            new RestrictedCommand(),
            new RestrictedCommand("тайное", false),
            new ModeratorsCommand());

        var everyone = page.Rows.Single(row => row.Canonical == "помощь");
        var restricted = page.Rows.Single(row => row.Canonical == "x2illson");
        var restrictedClosed = page.Rows.Single(row => row.Canonical == "тайное");
        var moderators = page.Rows.Single(row => row.Canonical == "игра");

        Assert.Multiple(() =>
        {
            Assert.That(restricted.IsRestrictedToAllowedUsers, Is.True);
            Assert.That(restricted.AccessText, Is.EqualTo("Особый список"));
            Assert.That(
                restrictedClosed.AccessText,
                Is.EqualTo("Никто"),
                "Команда, отказавшая всем пробам, объявляется закрытой, а особый список остаётся в подсказке");

            Assert.That(restrictedClosed.IsRestrictedToAllowedUsers, Is.True);
            Assert.That(everyone.CodeLevel, Is.EqualTo(CommandAccessLevel.Everyone));
            Assert.That(moderators.CodeLevel, Is.EqualTo(CommandAccessLevel.Moderators));
            Assert.That(moderators.AccessText, Is.EqualTo("Модераторы"));
            Assert.That(everyone.Invocation, Is.EqualTo("?помощь"), "Префикс страницы приходит из Core");
            Assert.That(everyone.Aliases, Is.EqualTo(new[] { "?help", "?h" }));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void Шёпот_без_права_у_бота_страница_просит_переавторизовать_бота(bool granted, bool expectsNotice)
    {
        new AccountsStore(null, Path.Combine(_root, "accounts.json")).Mutate(TwitchOAuthRole.Bot, account =>
            account.StoredScopes = granted
                ? [..TwitchScopes.BotRequired, ..TwitchScopes.BotOptional]
                : [..TwitchScopes.BotRequired]);

        var page = CreatePage("!", new FakeCommand());

        var before = page.WhisperNotice;

        page.Rows.Single().TargetOption = CommandResponseTargetOption.Whisper;

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Null, "Пока шёпотом не отвечает ни одна команда, заметки нет");
            Assert.That(page.WhisperNotice is not null, Is.EqualTo(expectsNotice));
        });
    }

    [Test]
    public void Право_из_инспектора_ложится_в_файл_и_снимается_обратно()
    {
        var settingsPath = Path.Combine(_root, "commands.json");
        var page = CreatePage("!", settingsPath, new FakeCommand());

        var row = page.Rows.Single();
        row.AccessOption = CommandAccessOption.Broadcaster;

        var saved = new CommandSettingsStore(null, settingsPath).Load().ReadAccess("помощь");

        row.AccessOption = CommandAccessOption.Inherit;

        var cleared = new CommandSettingsStore(null, settingsPath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.EqualTo(CommandAccessLevel.Broadcaster));
            Assert.That(cleared.ReadAccess("помощь"), Is.Null, "Право снимается, а не остаётся навсегда");
            Assert.That(cleared.Commands, Is.Empty, "Пустое переопределение в файле не остаётся");
            Assert.That(row.AccessText, Is.EqualTo("Все"));
        });
    }

    [Test]
    public void Право_слабее_кодового_объявляется_расхождением_и_не_расширяет_доступ()
    {
        var page = CreatePage("!", new ModeratorsCommand());

        var row = page.Rows.Single();
        row.AccessOption = CommandAccessOption.Inherit;

        Assert.Multiple(() =>
        {
            Assert.That(row.CodeLevel, Is.EqualTo(CommandAccessLevel.Moderators));
            Assert.That(row.EffectiveAccessLevel, Is.EqualTo(CommandAccessLevel.Moderators));
            Assert.That(row.HasAccessConflict, Is.False);
        });

        var weaker = new CommandSettings();
        weaker.Commands["игра"] = new()
        {
            Access = CommandAccessLevel.Everyone,
        };

        row.ApplySettings(weaker);

        Assert.Multiple(() =>
        {
            Assert.That(row.EffectiveAccessLevel,
                Is.EqualTo(CommandAccessLevel.Moderators),
                "Настройка слабее кода доступ не расширяет");

            Assert.That(row.AccessText, Is.EqualTo("Модераторы"));
        });
    }

    [Test]
    public void Инспектор_показывает_параметры_только_у_команды_с_ними()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager,
            new FakeCommand(),
            new DonateCommand(settingsManager));

        page.SelectedRow = page.Rows.Single(row => row.Canonical == "помощь");

        var withoutParameters = page.Parameters;

        page.SelectedRow = page.Rows.Single(row => row.Canonical == "донат");

        Assert.Multiple(() =>
        {
            Assert.That(withoutParameters, Is.Null, "У команды без параметров раздел не показывается");
            Assert.That(page.HasParameters, Is.True);
            Assert.That(page.Parameters!.Items, Has.Count.EqualTo(1));
            Assert.That(page.Parameters.Items[0].Text,
                Is.EqualTo(settingsManager.Current.Twitch.Messages.DonateCommandMessage));
        });
    }

    [Test]
    public void Правка_параметра_уходит_в_тот_же_файл_настроек()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager, new DonateCommand(settingsManager));

        page.SelectedRow = page.Rows.Single();

        var parameter = page.Parameters!.Items[0];
        parameter.Text = "Донат идёт через СБП";

        page.SaveParametersCommand.Execute(null);

        var reread = CreateSettingsManager().Current.Twitch.Messages.DonateCommandMessage;

        Assert.Multiple(() =>
        {
            Assert.That(reread, Is.EqualTo("Донат идёт через СБП"));
            Assert.That(page.Parameters.IsDirty, Is.False);
            Assert.That(page.ParametersNoticeTone, Is.EqualTo(CommandNoticeTone.Info));
        });
    }

    [Test]
    public void Негодный_параметр_не_сохраняется_и_называет_поле()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager, new TrumpCommand(settingsManager));

        page.SelectedRow = page.Rows.Single();

        var price = page.Parameters!.Items.Single(item => item.Label.StartsWith("Цена", StringComparison.Ordinal));
        price.Text = "0";

        page.SaveParametersCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(price.HasError, Is.True, "Ноль в цене покупки уронил бы расчёт прибыли");
            Assert.That(page.ParametersNoticeTone, Is.EqualTo(CommandNoticeTone.Error));
            Assert.That(CreateSettingsManager().Current.SpecialCommands.X2IllsonPurchasePrice,
                Is.EqualTo(new SpecialCommandsSettings().X2IllsonPurchasePrice),
                "Негодная правка до файла не доезжает");
        });
    }

    [Test]
    public void A_call_on_the_open_page_grows_the_counters_of_the_same_row()
    {
        var usage = new CommandUsageRepository(TimeProvider.System);
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), CreateSettingsManager(), usage, new FakeCommand());

        var row = page.Rows.Single();
        page.SelectedRow = row;
        row.TargetOption = CommandResponseTargetOption.Silent;

        usage.Track("помощь", "qp_illson");
        usage.Track("помощь", "qp_illson");
        usage.Track("донат", "qp_illson");

        page.RefreshUsage();

        Assert.Multiple(() =>
        {
            Assert.That(page.Rows, Has.Count.EqualTo(1), "Вызов неизвестной команды не добавляет строку");
            Assert.That(page.Rows.Single(), Is.SameAs(row), "Строки не пересобираются");
            Assert.That(page.SelectedRow, Is.SameAs(row), "Выделение сохраняется");
            Assert.That(row.TargetOption, Is.SameAs(CommandResponseTargetOption.Silent), "Правка строки сохраняется");
            Assert.That(row.TotalCount, Is.EqualTo(2));
            Assert.That(row.StreamCount, Is.EqualTo(2));
            Assert.That(row.LastUsedByText, Is.EqualTo("qp_illson"));
            Assert.That(page.SummaryLine, Does.Contain("вызовов 2"));
        });
    }

    [Test]
    public async Task Закрытый_гейт_записи_не_выдаётся_за_сохранение()
    {
        var settingsPath = Path.Combine(_root, "settings.json");
        var gate = new SettingsWriteGate();
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, settingsPath, gate);

        await gate.RunExternalWriteAsync(() => Task.FromResult(0), _ => new[] { "settings.json" });

        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager, new DonateCommand(settingsManager));

        page.SelectedRow = page.Rows.Single();
        page.Parameters!.Items[0].Text = "Донат идёт через СБП";

        page.SaveParametersCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.ParametersNoticeTone, Is.EqualTo(CommandNoticeTone.Warning),
                "Незаписанный файл – это не успех и не отказ ввода");
            Assert.That(page.ParametersNotice, Does.Contain("не записаны"));
            Assert.That(File.Exists(settingsPath), Is.False, "Гейт не пустил запись на диск");
            Assert.That(settingsManager.Current.Twitch.Messages.DonateCommandMessage,
                Is.EqualTo("Донат идёт через СБП"),
                "В памяти правка действует до перезапуска");
        });
    }

    [Test]
    public void Смена_команды_с_черновиком_спрашивает_и_на_отказ_возвращает_строку()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager,
            new DonateCommand(settingsManager),
            new TrumpCommand(settingsManager));

        var donate = page.Rows.Single(row => row.Canonical == "донат");
        page.SelectedRow = donate;
        page.Parameters!.Items[0].Text = "Донат идёт через СБП";

        _prompt.Answer = UnsavedChangesDecision.Stay;
        page.SelectedRow = page.Rows.Single(row => row.Canonical != "донат");

        Assert.Multiple(() =>
        {
            Assert.That(_prompt.AskCount, Is.EqualTo(1), "Черновик не выбрасывается молча");
            Assert.That(_prompt.LastAction, Is.EqualTo("переключением на другую команду"));
            Assert.That(_prompt.LastSubject, Does.Contain("!донат"), "Вопрос называет команду, а не «какие-то параметры»");
            Assert.That(page.SelectedRow, Is.SameAs(donate), "Отказ оставляет пользователя на правке");
            Assert.That(page.Parameters!.IsDirty, Is.True);
        });
    }

    [Test]
    public void Перечитывание_с_черновиком_спрашивает_и_по_согласию_сохраняет()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager, new DonateCommand(settingsManager));

        page.SelectedRow = page.Rows.Single();
        page.Parameters!.Items[0].Text = "Донат идёт через СБП";

        _prompt.Answer = UnsavedChangesDecision.Save;
        page.RefreshCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_prompt.AskCount, Is.EqualTo(1));
            Assert.That(_prompt.LastAction, Is.EqualTo("перечитыванием"));
            Assert.That(CreateSettingsManager().Current.Twitch.Messages.DonateCommandMessage,
                Is.EqualTo("Донат идёт через СБП"),
                "Согласие на сохранение доезжает до файла");
            Assert.That(page.Parameters!.IsDirty, Is.False);
        });
    }

    [Test]
    public async Task Гейт_закрытия_окна_видит_черновик_и_умеет_его_отбросить()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager, new DonateCommand(settingsManager));

        page.SelectedRow = page.Rows.Single();

        var original = page.Parameters!.Items[0].Text;
        page.Parameters.Items[0].Text = "Донат идёт через СБП";

        var dirtyPage = (IUnsavedChangesPage)page;
        var dirtyBeforeDiscard = dirtyPage.HasUnsavedChanges;

        dirtyPage.DiscardUnsavedChanges();

        var savedFromGate = await ((IUnsavedChangesPage)page).TrySaveUnsavedChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(dirtyBeforeDiscard, Is.True, "Закрытие окна обязано увидеть черновик инспектора");
            Assert.That(page.Parameters!.Items[0].Text, Is.EqualTo(original), "Отказ возвращает значение из файла");
            Assert.That(dirtyPage.HasUnsavedChanges, Is.False);
            Assert.That(savedFromGate, Is.True, "Сохранять нечего – гейт не держит закрытие");
        });
    }

    [Test]
    public void Фильтр_не_прячет_строку_с_несохранённым_черновиком()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager,
            new DonateCommand(settingsManager),
            new TrumpCommand(settingsManager));

        var donate = page.Rows.Single(row => row.Canonical == "донат");
        page.SelectedRow = donate;
        page.Parameters!.Items[0].Text = "Донат идёт через СБП";

        page.FilterText = "x2illson";

        Assert.Multiple(() =>
        {
            Assert.That(page.Rows, Does.Contain(donate), "Строка правки остаётся видимой, иначе черновик теряется молча");
            Assert.That(page.SelectedRow, Is.SameAs(donate));
            Assert.That(_prompt.AskCount, Is.Zero, "Поиск не спрашивает про черновик");
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Пересборка_списка_под_живым_ListBox_не_теряет_черновик()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager,
            new DonateCommand(settingsManager),
            new TrumpCommand(settingsManager));

        var list = new ListBox { ItemsSource = page.Rows };

        list.SetBinding(Selector.SelectedItemProperty,
            new Binding(nameof(CommandsPageViewModel.SelectedRow)) { Source = page, Mode = BindingMode.TwoWay });

        var donate = page.Rows.Single(row => row.Canonical == "донат");
        page.SelectedRow = donate;
        page.Parameters!.Items[0].Text = "Донат идёт через СБП";

        page.FilterText = "донат";

        Assert.Multiple(() =>
        {
            Assert.That(page.SelectedRow, Is.SameAs(donate), "Очистка коллекции сбрасывает выделение списка – его возвращает пересборка");
            Assert.That(page.Parameters!.IsDirty, Is.True, "Пересборка списка не съедает черновик");
            Assert.That(page.Parameters.Items[0].Text, Is.EqualTo("Донат идёт через СБП"));
            Assert.That(_prompt.AskCount, Is.Zero, "Про свою же пересборку страница не спрашивает");
        });
    }

    [Test]
    public void Возврат_на_страницу_после_несостоявшегося_ухода_не_съедает_черновик()
    {
        var settingsManager = CreateSettingsManager();
        var page = CreatePage("!", Path.Combine(_root, "commands.json"), settingsManager, new DonateCommand(settingsManager));

        page.SelectedRow = page.Rows.Single();
        page.Parameters!.Items[0].Text = "Донат идёт через СБП";

        page.OnEnter();

        Assert.Multiple(() =>
        {
            Assert.That(page.Parameters!.IsDirty, Is.True, "Вход на страницу перечитывает строки, а не правку");
            Assert.That(page.Parameters.Items[0].Text, Is.EqualTo("Донат идёт через СБП"));
        });
    }

    [Test]
    public void Страницы_с_черновиком_объявлены_гейту_закрытия_окна()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(IUnsavedChangesPage).IsAssignableFrom(typeof(CommandsPageViewModel)), Is.True);
            Assert.That(typeof(IUnsavedChangesPage).IsAssignableFrom(typeof(SettingsPageViewModel)), Is.True);
        });
    }

    private SettingsManager CreateSettingsManager()
    {
        return new(NullLogger<SettingsManager>.Instance, Path.Combine(_root, "settings.json"));
    }

    private CommandsPageViewModel CreatePage(string prefix, params IChatCommand[] commands)
    {
        return CreatePage(prefix, Path.Combine(_root, "commands.json"), commands);
    }

    private CommandsPageViewModel CreatePage(string prefix, string settingsPath, params IChatCommand[] commands)
    {
        return CreatePage(prefix, settingsPath, CreateSettingsManager(), commands);
    }

    private CommandsPageViewModel CreatePage(
        string prefix,
        string settingsPath,
        SettingsManager settingsManager,
        params IChatCommand[] commands)
    {
        return CreatePage(prefix, settingsPath, settingsManager, new CommandUsageRepository(TimeProvider.System), commands);
    }

    private CommandsPageViewModel CreatePage(
        string prefix,
        string settingsPath,
        SettingsManager settingsManager,
        CommandUsageRepository usage,
        params IChatCommand[] commands)
    {
        var settingsStore = new CommandSettingsStore(null, settingsPath);

        var processor = new ChatCommandProcessor(
            commands,
            settingsStore,
            usage,
            NullLogger<ChatCommandProcessor>.Instance,
            prefix);

        return new(processor,
            settingsStore,
            settingsManager,
            usage,
            _prompt,
            new AccountsStore(null, Path.Combine(_root, "accounts.json")),
            NullLogger<CommandsPageViewModel>.Instance);
    }
}
