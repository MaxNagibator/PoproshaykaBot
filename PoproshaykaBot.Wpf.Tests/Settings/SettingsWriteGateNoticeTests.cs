using KeepShell.Services;
using KeepShell.Services.Modal;
using KeepShell.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using PoproshaykaBot.Wpf.ViewModels.Settings;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
public class SettingsWriteGateNoticeTests
{
    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-write-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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

    [Test]
    public async Task Закрытый_гейт_записи_не_выдаётся_за_сохранённые_названия_баллов()
    {
        var settingsPath = Path.Combine(_root, "settings.json");
        var settingsManager = await CreateBlockedSettingsManagerAsync(settingsPath);

        var dialogs = new FakeDialogService(dialog => ((PointTermDialogViewModel)dialog).Singular = "очко");
        var page = CreateUserStatisticsPage(settingsManager, dialogs);

        await page.EditPointTermCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(settingsPath), Is.False, "Гейт не пустил запись на диск");
            Assert.That(settingsManager.Current.Ranks.PointTerm.Singular, Is.EqualTo("очко"),
                "В памяти название действует до перезапуска");
            Assert.That(dialogs.Warnings, Has.Count.EqualTo(1));
            Assert.That(dialogs.Warnings[0], Does.Contain("не записаны").And.Contain("Перезапустите"));
            Assert.That(dialogs.Infos, Is.Empty, "Незаписанный файл – это не сообщение об успехе");
        });
    }

    [Test]
    public async Task Закрытый_гейт_записи_останавливает_шаг_подключения_мастера()
    {
        var settingsPath = Path.Combine(_root, "settings.json");
        var settingsManager = await CreateBlockedSettingsManagerAsync(settingsPath);

        var accountsStore = new AccountsStore(null, Path.Combine(_root, "accounts.json"));
        var connection = new FakeBotConnectionController();

        using var page = new BotConnectionPageViewModel(
            connection,
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            settingsManager,
            accountsStore,
            NullLogger<BotConnectionPageViewModel>.Instance);

        page.OnEnter(new(new AppSettings(), new TwitchAccountSettings(), new TwitchAccountSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(settingsPath), Is.False, "Гейт не пустил запись на диск");
            Assert.That(page.CanAdvance, Is.False, "Шаг остановлен, дальше мастер не пускает");
            Assert.That(page.ShowRetry, Is.False, "Повтор здесь не помогает – помогает перезапуск");
            Assert.That(page.DetailsText, Does.Contain("не записаны").And.Contain("заново"));
            Assert.That(connection.StartCount, Is.Zero, "Подключение не начинается поверх незаписанных настроек");
        });
    }

    [TestCase("settings.json", "Настройки применены")]
    [TestCase("accounts.json", "Вход в Twitch применён")]
    public async Task Мастер_называет_незаписанный_файл_вместо_молчаливого_готово(string revokedFile, string expected)
    {
        var gate = await CreateClosedGateAsync(revokedFile);
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_root, "settings.json"), gate);
        var accountsStore = new AccountsStore(null, Path.Combine(_root, "accounts.json"), gate);
        var dialogs = new FakeDialogService(_ => { });

        var page = new CompletionPageViewModel(
            settingsManager,
            accountsStore,
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            null!,
            null!,
            null!,
            NullLogger<CompletionPageViewModel>.Instance,
            dialogs)
        {
            AutoConnect = false,
        };

        var bot = new TwitchAccountSettings { Login = "bot", AccessToken = "token" };
        var left = await page.OnLeavingAsync(new(new AppSettings(), bot, new TwitchAccountSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(left, Is.True, "Правка действует в памяти, мастер закрывается");
            Assert.That(File.Exists(Path.Combine(_root, revokedFile)), Is.False, "Гейт не пустил запись на диск");
            Assert.That(accountsStore.LoadBot().AccessToken, Is.EqualTo("token"), "Токен действует до перезапуска");
            Assert.That(dialogs.Warnings, Has.Count.EqualTo(1), "Незаписанный файл – не повод для молчаливого «готово»");
            Assert.That(dialogs.Warnings[0], Does.Contain(expected).And.Contain("Перезапустите"));
        });
    }

    [Test]
    public async Task Незаписанные_аккаунты_останавливают_шаг_подключения_мастера()
    {
        var gate = await CreateClosedGateAsync("accounts.json");
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_root, "settings.json"), gate);
        var accountsStore = new AccountsStore(null, Path.Combine(_root, "accounts.json"), gate);
        var connection = new FakeBotConnectionController();

        using var page = new BotConnectionPageViewModel(
            connection,
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            settingsManager,
            accountsStore,
            NullLogger<BotConnectionPageViewModel>.Instance);

        page.OnEnter(new(new AppSettings(), new TwitchAccountSettings(), new TwitchAccountSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_root, "accounts.json")), Is.False);
            Assert.That(page.CanAdvance, Is.False);
            Assert.That(page.ShowRetry, Is.False);
            Assert.That(page.DetailsText, Does.Contain("Вход в Twitch").And.Contain("заново"));
            Assert.That(connection.StartCount, Is.Zero);
        });
    }

    [Test]
    public async Task Раздел_опросов_называет_незаписанный_файл()
    {
        var pollsPath = Path.Combine(_root, "polls.json");
        var section = new PollsSettingsSectionViewModel(
            new PollsStore(null, pollsPath, await CreateClosedGateAsync("polls.json")),
            TimeProvider.System);

        section.StartTemplate = "Новый опрос";
        var written = section.SaveChanges();

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.False, "Страница настроек складывает этот ответ в свою строку итога");
            Assert.That(File.Exists(pollsPath), Is.False);
            Assert.That(section.Notice, Does.Contain("не записаны").And.Contain("Перезапустите"));
            Assert.That(section.HasChanges, Is.False, "Правка принята в памяти, черновик чист");
        });
    }

    [Test]
    public async Task Плитка_профилей_трансляции_называет_незаписанный_файл()
    {
        var gate = await CreateClosedGateAsync("broadcast-profiles.json");
        var store = new BroadcastProfilesStore(null, Path.Combine(_root, "broadcast-profiles.json"), gate);
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        var manager = new BroadcastProfilesManager(store, null!, bus, TimeProvider.System, NullLogger<BroadcastProfilesManager>.Instance);
        manager.Upsert(new() { Name = "Стрим" });

        using var tile = new BroadcastProfilesTileViewModel(
            manager,
            store,
            new FakeStreamStatus(),
            null!,
            null!,
            null!,
            new FakeDialogService(_ => { }),
            null!,
            bus);

        tile.Items[0].DuplicateCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(manager.GetAll(), Has.Count.EqualTo(2), "Копия действует в памяти");
            Assert.That(File.Exists(Path.Combine(_root, "broadcast-profiles.json")), Is.False);
            Assert.That(tile.UnsavedNotice, Does.Contain("не записаны").And.Contain("Перезапустите"));
        });

        tile.Items[0].DeleteCommand.Execute(null);

        Assert.That(tile.UnsavedNotice, Is.Not.Null, "Удаление без записи тоже называется, а не проходит молча");
    }

    [Test]
    public async Task Выбор_профиля_голосования_называет_незаписанное_удаление()
    {
        var gate = await CreateClosedGateAsync("polls.json");
        var manager = new PollProfilesManager(
            new PollsStore(null, Path.Combine(_root, "polls.json"), gate),
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            NullLogger<PollProfilesManager>.Instance);

        manager.Upsert(new() { Name = "Опрос", Title = "Что дальше?", Choices = ["Да", "Нет"] });

        var dialog = new PollFromProfileDialogViewModel(manager, new FakeDialogService(_ => { }));
        dialog.SelectedProfile = dialog.Profiles[0];
        dialog.DeleteCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(dialog.Profiles, Is.Empty, "Удаление действует в памяти");
            Assert.That(dialog.ProfilesNotWritten, Is.True, "Плитка опросов берёт этот признак после закрытия окна");
            Assert.That(dialog.Notice, Does.Contain("не записаны").And.Contain("Перезапустите"));
        });
    }

    private static async Task<SettingsWriteGate> CreateClosedGateAsync(string fileName)
    {
        var gate = new SettingsWriteGate();
        await gate.RunExternalWriteAsync(() => Task.FromResult(0), _ => new[] { fileName });

        return gate;
    }

    private static async Task<SettingsManager> CreateBlockedSettingsManagerAsync(string settingsPath)
    {
        var gate = new SettingsWriteGate();
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, settingsPath, gate);

        await gate.RunExternalWriteAsync(() => Task.FromResult(0), _ => new[] { "settings.json" });

        return settingsManager;
    }

    private sealed class FakeStreamStatus : IStreamStatus
    {
        public StreamStatus CurrentStatus => StreamStatus.Unknown;

        public StreamInfo? CurrentStream => null;

        public Task RefreshLiveSnapshotAsync() => Task.CompletedTask;
    }

    private static UserStatisticsPageViewModel CreateUserStatisticsPage(SettingsManager settingsManager, IDialogService dialogs)
    {
        var statistics = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        return new(
            statistics,
            null!,
            new UserRankService(settingsManager),
            new UserPointsManagementService(statistics, settingsManager, eventBus),
            new FakeChannelProvider(),
            settingsManager,
            dialogs,
            eventBus,
            new MemorySettings());
    }

    private sealed class FakeChannelProvider : IChannelProvider
    {
        public string? Channel => "test";
    }

    private sealed class FakeBotConnectionController : IBotConnectionController
    {
        public int StartCount { get; private set; }

        public bool IsBusy => false;

        public BotLifecyclePhase CurrentPhase => BotLifecyclePhase.Idle;

        public void StartConnection() => StartCount++;

        public void CancelConnection()
        {
        }

        public Task WaitForConnectionAsync() => Task.CompletedTask;

        public Task StopAsync(BotStopMode mode) => Task.CompletedTask;
    }

    private sealed class FakeDialogService(Action<IDialogViewModel> edit) : IDialogService
    {
        public List<string> Warnings { get; } = [];

        public List<string> Infos { get; } = [];

        public Task<bool> ShowAsync(IDialogViewModel viewModel)
        {
            edit(viewModel);
            return Task.FromResult(true);
        }

        public Task<bool> ReplaceAsync(IDialogViewModel viewModel) => ShowAsync(viewModel);

        public bool Confirm(string title, string message, bool defaultYes = false) => true;

        public bool ConfirmWarning(string title, string message, bool defaultYes = false) => true;

        public void Info(string title, string message) => Infos.Add(message);

        public void Warning(string title, string message) => Warnings.Add(message);

        public void Error(string title, string message) => throw new InvalidOperationException(message);
    }
}
