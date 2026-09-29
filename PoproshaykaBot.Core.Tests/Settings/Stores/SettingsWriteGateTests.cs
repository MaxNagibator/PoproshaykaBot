using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Tests.Settings.Stores;

[TestFixture]
public sealed class SettingsWriteGateTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("settings-write-gate");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private const string Imported = "{ \"принесено\": \"переносом\" }";

    private static readonly Type[] OwnerTypes =
    [
        typeof(SettingsManager),
        typeof(AccountsStore),
        typeof(BroadcastProfilesStore),
        typeof(DashboardLayoutStore),
        typeof(ObsChatStore),
        typeof(ObsIntegrationStore),
        typeof(PollsStore),
        typeof(RecentCategoriesStore),
        typeof(UpdateStore),
    ];

    private DirectoryInfo _directory = null!;

    private static IEnumerable<TestCaseData> Owners()
    {
        yield return Owner("settings.json", (path, gate) =>
        {
            var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, path, gate);
            return () => manager.SaveSettings(manager.Current);
        });

        yield return Owner("accounts.json", (path, gate) =>
        {
            var store = new AccountsStore(null, path, gate);
            return () => store.SaveAll(new(), new());
        });

        yield return Owner("broadcast-profiles.json", (path, gate) =>
        {
            var store = new BroadcastProfilesStore(null, path, gate);
            return () => store.Save(new());
        });

        yield return Owner("dashboard-layout.json", (path, gate) =>
        {
            var store = new DashboardLayoutStore(null, path, gate);
            return () => store.SaveMainWindow(new());
        });

        yield return Owner("obs-chat.json", (path, gate) =>
        {
            var store = new ObsChatStore(Substitute.For<IEventBus>(), null, path, gate);
            return () => store.Save(new());
        });

        yield return Owner("obs-integration.json", (path, gate) =>
        {
            var store = new ObsIntegrationStore(Substitute.For<IEventBus>(), null, path, gate);
            return () => store.Save(new());
        });

        yield return Owner("polls.json", (path, gate) =>
        {
            var store = new PollsStore(null, path, gate);
            return () => store.Save(new());
        });

        yield return Owner("recent-categories.json", (path, gate) =>
        {
            var store = new RecentCategoriesStore(null, path, gate);
            return store.Save;
        });

        yield return Owner("update.json", (path, gate) =>
        {
            var store = new UpdateStore(null, path, gate);
            return () => store.Save(new());
        });
    }

    [Test]
    public void Каталог_переноса_не_несёт_файла_настроек_без_владельца_с_гейтом()
    {
        var covered = Owners().Select(owner => (string)owner.Arguments[0]!).ToArray();

        Assert.That(covered, Is.EquivalentTo(LegacyDataCatalog.StoreOwnedSettingsFiles),
            "Перенос кладёт на диск каждый файл этого списка, и у каждого обязан быть владелец, спрашивающий гейт: "
            + "файл без владельца молча потеряет принесённое, а владелец без файла в каталоге – лишний случай в этих тестах");
    }

    [Test]
    public void Композиция_регистрирует_гейт_синглтоном_и_каждый_владелец_берёт_его_конструктором()
    {
        var services = new ServiceCollection();
        services.AddSettingsStores();

        var registration = services.SingleOrDefault(descriptor => descriptor.ServiceType == typeof(SettingsWriteGate));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registration?.Lifetime, Is.EqualTo(ServiceLifetime.Singleton),
                "Гейт один на процесс: у каждого стора свой экземпляр означал бы, что гашение не видит никто");

            foreach (var owner in OwnerTypes)
            {
                var constructors = owner.GetConstructors();

                Assert.That(constructors, Has.Length.EqualTo(1),
                    $"У {owner.Name} обязан быть ровно один публичный конструктор: из нескольких контейнер выбирает не самый длинный, "
                    + "а первый, у которого каждый параметр разрешим или имеет значение по умолчанию, – и тихо берёт тот, где гейта нет");

                Assert.That(constructors[0].GetParameters().Any(parameter => parameter.ParameterType == typeof(SettingsWriteGate)), Is.True,
                    $"Единственный конструктор {owner.Name} обязан нести гейт – иначе подмена его на null делает гашение тихой пустышкой");
            }
        }
    }

    [TestCaseSource(nameof(Owners))]
    public async Task Владелец_не_переписывает_файл_который_принёс_перенос(string fileName, Func<string, SettingsWriteGate, Action> createWriter)
    {
        var path = Path.Combine(_directory.FullName, fileName);
        var gate = new SettingsWriteGate();
        var write = createWriter(path, gate);

        await ImportAsync(gate, path, fileName);

        write();

        Assert.That(File.ReadAllText(path), Is.EqualTo(Imported),
            "Перенос принёс этот файл, поэтому до перезапуска стор пишет только в память");
    }

    [TestCaseSource(nameof(Owners))]
    public async Task Холостой_для_файла_перенос_возвращает_владельцу_право_записи(string fileName, Func<string, SettingsWriteGate, Action> createWriter)
    {
        var path = Path.Combine(_directory.FullName, fileName);
        var gate = new SettingsWriteGate();
        var write = createWriter(path, gate);

        await ImportAsync(gate, Path.Combine(_directory.FullName, "полностью-чужой-файл.json"), "полностью-чужой-файл.json");

        write();

        Assert.That(File.Exists(path), Is.True,
            "Этого файла перенос не принёс, поэтому право записи обязано вернуться – иначе настройки перестают сохраняться целыми областями");
    }

    [Test]
    public async Task Снятое_право_запрещает_переписывать_файл_а_не_менять_состояние_в_памяти()
    {
        var path = Path.Combine(_directory.FullName, "update.json");
        var gate = new SettingsWriteGate();
        var store = new UpdateStore(null, path, gate);

        Assert.That(store.Load().CheckIntervalHours, Is.Not.EqualTo(9));

        await ImportAsync(gate, path, "update.json");

        store.Save(new() { CheckIntervalHours = 9 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(9),
                "Правка принята в памяти и действует до перезапуска, иначе сеанс после переноса живёт с чужими значениями");

            Assert.That(File.ReadAllText(path), Is.EqualTo(Imported),
                "Файл остаётся тем, который принёс перенос");
        }
    }

    [Test]
    public void Обрыв_внешней_записи_оставляет_право_снятым_у_всех_файлов_настроек()
    {
        var pollsPath = Path.Combine(_directory.FullName, "polls.json");
        var updatePath = Path.Combine(_directory.FullName, "update.json");
        var gate = new SettingsWriteGate();
        var polls = new PollsStore(null, pollsPath, gate);
        var update = new UpdateStore(null, updatePath, gate);

        var failure = Assert.ThrowsAsync<IOException>(() => gate.RunExternalWriteAsync<int>(
            () =>
            {
                File.WriteAllText(pollsPath, Imported);
                throw new IOException("копирование оборвалось на середине");
            },
            static _ => []));

        polls.Save(new());
        update.Save(new());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failure?.Message, Is.EqualTo("копирование оборвалось на середине"));

            Assert.That(File.ReadAllText(pollsPath), Is.EqualTo(Imported),
                "Файлы могли остаться переписанными наполовину, поэтому право не возвращается никому");

            Assert.That(File.Exists(updatePath), Is.False,
                "Файл, до которого оборвавшийся перенос не дошёл, тоже не переписывается: что он успел тронуть, неизвестно");
        }
    }

    [Test]
    public async Task Второй_перенос_не_возвращает_право_записи_файлам_первого()
    {
        var pollsPath = Path.Combine(_directory.FullName, "polls.json");
        var updatePath = Path.Combine(_directory.FullName, "update.json");
        var gate = new SettingsWriteGate();
        var polls = new PollsStore(null, pollsPath, gate);
        var update = new UpdateStore(null, updatePath, gate);

        await ImportAsync(gate, pollsPath, "polls.json");
        await ImportAsync(gate, updatePath, "update.json");

        polls.Save(new());
        update.Save(new());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(pollsPath), Is.EqualTo(Imported),
                "Право, снятое первым переносом, второй возвращать не вправе – принесённое первым никуда не делось");

            Assert.That(File.ReadAllText(updatePath), Is.EqualTo(Imported));
        }
    }

    [Test]
    public async Task Запись_начатая_до_переноса_доезжает_до_диска_раньше_копирования()
    {
        var path = Path.Combine(_directory.FullName, "update.json");
        var gate = new SettingsWriteGate();
        using var writeStarted = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();

        var storeWrite = Task.Run(() => gate.TryWrite(path, () =>
        {
            writeStarted.Set();
            releaseWrite.Wait(TimeSpan.FromSeconds(10));
            File.WriteAllText(path, "запись стора");
        }));

        Assert.That(writeStarted.Wait(TimeSpan.FromSeconds(10)), Is.True);

        var import = Task.Run(() => ImportAsync(gate, path, "update.json"));

        Assert.That(import.Wait(TimeSpan.FromMilliseconds(300)), Is.False,
            "Перенос не имеет права начать копирование, пока идёт запись стора: иначе она ляжет поверх принесённого файла");

        releaseWrite.Set();

        await storeWrite;
        await import;

        Assert.That(File.ReadAllText(path), Is.EqualTo(Imported),
            "Запись стора успела целиком до копирования, а копирование заместило её результат");
    }

    [Test]
    public async Task Разбор_принесённого_монолита_отнимает_право_записи_у_файлов_которые_он_породил()
    {
        var (source, target) = CreateMonolithSource();
        var pollsPath = Path.Combine(target, "settings", "polls.json");
        var gate = new SettingsWriteGate();
        var polls = new PollsStore(null, pollsPath, gate);

        await gate.RunExternalWriteAsync(
            () => Task.FromResult(LegacyDataImporter.Import(source, target, false, null)),
            static imported => imported.RewrittenSettingsFiles);

        polls.Save(new());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(pollsPath), Does.Contain("777"),
                "polls.json никто не копировал – его вынул из принесённого монолита разбор настроек, "
                + "и стор с доимпортным состоянием в памяти переписывать его не вправе");

            Assert.That(gate.IsRevoked(pollsPath), Is.True);
        }
    }

    [Test]
    public async Task Возвращённый_в_прежний_вид_файл_права_записи_не_отнимает()
    {
        var (source, target) = CreateMonolithSource();
        var pollsPath = Path.Combine(target, "settings", "polls.json");
        Directory.CreateDirectory(Path.GetDirectoryName(pollsPath)!);
        File.WriteAllText(pollsPath, """{"historyMaxItems":11}""");

        var gate = new SettingsWriteGate();
        var polls = new PollsStore(null, pollsPath, gate);

        await gate.RunExternalWriteAsync(
            () => Task.FromResult(LegacyDataImporter.Import(source, target, false, null)),
            static imported => imported.RewrittenSettingsFiles);

        polls.Save(new() { HistoryMaxItems = 22 });

        Assert.That(File.ReadAllText(pollsPath), Does.Contain("22"),
            "С выключенной перезаписью разбор откатили к доимпортному снимку: на диске лежит то же, что у стора в памяти, "
            + "и запрещать ему сохраняться до перезапуска не за что");
    }

    [Test]
    public async Task Переезд_плоской_раскладки_на_месте_отнимает_право_записи_у_перенесённого_файла()
    {
        var target = Path.Combine(_directory.FullName, "рабочая");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "accounts.json"),
            """{"botAccount":{"login":"thebot","accessToken":"принесённый-токен"}}""");

        var accountsPath = Path.Combine(target, "settings", "accounts.json");
        var gate = new SettingsWriteGate();
        var accounts = new AccountsStore(null, accountsPath, gate);

        await gate.RunExternalWriteAsync(
            () => Task.FromResult(LegacyDataImporter.Import(target, target, false, null)),
            static imported => imported.RewrittenSettingsFiles);

        accounts.SaveAll(new(), new());

        Assert.That(File.ReadAllText(accountsPath), Does.Contain("принесённый-токен"),
            "Перенос на месте ничего не копирует, но миграция раскладки положила этот файл в settings/ мимо стора: "
            + "стор его не читал и до перезапуска переписывать не вправе");
    }

    private (string Source, string Target) CreateMonolithSource()
    {
        var source = Path.Combine(_directory.FullName, "источник");
        var target = Path.Combine(_directory.FullName, "рабочая");
        Directory.CreateDirectory(Path.Combine(source, "settings"));
        Directory.CreateDirectory(target);

        File.WriteAllText(Path.Combine(source, "settings", "settings.json"),
            """{"twitch":{"channel":"bobito217","polls":{"historyMaxItems":777}}}""");

        return (source, target);
    }

    private static TestCaseData Owner(string fileName, Func<string, SettingsWriteGate, Action> createWriter)
    {
        return new TestCaseData(fileName, createWriter).SetArgDisplayNames(fileName, "владелец");
    }

    [Test]
    public async Task Менеджер_настроек_докладывает_дошла_ли_правка_до_файла()
    {
        var path = Path.Combine(_directory.FullName, "settings.json");
        var gate = new SettingsWriteGate();
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, path, gate);

        var writtenBeforeImport = manager.Mutate(settings => settings.Twitch.HttpServerPort = 3000);

        await gate.RunExternalWriteAsync(() => Task.FromResult(0), _ => new[] { "settings.json" });

        var writtenAfterImport = manager.Mutate(settings => settings.Twitch.HttpServerPort = 4000);
        var savedAfterImport = manager.SaveSettings(manager.Current);
        var onDisk = new SettingsManager(NullLogger<SettingsManager>.Instance, path);

        Assert.Multiple(() =>
        {
            Assert.That(writtenBeforeImport, Is.True);
            Assert.That(writtenAfterImport, Is.False, "Снятое право – не успех, и вызывающий обязан это узнать");
            Assert.That(savedAfterImport, Is.False);
            Assert.That(manager.Current.Twitch.HttpServerPort, Is.EqualTo(4000), "В памяти правка действует до перезапуска");
            Assert.That(onDisk.Current.Twitch.HttpServerPort, Is.EqualTo(3000), "В файле осталась последняя удавшаяся запись");
        });
    }

    [Test]
    public async Task Признак_снятого_права_поднят_ровно_когда_перенос_что_то_принёс()
    {
        var pollsPath = Path.Combine(_directory.FullName, "polls.json");
        var idlePath = Path.Combine(_directory.FullName, "update.json");
        var gate = new SettingsWriteGate();
        var idleGate = new SettingsWriteGate();
        var abandonedGate = new SettingsWriteGate();

        await ImportAsync(gate, pollsPath, "polls.json");

        await idleGate.RunExternalWriteAsync(
            () => Task.FromResult(0),
            static _ => []);

        Assert.ThrowsAsync<IOException>(() => abandonedGate.RunExternalWriteAsync<int>(
            () => throw new IOException("копирование оборвалось на середине"),
            static _ => []));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gate.HasRevoked, Is.True,
                "По этому признаку страница настроек говорит пользователю, что сохранённое не уехало в файл");

            Assert.That(idleGate.HasRevoked, Is.False,
                "Холостой перенос права не отнимает, и пугать пользователя нечем");

            Assert.That(abandonedGate.HasRevoked, Is.True,
                "Обрыв запрещает запись всем файлам настроек, и сказать об этом надо тем более");

            Assert.That(File.Exists(idlePath), Is.False);
        }
    }

    private static Task ImportAsync(SettingsWriteGate gate, string path, string rewrittenFileName)
    {
        return gate.RunExternalWriteAsync(
            () =>
            {
                File.WriteAllText(path, Imported);
                return Task.FromResult(0);
            },
            _ => new[] { rewrittenFileName });
    }
}
