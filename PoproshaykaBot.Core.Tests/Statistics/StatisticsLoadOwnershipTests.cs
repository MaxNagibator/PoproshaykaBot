using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Statistics;

[TestFixture]
public sealed class StatisticsLoadOwnershipTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private const string UsersFileName = "users_statistics.json";
    private const string BotFileName = "bot_statistics.json";
    private const string HistoryFileName = "stream_sessions.json";
    private const string CommandUsageFileName = "command-usage.json";
    private const string PollHistoryFileName = "polls-history.json";

    private const string UsersContent = """
                                        [
                                          {
                                            "userId": "u1",
                                            "name": "Alice",
                                            "messageCount": 100,
                                            "bonusPoints": 5,
                                            "penaltyPoints": 2,
                                            "firstSeen": "2024-01-01T00:00:00Z",
                                            "lastSeen": "2024-01-02T00:00:00Z"
                                          }
                                        ]
                                        """;

    private const string ImportedUsersContent = """
                                                [
                                                  {
                                                    "userId": "u1",
                                                    "name": "Alice",
                                                    "messageCount": 500,
                                                    "bonusPoints": 5,
                                                    "penaltyPoints": 2,
                                                    "firstSeen": "2024-01-01T00:00:00Z",
                                                    "lastSeen": "2024-01-02T00:00:00Z"
                                                  }
                                                ]
                                                """;

    private const string BotContent = """{"totalMessages":7,"startTime":"2024-01-01T00:00:00Z"}""";

    private const string BotCountersContent = """
                                              {
                                                "totalMessagesProcessed": 7,
                                                "botStartTime": "2024-01-01T00:00:00Z",
                                                "lastUpdated": "2024-01-01T00:00:00Z",
                                                "totalUptime": "00:00:00"
                                              }
                                              """;

    private const string BrokenContent = "{ это не json";

    private string _directory = string.Empty;
    private string _source = string.Empty;

    [SetUp]
    public void SetUp()
    {
        var root = Path.Combine(Path.GetTempPath(), "PoproshaykaBot-StatisticsLoad-" + Guid.NewGuid().ToString("N"));

        _directory = Path.Combine(root, "data");
        _source = Path.Combine(root, "source");

        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(_source);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_directory)!, true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public async Task Подключение_бота_не_перечитывает_уже_загруженную_статистику_пользователей()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await loader.EnsureLoadedAsync();
        users.TrackMessage("u1", "Alice");

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            Assert.That(users.GetById("u1")!.MessageCount, Is.EqualTo(101ul),
                "Подключение бота не должно ни обнулять счётчики до файла, ни складывать их с файлом");
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Сохранение_до_подключения_бота_не_затирает_статистику_бота()
    {
        var usersPath = Path.Combine(_directory, UsersFileName);
        var botPath = Path.Combine(_directory, BotFileName);

        await File.WriteAllTextAsync(usersPath, UsersContent);
        await File.WriteAllTextAsync(botPath, BotContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await loader.EnsureLoadedAsync();
        users.IncrementBonusPoints("u1", 10);

        await saver.SaveNowAsync();

        var savedUsers = await fileStore.LoadUsersAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(botPath), Is.EqualTo(BotContent),
                "Статистика бота не загружалась – её файл нельзя переписывать пустым репозиторием");
            Assert.That(savedUsers.Value!.Single().BonusPoints, Is.EqualTo(15ul),
                "Загруженная статистика пользователей обязана сохраняться и до подключения бота");
        }
    }

    [TestCase(UsersFileName)]
    [TestCase(BotFileName)]
    public async Task Непрочитанный_файл_статистики_не_переписывается_пустым_репозиторием(string brokenFileName)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName),
            string.Equals(brokenFileName, UsersFileName, StringComparison.Ordinal) ? BrokenContent : UsersContent);

        await File.WriteAllTextAsync(Path.Combine(_directory, BotFileName),
            string.Equals(brokenFileName, BotFileName, StringComparison.Ordinal) ? BrokenContent : BotContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            users.TrackMessage("u2", "Bob");
            bot.IncrementMessagesProcessed();

            await saver.SaveNowAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await File.ReadAllTextAsync(Path.Combine(_directory, brokenFileName)), Is.EqualTo(BrokenContent),
                    "Сорвавшееся чтение – не пустота: файл обязан остаться тем, чем был");
                Assert.That(BackupsOf(brokenFileName), Is.Not.Empty,
                    "О повреждённом файле пользователь узнаёт по копии с суффиксом invalid рядом с ним");
                Assert.That(loader.IsLoaded, Is.EqualTo(!string.Equals(brokenFileName, UsersFileName, StringComparison.Ordinal)),
                    "Флаг «загружено» поднимает только удачное чтение");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Отсутствующий_файл_статистики_это_законная_пустота()
    {
        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            users.TrackMessage("u3", "Carol");

            await saver.SaveNowAsync();

            var savedUsers = await fileStore.LoadUsersAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(loader.IsLoaded, Is.True,
                    "Свежий профиль без файла – это ноль записей, а не отказ чтения");
                Assert.That(savedUsers.Value!.Single().Name, Is.EqualTo("Carol"),
                    "Накопленное в первом сеансе обязано дойти до диска");
                Assert.That(File.Exists(Path.Combine(_directory, BotFileName)), Is.True,
                    "Статистика бота тоже заводится с нуля, а не ждёт файла");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Сорвавшееся_повторное_чтение_статистики_бота_гасит_флаг_прошлой_загрузки()
    {
        var botPath = Path.Combine(_directory, BotFileName);

        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(botPath, BotContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);
        await saver.StopAsync(new Progress<string>(), CancellationToken.None);

        await File.WriteAllTextAsync(botPath, BrokenContent);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            bot.IncrementMessagesProcessed();

            await saver.SaveNowAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await File.ReadAllTextAsync(botPath), Is.EqualTo(BrokenContent),
                    "Флаг удачной загрузки прошлого подключения не даёт права переписать непрочитанный файл");
                Assert.That(BackupsOf(BotFileName), Is.Not.Empty,
                    "О повреждённом файле пользователь узнаёт по копии с суффиксом invalid рядом с ним");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [TestCase(true, 500ul)]
    [TestCase(false, 101ul)]
    public async Task Перенос_данных_в_живом_сеансе_решает_судьбу_ближайшего_сохранения(bool importsStatistics, ulong expectedMessageCount)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);

        await File.WriteAllTextAsync(
            Path.Combine(_source, importsStatistics ? UsersFileName : "chat-zoom.txt"),
            importsStatistics ? ImportedUsersContent : "1,25");

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            users.TrackMessage("u1", "Alice");

            var result = await saver.RunExternalWriteAsync(
                () =>
                {
                    Assert.That(loader.IsLoaded, Is.False,
                        "Гашение идёт до копирования – иначе автосохранение успевает лечь поверх принесённого");

                    return LegacyDataImporter.Import(_source, _directory, true, null);
                },
                static imported => imported.ExternalWrite);

            Assert.That(result.CopiedFiles.Contains(UsersFileName), Is.EqualTo(importsStatistics),
                "Перенос обязан либо принести файл статистики, либо не трогать его вовсе");

            Assert.That(loader.IsLoaded, Is.EqualTo(!importsStatistics),
                "Холостой прогон возвращает ровно погашенное, перенос статистики оставляет флаги опущенными");

            await saver.SaveNowAsync();

            var savedUsers = await fileStore.LoadUsersAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(savedUsers.Value!.Single().MessageCount, Is.EqualTo(expectedMessageCount),
                    "Принесённое переносом нельзя терять, а холостой прогон нельзя наказывать потерей сеанса");
                Assert.That(File.Exists(Path.Combine(_directory, BotFileName)), Is.EqualTo(!importsStatistics),
                    "Половина бота гасится и возвращается вместе с половиной пользователей");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Подключение_во_время_внешней_записи_ждёт_её_конца_и_читает_записанное()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(Path.Combine(_source, UsersFileName), ImportedUsersContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);

        var write = Task.Run(() => saver.RunExternalWriteAsync(
            () =>
            {
                entered.Set();
                release.Wait(WaitTimeout);

                return LegacyDataImporter.Import(_source, _directory, true, null);
            },
            static imported => imported.ExternalWrite));

        try
        {
            Assert.That(entered.Wait(WaitTimeout), Is.True, "Внешняя запись обязана дойти до своей работы");

            var start = saver.StartAsync(new Progress<string>(), CancellationToken.None);
            var waited = await Task.WhenAny(start, Task.Delay(TimeSpan.FromMilliseconds(300)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(waited, Is.Not.SameAs(start),
                    "Подключение обязано ждать конца внешней записи, а не читать файл из-под неё");
                Assert.That(loader.IsLoaded, Is.False,
                    "Флаг не поднимается, пока внешняя запись не отпустила замок сохранения");
            }

            release.Set();

            await write;
            await start;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(loader.IsLoaded, Is.True,
                    "Дождавшееся подключение читает уже перенесённый файл, и флаг у него законный");
                Assert.That(users.GetById("u1")!.MessageCount, Is.EqualTo(500ul),
                    "Подключение обязано прочитать то, что записала внешняя запись, а не доимпортный файл");
            }
        }
        finally
        {
            release.Set();

            await Task.WhenAny(write, Task.Delay(WaitTimeout));
            await saver.DisposeAsync();

            entered.Dispose();
            release.Dispose();
        }
    }

    [Test]
    public async Task Остановка_во_время_холостого_переноса_ждёт_его_конца_и_сохраняет_накопленное()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(Path.Combine(_directory, BotFileName), BotCountersContent);
        await File.WriteAllTextAsync(Path.Combine(_directory, CommandUsageFileName), BrokenContent);
        await File.WriteAllTextAsync(Path.Combine(_source, "chat-zoom.txt"), "1,25");

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        Assert.That(loader.IsLoaded, Is.True,
            "Повреждённый файл команд оставляет третий флаг опущенным, и решение об остановке держат только две погашаемые половины");

        users.TrackMessage("u1", "Alice");
        bot.IncrementMessagesProcessed();

        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);

        var write = Task.Run(() => saver.RunExternalWriteAsync(
            () =>
            {
                entered.Set();
                release.Wait(WaitTimeout);

                return LegacyDataImporter.Import(_source, _directory, true, null);
            },
            static imported => imported.ExternalWrite));

        try
        {
            Assert.That(entered.Wait(WaitTimeout), Is.True, "Внешняя запись обязана дойти до своей работы");

            var stop = saver.StopAsync(new Progress<string>(), CancellationToken.None);
            var waited = await Task.WhenAny(stop, Task.Delay(TimeSpan.FromMilliseconds(300)));

            Assert.That(waited, Is.Not.SameAs(stop),
                "Решение о финальном сохранении читает те же флаги, что гасит внешняя запись, поэтому оно обязано ждать её замка, а не опережать его");

            release.Set();

            await write;
            await stop;

            var savedUsers = await fileStore.LoadUsersAsync();
            var savedBot = await fileStore.LoadBotAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(savedUsers.Value!.Single().MessageCount, Is.EqualTo(101ul),
                    "Остановка, попавшая в окно холостого переноса, обязана увидеть возвращённые флаги, а не уйти по ветке «загрузка не выполнялась»");
                Assert.That(savedBot.Value!.TotalMessagesProcessed, Is.EqualTo(8ul),
                    "Счётчики бота с последнего тика автосохранения теряются на ближайшем Replace, если их не дописать при остановке");
            }
        }
        finally
        {
            release.Set();

            await Task.WhenAny(write, Task.Delay(WaitTimeout));
            await saver.DisposeAsync();

            entered.Dispose();
            release.Dispose();
        }
    }

    [Test]
    public async Task Бросок_внутри_внешней_записи_отпускает_замок_и_оставляет_флаги_погашенными()
    {
        var botPath = Path.Combine(_directory, BotFileName);

        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(botPath, BotContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var logger = new RecordingLogger<StatisticsAutoSaver>();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, logger);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            var failure = new IOException("каталог назначения не создался");

            Assert.ThrowsAsync<IOException>(
                () => saver.RunExternalWriteAsync<object>(() => throw failure, _ => StatisticsExternalWrite.None),
                "Отказ внешней записи не проглатывается – о нём узнаёт вызывающий");

            users.TrackMessage("u1", "Alice");
            bot.IncrementMessagesProcessed();

            var save = saver.SaveNowAsync();
            var finished = await Task.WhenAny(save, Task.Delay(WaitTimeout));
            var botOnDisk = await File.ReadAllTextAsync(botPath);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(finished, Is.SameAs(save),
                    "Замок сохранения обязан освободиться и на броске – иначе сохранение висит до конца процесса");
                Assert.That(loader.IsLoaded, Is.False,
                    "Оборванная запись могла оставить файл перенесённым наполовину – писать поверх него нельзя");
                Assert.That(botOnDisk, Is.EqualTo(BotContent),
                    "Обе половины остаются погашенными, пока процесс не перезапустили");
                Assert.That(
                    logger.Entries.Any(entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, failure)),
                    Is.True,
                    "Молчаливо переставшая сохраняться статистика – это потеря сеанса без следа в журнале");
            }

            await save;
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    [Repeat(5)]
    public async Task Одновременные_подключение_и_внешняя_запись_не_встают_в_взаимную_блокировку()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(Path.Combine(_source, UsersFileName), ImportedUsersContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        try
        {
            var start = Task.Run(() => saver.StartAsync(new Progress<string>(), CancellationToken.None));

            var write = Task.Run(() => saver.RunExternalWriteAsync(
                () => LegacyDataImporter.Import(_source, _directory, true, null),
                static imported => imported.ExternalWrite));

            var both = Task.WhenAll(start, write);

            Assert.That(await Task.WhenAny(both, Task.Delay(WaitTimeout)) == both, Is.True,
                "Порядок замков один во всех путях – семафор сохранения, затем семафор загрузчика");

            await both;

            Assert.That(loader.IsLoaded && users.GetById("u1")!.MessageCount != 500ul, Is.False,
                "Поднятый флаг обязан означать, что в репозитории лежит содержимое текущего файла");
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task Гашение_переносом_доживает_до_подключений_бота(int connections)
    {
        var usersPath = Path.Combine(_directory, UsersFileName);
        var botPath = Path.Combine(_directory, BotFileName);

        await File.WriteAllTextAsync(usersPath, UsersContent);
        await File.WriteAllTextAsync(botPath, BotContent);
        await File.WriteAllTextAsync(Path.Combine(_source, UsersFileName), ImportedUsersContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await loader.EnsureLoadedAsync();

        await saver.RunExternalWriteAsync(
            () => LegacyDataImporter.Import(_source, _directory, true, null),
            static imported => imported.ExternalWrite);

        users.TrackMessage("u1", "Alice");

        try
        {
            for (var connection = 1; connection <= connections; connection++)
            {
                await saver.StartAsync(new Progress<string>(), CancellationToken.None);

                if (connection < connections)
                {
                    await saver.StopAsync(new Progress<string>(), CancellationToken.None);
                }
            }

            bot.IncrementMessagesProcessed();

            await saver.SaveNowAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(users.GetById("u1")!.MessageCount, Is.EqualTo(101ul),
                    "Погашенная статистика не перечитывается подключением – иначе накопленное с момента переноса выбрасывается");
                Assert.That(loader.IsLoaded, Is.False,
                    "Перенесённое вступает в силу после перезапуска, а до него флаг остаётся опущенным");
                Assert.That(await File.ReadAllTextAsync(usersPath), Is.EqualTo(ImportedUsersContent),
                    "Перенесённый файл нельзя переписывать доимпортным репозиторием");
                Assert.That(await File.ReadAllTextAsync(botPath), Is.EqualTo(BotContent),
                    "Статистика бота перечитывается на каждом подключении, но после внешней записи это право снято до перезапуска");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Сорвавшееся_чтение_статистики_пользователей_не_повторяется_на_следующем_подключении()
    {
        var usersPath = Path.Combine(_directory, UsersFileName);

        await File.WriteAllTextAsync(usersPath, BrokenContent);
        await File.WriteAllTextAsync(Path.Combine(_directory, BotFileName), BotContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var saver = new StatisticsAutoSaver(users, bot, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);
        await saver.StopAsync(new Progress<string>(), CancellationToken.None);

        await File.WriteAllTextAsync(usersPath, UsersContent);

        users.TrackMessage("u9", "Zed");

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            await saver.SaveNowAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(users.GetById("u9")!.MessageCount, Is.EqualTo(1ul),
                    "Повторная попытка чтения выбросила бы всё, что накопилось после отказа");
                Assert.That(users.GetById("u1"), Is.Null,
                    "Отказ чтения не ретраится: файл в этом сеансе больше не читают, даже если он стал читаемым");
                Assert.That(loader.IsLoaded, Is.False,
                    "Флаг остаётся опущенным – сохранять поверх файла, который не удалось прочитать, нельзя");
                Assert.That(await File.ReadAllTextAsync(usersPath), Is.EqualTo(UsersContent),
                    "Накопленное в сеансе не ложится поверх файла, содержимое которого приложению неизвестно");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Перенос_гасит_право_записи_ровно_у_тех_файлов_которые_принёс(bool importsHistory)
    {
        var historyPath = Path.Combine(_directory, HistoryFileName);

        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(historyPath, HistoryJson("свой-канал"));

        await File.WriteAllTextAsync(
            Path.Combine(_source, importsHistory ? HistoryFileName : UsersFileName),
            importsHistory ? HistoryJson("принесённый-канал") : ImportedUsersContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var history = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, historyPath);

        var saver = new StatisticsAutoSaver(
            users,
            bot,
            new(TimeProvider.System),
            loader,
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            history);

        var ownSessionId = history.Load().Sessions.Single().Id;

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            var imported = await saver.RunExternalWriteAsync(
                () =>
                {
                    Assert.That(history.IsLoaded, Is.False,
                        "Гашение идёт до копирования – иначе конец стрима успевает лечь поверх принесённого");

                    return LegacyDataImporter.Import(_source, _directory, true, null);
                },
                static result => result.ExternalWrite);

            Assert.That(imported.CopiedStreamHistory, Is.EqualTo(importsHistory),
                "Перенос обязан либо принести файл истории, либо не трогать его вовсе");

            Assert.That(history.IsLoaded, Is.EqualTo(!importsHistory),
                "Холостой для истории прогон возвращает право записи, принесённая история оставляет его снятым");

            history.Append(Session("конец-стрима"));
            var hidden = history.TrySetHidden(ownSessionId, true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hidden, Is.True,
                    "Снятое право – это про файл, а не про память: скрытие применяется и без права, а на диск не идёт");
                Assert.That(history.Load().Sessions.Select(session => session.Channel), Does.Contain("конец-стрима"),
                    "Сессию нельзя выбрасывать из-за снятого права – иначе она теряется безвозвратно");
                Assert.That(ChannelsOnDisk(historyPath),
                    Is.EqualTo(importsHistory ? new[] { "принесённый-канал" } : new[] { "свой-канал", "конец-стрима" }),
                    "Принесённую историю нельзя переписывать доимпортной памятью стора, а холостой прогон нельзя наказывать потерей сессии");
                Assert.That(loader.IsLoaded, Is.EqualTo(importsHistory),
                    "Половина статистики живёт своей судьбой: перенос одной истории её права не отнимает");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Сессия_завершившаяся_во_время_несвязанного_переноса_доезжает_до_файла()
    {
        var historyPath = Path.Combine(_directory, HistoryFileName);

        await File.WriteAllTextAsync(historyPath, HistoryJson("свой-канал"));
        await File.WriteAllTextAsync(Path.Combine(_source, "chat-zoom.txt"), "1,25");

        var (users, bot, fileStore, loader) = CreateGraph();
        var history = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, historyPath);

        var saver = new StatisticsAutoSaver(
            users,
            bot,
            new(TimeProvider.System),
            loader,
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            history);

        try
        {
            var imported = await saver.RunExternalWriteAsync(
                () =>
                {
                    history.Append(Session("конец-стрима"));

                    Assert.That(ChannelsOnDisk(historyPath), Is.EqualTo(new[] { "свой-канал" }),
                        "Пока идёт копирование, файл истории не переписывается – перенос мог принести и его");

                    return LegacyDataImporter.Import(_source, _directory, true, null);
                },
                static result => result.ExternalWrite);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(imported.CopiedStreamHistory, Is.False, "Перенос этого прогона истории не касается");
                Assert.That(history.IsLoaded, Is.True, "Право записи возвращается сразу, как выяснилось, что файл никто не менял");
                Assert.That(ChannelsOnDisk(historyPath), Is.EqualTo(new[] { "свой-канал", "конец-стрима" }),
                    "Накопленное за окно переноса сбрасывается на диск возвратом права – иначе стрим, закончившийся в эти секунды, пропадает");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Сорвавшаяся_запись_истории_дотаскивается_ближайшим_тиком_автосохранения(bool failsInsideRestore)
    {
        var historyPath = Path.Combine(_directory, HistoryFileName);
        var blockedTemporaryPath = historyPath + ".tmp";

        Directory.CreateDirectory(blockedTemporaryPath);

        var (users, bot, fileStore, loader) = CreateGraph();
        var history = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, historyPath);

        var saver = new StatisticsAutoSaver(
            users,
            bot,
            new(TimeProvider.System),
            loader,
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            history);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            if (failsInsideRestore)
            {
                await File.WriteAllTextAsync(Path.Combine(_source, "chat-zoom.txt"), "1,25");

                await saver.RunExternalWriteAsync(
                    () =>
                    {
                        history.Append(Session("конец-стрима"));

                        return LegacyDataImporter.Import(_source, _directory, true, null);
                    },
                    static result => result.ExternalWrite);
            }
            else
            {
                Assert.Throws<UnauthorizedAccessException>(() => history.Append(Session("конец-стрима")),
                    "Отказ записи истории не проглатывается – о нём узнаёт вызывающий");
            }

            Assert.That(File.Exists(historyPath), Is.False, "Записывать было некуда: файла истории на диске нет");

            Directory.Delete(blockedTemporaryPath);

            await saver.SaveNowAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ChannelsOnDisk(historyPath), Is.EqualTo(new[] { "конец-стрима" }),
                    "Признак «файл отстаёт» переживает любой отказ записи, и ближайший тик автосохранения дотаскивает сессию");
                Assert.That(history.TryFlush(), Is.True, "После удачного сброса дописывать больше нечего");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Остановка_без_единой_прочитанной_статистики_дотаскивает_историю()
    {
        var historyPath = Path.Combine(_directory, HistoryFileName);
        var usersPath = Path.Combine(_directory, UsersFileName);
        var blockedTemporaryPath = historyPath + ".tmp";

        await File.WriteAllTextAsync(usersPath, BrokenContent);
        await File.WriteAllTextAsync(Path.Combine(_directory, BotFileName), BrokenContent);
        await File.WriteAllTextAsync(Path.Combine(_directory, CommandUsageFileName), BrokenContent);

        Directory.CreateDirectory(blockedTemporaryPath);

        var (users, bot, fileStore, loader) = CreateGraph();
        var history = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, historyPath);

        var saver = new StatisticsAutoSaver(
            users,
            bot,
            new(TimeProvider.System),
            loader,
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            history);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            Assert.That(loader.IsLoaded, Is.False, "Повреждённый файл пользователей права сохранять не даёт");

            Assert.Throws<UnauthorizedAccessException>(() => history.Append(Session("конец-стрима")),
                "Записывать было некуда, сессия осталась несохранённой в памяти");

            Directory.Delete(blockedTemporaryPath);

            await saver.StopAsync(new Progress<string>(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ChannelsOnDisk(historyPath), Is.EqualTo(new[] { "конец-стрима" }),
                    "Остановка бота – последний шанс записать историю, и непрочитанная статистика его не отменяет");
                Assert.That(await File.ReadAllTextAsync(usersPath), Is.EqualTo(BrokenContent),
                    "Сохранять при этом по-прежнему нечего: повреждённый файл статистики остаётся нетронутым");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Отмена_после_удавшейся_записи_не_оставляет_нетронутые_половины_погашенными()
    {
        var historyPath = Path.Combine(_directory, HistoryFileName);
        var botPath = Path.Combine(_directory, BotFileName);

        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(historyPath, HistoryJson("свой-канал"));
        await File.WriteAllTextAsync(Path.Combine(_source, "chat-zoom.txt"), "1,25");

        var (users, bot, fileStore, loader) = CreateGraph();
        var history = new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, historyPath);

        var saver = new StatisticsAutoSaver(
            users,
            bot,
            new(TimeProvider.System),
            loader,
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            history);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        using var cancellation = new CancellationTokenSource();

        try
        {
            await saver.RunExternalWriteAsync(
                () =>
                {
                    var result = LegacyDataImporter.Import(_source, _directory, true, null);
                    cancellation.Cancel();

                    return result;
                },
                static result => result.ExternalWrite,
                cancellation.Token);

            await saver.SaveNowAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(loader.IsLoaded, Is.True,
                    "Возврат прав – уборка после удавшейся записи, и отменять её нечем");
                Assert.That(history.IsLoaded, Is.True,
                    "Отменённый токен не должен оставлять нетронутую историю без права записи до перезапуска");
                Assert.That(File.Exists(botPath), Is.True,
                    "Половина бота возвращается той же уборкой – иначе счётчики сеанса не доезжают до файла");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    [Test]
    public async Task Контейнер_отдаёт_области_внешней_записи_тот_же_стор_истории()
    {
        var historyPath = Path.Combine(_directory, HistoryFileName);
        await File.WriteAllTextAsync(historyPath, HistoryJson("свой-канал"));

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddStatistics();
        services.AddSingleton(new StreamSessionHistoryStore(NullLogger<StreamSessionHistoryStore>.Instance, historyPath));
        services.AddSingleton(CreatePollHistoryStore());

        await using var provider = services.BuildServiceProvider();

        var history = provider.GetRequiredService<StreamSessionHistoryStore>();
        var pollHistory = provider.GetRequiredService<PollHistoryStore>();

        Assert.That(pollHistory.GetAll(), Is.Empty);

        Assert.That(pollHistory.IsLoaded, Is.True,
            "Чтение у истории голосований ленивое, поэтому право писать сперва надо получить – иначе проверка ниже пройдёт сама собой");

        await provider.GetRequiredService<StatisticsAutoSaver>().RunExternalWriteAsync(
            () => 0,
            static _ => new StatisticsExternalWrite(false, true, true));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(history.IsLoaded, Is.False,
                "Необязательный параметр конструктора превращает гашение в тихую пустышку, и увидеть это можно только на настоящей композиции");
            Assert.That(pollHistory.IsLoaded, Is.False,
                "У истории голосований тот же необязательный параметр и та же цена подмены его на null");
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Перенос_гасит_право_записи_истории_голосований_ровно_когда_принёс_её_файл(bool importsPollHistory)
    {
        var pollHistoryPath = Path.Combine(_directory, PollHistoryFileName);

        await File.WriteAllTextAsync(Path.Combine(_directory, UsersFileName), UsersContent);
        await File.WriteAllTextAsync(pollHistoryPath, PollHistoryJson("свой-опрос"));

        await File.WriteAllTextAsync(
            Path.Combine(_source, importsPollHistory ? PollHistoryFileName : UsersFileName),
            importsPollHistory ? PollHistoryJson("принесённый-опрос") : ImportedUsersContent);

        var (users, bot, fileStore, loader) = CreateGraph();
        var pollHistory = CreatePollHistoryStore(pollHistoryPath);

        var saver = new StatisticsAutoSaver(
            users,
            bot,
            new(TimeProvider.System),
            loader,
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            null,
            pollHistory);

        await saver.StartAsync(new Progress<string>(), CancellationToken.None);

        try
        {
            var imported = await saver.RunExternalWriteAsync(
                () =>
                {
                    Assert.That(pollHistory.IsLoaded, Is.False,
                        "Гашение идёт до копирования – иначе конец опроса успевает лечь поверх принесённого");

                    return LegacyDataImporter.Import(_source, _directory, true, null);
                },
                static result => result.ExternalWrite);

            Assert.That(imported.CopiedPollHistory, Is.EqualTo(importsPollHistory),
                "Перенос обязан либо принести файл истории голосований, либо не трогать его вовсе");

            Assert.That(pollHistory.IsLoaded, Is.EqualTo(!importsPollHistory),
                "Холостой для истории голосований прогон возвращает право записи, принесённая история оставляет его снятым");

            var added = pollHistory.TryAdd(new() { PollId = "конец-опроса", Title = "Опрос" });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(added, Is.True,
                    "Снятое право – это про файл, а не про память: запись принимается и без права, а на диск не идёт");
                Assert.That(pollHistory.GetAll().Select(entry => entry.PollId), Does.Contain("конец-опроса"),
                    "Запись нельзя выбрасывать из-за снятого права – иначе она теряется безвозвратно");
                Assert.That(PollIdsOnDisk(pollHistoryPath),
                    Is.EqualTo(importsPollHistory ? new[] { "принесённый-опрос" } : new[] { "свой-опрос", "конец-опроса" }),
                    "Принесённую историю голосований нельзя переписывать доимпортной памятью стора, а холостой прогон нельзя наказывать потерей записи");
                Assert.That(loader.IsLoaded, Is.EqualTo(importsPollHistory),
                    "Половины живут своей судьбой: перенос одной истории голосований прав статистики не отнимает");
            }
        }
        finally
        {
            await saver.DisposeAsync();
        }
    }

    private static string PollHistoryJson(string pollId)
    {
        return $$"""{"version":1,"entries":[{"pollId":"{{pollId}}","title":"Опрос","finalStatus":1}]}""";
    }

    private static string[] PollIdsOnDisk(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return [.. document.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("pollId").GetString()!)];
    }

    private static string[] ChannelsOnDisk(string path)
    {
        var history = JsonSerializer.Deserialize<StreamSessionHistory>(File.ReadAllText(path), JsonStoreOptions.Default)!;

        return [.. history.Sessions.Select(session => session.Channel)];
    }

    private static string HistoryJson(string channel)
    {
        return JsonSerializer.Serialize(new StreamSessionHistory { Sessions = [Session(channel)] }, JsonStoreOptions.Default);
    }

    private static StreamSessionRecord Session(string channel)
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero).AddDays(channel.Length);

        return new()
        {
            Channel = channel,
            StartedAt = startedAt,
            EndedAt = startedAt.AddHours(2),
            Title = "Эфир",
            MessageCount = 10,
        };
    }

    private string[] BackupsOf(string fileName)
    {
        var pattern = Path.GetFileNameWithoutExtension(fileName) + ".invalid-*" + Path.GetExtension(fileName);

        return Directory.GetFiles(_directory, pattern);
    }

    private PollHistoryStore CreatePollHistoryStore(string? filePath = null)
    {
        var pollsStore = Substitute.For<PollsStore>(NullLogger<PollsStore>.Instance, null, null);
        pollsStore.Load().Returns(new PollsSettings());

        return new(pollsStore,
            Substitute.For<ITwitchHelixClient>(),
            Substitute.For<IBroadcasterIdProvider>(),
            new FakeTargetChannelProvider(),
            NullLogger<PollHistoryStore>.Instance,
            filePath ?? Path.Combine(_directory, PollHistoryFileName));
    }

    private (UserStatisticsRepository Users, BotStatisticsRepository Bot, StatisticsFileStore FileStore, UserStatisticsLoader Loader) CreateGraph()
    {
        var users = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var bot = new BotStatisticsRepository();
        var fileStore = new StatisticsFileStore(NullLogger<StatisticsFileStore>.Instance, _directory);
        var loader = new UserStatisticsLoader(users, fileStore, NullLogger<UserStatisticsLoader>.Instance);

        return (users, bot, fileStore, loader);
    }
}
