using KeepShell.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
public class StreamHistoryBoxArtTests
{
    private static readonly DateTimeOffset Start = new(new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Local));

    private string _directory = null!;
    private FakeBoxArtCache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-box-art-page-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _cache = new();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task Страница_спрашивает_кеш_обо_всех_играх_набора_одним_вызовом()
    {
        var page = Create(Session(0, "Just Chatting"), Session(1, "Minecraft"), Session(2, "Just Chatting"));

        await page.BoxArtLoading;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_cache.CachedRequests, Has.Count.EqualTo(1));
            Assert.That(_cache.CachedRequests[0], Is.EquivalentTo(new[] { "Just Chatting", "Minecraft" }));
            Assert.That(page.Sessions, Has.Count.EqualTo(3));
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public async Task Кеш_и_декодирование_обложек_идут_мимо_потока_страницы()
    {
        _cache.Cached["Just Chatting"] = WriteImage("just-chatting");

        var pageThread = Environment.CurrentManagedThreadId;
        var page = Create(Session(0, "Just Chatting"), Session(1, "Minecraft"));

        await page.BoxArtLoading;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_cache.RequestThreads, Is.Not.Empty);
            Assert.That(_cache.RequestThreads, Has.No.Member(pageThread),
                "Файловая проверка кеша и BitmapImage не должны стоять в потоке, который пересобирает страницу");
            Assert.That(page.Sessions.First(row => row.GameFormatted == "Just Chatting").BoxArt.HasImage, Is.True);
        }
    }

    [Test]
    public async Task Уже_показанная_обложка_не_ходит_в_кеш_на_следующей_пересборке()
    {
        _cache.Cached["Just Chatting"] = WriteImage("just-chatting");

        var page = Create(Session(0, "Just Chatting"));

        await page.BoxArtLoading;

        var cachedRequests = _cache.CachedRequests.Count;

        page.FilterByGameCommand.Execute(page.TopCategories[0]);
        await page.BoxArtLoading;

        Assert.That(_cache.CachedRequests, Has.Count.EqualTo(cachedRequests),
            "Смена фильтра не повторяет файловую проверку кеша ради обложки, которая уже на экране");
    }

    [Test]
    public async Task Обложка_из_кеша_видна_в_таблице_в_сегментах_в_шапке_и_в_топе_категорий()
    {
        _cache.Cached["Just Chatting"] = WriteImage("just-chatting");

        var page = Create(Session(0, "Just Chatting"));
        page.TrySelectAt(0);

        await page.BoxArtLoading;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions[0].BoxArt.HasImage, Is.True);
            Assert.That(page.Segments[0].BoxArt.HasImage, Is.True);
            Assert.That(page.DetailBoxArt.HasImage, Is.True);
            Assert.That(page.TopCategories[0].BoxArt.HasImage, Is.True);
            Assert.That(page.Sessions[0].BoxArt.AutomationName, Is.EqualTo("Обложка игры «Just Chatting»"));
        }
    }

    [Test]
    public void Строка_без_обложки_остаётся_читаемой_а_строка_без_игры_слота_не_занимает()
    {
        var page = Create(Session(0, "Minecraft"), Session(1, null));
        page.TrySelectAt(0);

        var withGame = page.Sessions.First(row => row.GameFormatted == "Minecraft");
        var without = page.Sessions.First(row => row.GameFormatted == "–");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withGame.BoxArt.HasGame, Is.True);
            Assert.That(withGame.BoxArt.HasImage, Is.False);
            Assert.That(without.BoxArt.HasGame, Is.False);
            Assert.That(page.DetailBoxArt.HasGame, Is.False);
            Assert.That(page.DetailGame, Is.EqualTo("–"));
        }
    }

    [Test]
    public async Task Подгрузка_доводит_обложку_до_всех_видов_строки_разом()
    {
        var provider = new GameBoxArtProvider(_cache, NullLogger<GameBoxArtProvider>.Instance);
        var page = Create(provider, Session(0, "Minecraft"));
        page.TrySelectAt(0);

        await page.BoxArtLoading;

        _cache.Downloadable["Minecraft"] = WriteImage("minecraft");
        await provider.LoadAsync(["Minecraft"], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions[0].BoxArt.HasImage, Is.True);
            Assert.That(page.Segments[0].BoxArt.HasImage, Is.True);
            Assert.That(page.DetailBoxArt.HasImage, Is.True);
        }
    }

    [Test]
    public async Task Быстрое_переключение_фильтра_не_роняет_страницу_и_отменяет_прошлую_подгрузку()
    {
        _cache.WaitForGate = true;
        _cache.Downloadable["Just Chatting"] = WriteImage("just-chatting");
        _cache.Downloadable["Minecraft"] = WriteImage("minecraft");

        var page = Create(Session(0, "Just Chatting"), Session(1, "Minecraft"));
        var first = page.BoxArtLoading;

        page.FilterByGameCommand.Execute(page.TopCategories[0]);
        page.ClearGameFilterCommand.Execute(null);
        page.FilterByGameCommand.Execute(page.TopCategories[0]);

        var filtered = page.TopCategories[0].Game;

        _cache.Gate.SetResult();
        await first;
        await page.BoxArtLoading;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasGameFilter, Is.True);
            Assert.That(page.Sessions, Has.Count.EqualTo(1));
            Assert.That(page.Sessions[0].BoxArt.HasImage, Is.True, "Последняя пересборка довела свою обложку");
            Assert.That(page.Sessions.All(row => string.Equals(row.GameFormatted, filtered, StringComparison.Ordinal)), Is.True);
        }
    }

    [Test]
    public void Провайдер_отдаёт_одну_ручку_на_имя_игры_независимо_от_регистра_и_пробелов()
    {
        var provider = new GameBoxArtProvider(_cache, NullLogger<GameBoxArtProvider>.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.For(" Minecraft "), Is.SameAs(provider.For("minecraft")));
            Assert.That(provider.For(null), Is.SameAs(GameBoxArtViewModel.None));
            Assert.That(provider.For("   "), Is.SameAs(GameBoxArtViewModel.None));
        }
    }

    [Test]
    public async Task Отменённая_подгрузка_обложку_не_ставит()
    {
        var provider = new GameBoxArtProvider(_cache, NullLogger<GameBoxArtProvider>.Instance);
        var handle = provider.For("Minecraft");

        _cache.Downloadable["Minecraft"] = WriteImage("minecraft");
        _cache.WaitForGate = true;

        using var cancellation = new CancellationTokenSource();
        var loading = provider.LoadAsync(["Minecraft"], cancellation.Token);

        await cancellation.CancelAsync();
        _cache.Gate.SetResult();
        await loading;

        Assert.That(handle.HasImage, Is.False);
    }

    private string WriteImage(string name)
    {
        var path = Path.Combine(_directory, name + ".jpg");
        var source = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr24, null, new byte[2 * 2 * 3], 2 * 3);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = File.Create(path);
        encoder.Save(stream);

        return path;
    }

    private static StreamSessionRecord Session(int index, string? game)
    {
        var started = Start.AddDays(index);

        return new()
        {
            Channel = "bobito217",
            StartedAt = started,
            EndedAt = started.AddHours(3),
            Title = "эфир",
            Game = game,
            MessageCount = 100,
            ChatterCount = 1,
            PeakViewers = 10 + index,
            AverageViewers = 5,
            Segments =
            [
                new()
                {
                    StartedAt = started,
                    EndedAt = started.AddHours(3),
                    Title = "эфир",
                    Game = game,
                    MessageCount = 100,
                    PeakViewers = 10 + index,
                    AverageViewers = 5,
                },
            ],
        };
    }

    private StreamHistoryPageViewModel Create(params StreamSessionRecord[] sessions)
    {
        return Create(new GameBoxArtProvider(_cache, NullLogger<GameBoxArtProvider>.Instance), sessions);
    }

    private StreamHistoryPageViewModel Create(GameBoxArtProvider boxArt, params StreamSessionRecord[] sessions)
    {
        var store = new StreamSessionHistoryStore(filePath: Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".json"));

        foreach (var session in sessions)
        {
            store.Append(session);
        }

        var users = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);

        return new(
            store,
            users,
            new MemorySettings(),
            boxArt,
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance));
    }
}
