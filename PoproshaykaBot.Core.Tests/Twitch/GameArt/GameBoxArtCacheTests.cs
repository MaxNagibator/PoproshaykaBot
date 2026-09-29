using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Tests.Twitch.Helix;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.GameArt;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Twitch.GameArt;

[TestFixture]
public sealed class GameBoxArtCacheTests
{
    private const string BoxArtTemplate = "https://static-cdn.example/boxart/509658-{width}x{height}.jpg";

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    private string _directory = null!;
    private TestTimeProvider _time = null!;
    private RecordingLogger<GameBoxArtCache> _logger = null!;
    private ITwitchHelixClient _helix = null!;
    private StubHttpMessageHandler _cdn = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-box-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _time = new() { UtcNow = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero) };
        _logger = new();
        _helix = Substitute.For<ITwitchHelixClient>();
        _cdn = ImageHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _cdn.Dispose();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task Загружает_обложку_один_раз_и_дальше_берёт_её_из_кеша()
    {
        Knows("Minecraft");
        var cache = Create();

        var first = await cache.GetPathsAsync(["Minecraft"]);
        var second = await cache.GetPathsAsync(["Minecraft"]);
        var cached = Create().GetCachedPaths(["minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first["Minecraft"], Does.EndWith(".jpg"));
            Assert.That(File.ReadAllBytes(first["Minecraft"]), Is.EqualTo(Jpeg));
            Assert.That(second["Minecraft"], Is.EqualTo(first["Minecraft"]));
            Assert.That(cached["Minecraft"], Is.EqualTo(first["Minecraft"]),
                "кеш ищется по имени игры без учёта регистра");
            Assert.That(_cdn.Requests, Has.Count.EqualTo(1), "второй заход не должен идти в CDN");

            await _helix.Received(1).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Ссылка_запрашивается_в_размере_кеша()
    {
        Knows("Minecraft");

        await Create().GetPathsAsync(["Minecraft"]);

        Assert.That(_cdn.Requests[0].RequestUri!.AbsoluteUri,
            Is.EqualTo("https://static-cdn.example/boxart/509658-104x144.jpg"));
    }

    [Test]
    public async Task Игры_которой_нет_на_Twitch_не_спрашивает_повторно_неделю()
    {
        var cache = Create();

        await cache.GetPathsAsync(["Выдуманная игра"]);
        await cache.GetPathsAsync(["Выдуманная игра"]);

        _time.UtcNow += TimeSpan.FromDays(6);
        await cache.GetPathsAsync(["Выдуманная игра"]);

        await _helix.Received(1).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Промах_переживает_перезапуск_и_истекает_через_неделю()
    {
        await Create().GetPathsAsync(["Выдуманная игра"]);

        var restarted = Create();
        await restarted.GetPathsAsync(["Выдуманная игра"]);

        _time.UtcNow += TimeSpan.FromDays(7);
        await restarted.GetPathsAsync(["Выдуманная игра"]);

        await _helix.Received(2).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Сто_одно_имя_уходит_двумя_запросами()
    {
        var names = Enumerable.Range(0, 101).Select(index => "Игра " + index).ToArray();
        var batches = new List<int>();

        _helix
            .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                batches.Add(call.Arg<IEnumerable<string>>().Count());

                return Task.FromResult<IReadOnlyList<GameInfo>>([]);
            });

        await Create().GetPathsAsync(names);

        Assert.That(batches, Is.EqualTo(new[] { GameBoxArtCache.ResolveBatchSize, 1 }));
    }

    [Test]
    public async Task Без_токена_отдаёт_пустой_результат_и_одно_предупреждение_на_запуск()
    {
        _helix
            .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Throws(new TwitchAuthorizationMissingException("нет токена"));

        var cache = Create();

        var first = await cache.GetPathsAsync(["Minecraft"]);
        var second = await cache.GetPathsAsync(["Dota 2"]);
        var warnings = _logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0].Message, Does.Contain("нет авторизации Twitch"));
            Assert.That(warnings[0].Message, Does.Not.Contain("нет токена"), "текст исключения в журнал не попадает");
        }
    }

    [Test]
    public async Task Сорвавшаяся_загрузка_картинки_не_оставляет_файла_и_не_считается_промахом()
    {
        Knows("Minecraft");
        _cdn.Responder = _ => new(HttpStatusCode.ServiceUnavailable);

        var cache = Create();
        var paths = await cache.GetPathsAsync(["Minecraft"]);
        var leftovers = Directory.EnumerateFiles(_directory).ToArray();

        _cdn.Responder = ImageHandler().Responder;
        var retried = await cache.GetPathsAsync(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Empty);
            Assert.That(leftovers, Is.Empty, "сорвавшаяся загрузка не оставляет ни файла, ни хвоста .tmp");
            Assert.That(retried, Is.Not.Empty, "сорвавшаяся загрузка не запрещает следующую попытку");
        }
    }

    [Test]
    public async Task Отказ_CDN_на_одной_игре_не_лишает_обложки_соседнюю()
    {
        Knows("Minecraft", "Dota 2");

        var image = ImageHandler().Responder!;
        var attempt = 0;

        _cdn.Responder = request => attempt++ == 0
            ? throw new HttpRequestException("сеть недоступна")
            : image(request);

        var paths = await Create().GetPathsAsync(["Minecraft", "Dota 2"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths.ContainsKey("Minecraft"), Is.False, "обложка упавшей игры в результат не попадает");
            Assert.That(paths["Dota 2"], Does.EndWith(".jpg"));
        }
    }

    [Test]
    public async Task Три_отказа_CDN_подряд_обрывают_проход()
    {
        Knows("Игра 1", "Игра 2", "Игра 3", "Игра 4");
        _cdn.Responder = _ => throw new HttpRequestException("сеть недоступна");

        var paths = await Create().GetPathsAsync(["Игра 1", "Игра 2", "Игра 3", "Игра 4"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Empty);
            Assert.That(_cdn.Requests, Has.Count.EqualTo(3), "четвёртая игра батча в CDN уже не идёт");
        }
    }

    [Test]
    public async Task Ответ_не_картинкой_на_диск_не_попадает()
    {
        Knows("Minecraft");

        _cdn.Responder = _ => new(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>ошибка</html>", Encoding.UTF8, "text/html"),
        };

        var paths = await Create().GetPathsAsync(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Empty);
            Assert.That(Directory.EnumerateFiles(_directory, "*.jpg"), Is.Empty);
        }
    }

    [Test]
    public async Task Битое_тело_под_заголовком_картинки_на_диск_не_попадает()
    {
        Knows("Minecraft");

        _cdn.Responder = _ =>
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes("<html>ошибка</html>"));
            content.Headers.ContentType = new("image/jpeg");

            return new(HttpStatusCode.OK) { Content = content };
        };

        var paths = await Create().GetPathsAsync(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Empty);
            Assert.That(Directory.EnumerateFiles(_directory, "*.jpg"), Is.Empty);
        }
    }

    [Test]
    public async Task Испорченная_обложка_убирается_и_качается_заново()
    {
        Knows("Minecraft");

        var cache = Create();
        var first = await cache.GetPathsAsync(["Minecraft"]);

        cache.Invalidate("Minecraft");

        var second = await cache.GetPathsAsync(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Not.Empty);
            Assert.That(second["Minecraft"], Is.EqualTo(first["Minecraft"]));
            Assert.That(_cdn.Requests, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task Картинка_больше_потолка_отбрасывается()
    {
        Knows("Minecraft");

        _cdn.Responder = _ =>
        {
            var content = new ByteArrayContent(new byte[64]);
            content.Headers.ContentType = new("image/jpeg");

            return new(HttpStatusCode.OK) { Content = content };
        };

        var paths = await Create(maxImageBytes: 16).GetPathsAsync(["Minecraft"]);

        Assert.That(paths, Is.Empty);
    }

    [Test]
    public async Task Битый_индекс_не_роняет_кеш_а_читается_как_пустой()
    {
        File.WriteAllText(Path.Combine(_directory, GameBoxArtCache.IndexFileName), "{ это не json");

        var cache = Create();
        var paths = await cache.GetPathsAsync(["Выдуманная игра"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Empty);
            Assert.That(cache.GetStatus().MissCount, Is.EqualTo(1), "промах записан заново поверх битого индекса");

            await _helix.Received(1).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Режим_только_из_кеша_в_сеть_не_ходит()
    {
        Knows("Minecraft");
        await Create().GetPathsAsync(["Minecraft"]);

        _helix.ClearReceivedCalls();
        _cdn.Requests.Clear();

        var gallery = Create(cacheOnly: true);
        var paths = await gallery.GetPathsAsync(["Minecraft", "Dota 2"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths.Keys, Is.EqualTo(new[] { "Minecraft" }));
            Assert.That(_cdn.Requests, Is.Empty);

            await _helix.DidNotReceive().GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Одинаковые_имена_в_одном_запросе_спрашиваются_один_раз()
    {
        Knows("Minecraft");

        var paths = await Create().GetPathsAsync(["Minecraft", "minecraft", " Minecraft ", ""]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Has.Count.EqualTo(1));
            Assert.That(_cdn.Requests, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Параллельные_запросы_одного_имени_качают_обложку_один_раз()
    {
        Knows("Minecraft");
        var cache = Create();

        var results = await Task.WhenAll(
            Task.Run(() => cache.GetPathsAsync(["Minecraft"])),
            Task.Run(() => cache.GetPathsAsync(["Minecraft"])),
            Task.Run(() => cache.GetPathsAsync(["Minecraft"])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Select(result => result["Minecraft"]).Distinct().Count(), Is.EqualTo(1));
            Assert.That(_cdn.Requests, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Состояние_кеша_называет_каталог_и_срок_промаха()
    {
        var status = Create().GetStatus();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.Directory, Is.EqualTo(_directory));
            Assert.That(status.MissLifetime, Is.EqualTo(GameBoxArtCacheOptions.DefaultMissLifetime));
            Assert.That(status.ImageCount, Is.Zero);
            Assert.That(status.CacheOnly, Is.False);
        }
    }

    [Test]
    public async Task Обложка_к_которой_давно_не_обращались_убирается_по_возрасту()
    {
        Knows("Minecraft", "Dota 2");
        var lifetime = TimeSpan.FromDays(90);

        var downloaded = await Create(imageLifetime: lifetime).GetPathsAsync(["Minecraft", "Dota 2"]);

        _time.UtcNow += TimeSpan.FromDays(30);
        Create(imageLifetime: lifetime).GetCachedPaths(["Minecraft"]);

        _time.UtcNow += TimeSpan.FromDays(70);
        var survivors = Create(imageLifetime: lifetime).GetCachedPaths(["Minecraft", "Dota 2"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(survivors.Keys, Is.EqualTo(new[] { "Minecraft" }));
            Assert.That(File.Exists(downloaded["Minecraft"]), Is.True,
                "время последнего обращения переживает перезапуск");
            Assert.That(File.Exists(downloaded["Dota 2"]), Is.False);
        }
    }

    [Test]
    public async Task Потолок_размера_убирает_самые_давние_обложки()
    {
        Knows("Игра 1", "Игра 2", "Игра 3");
        var cache = Create(maxTotalBytes: Jpeg.Length * 2);

        var first = await cache.GetPathsAsync(["Игра 1"]);
        _time.UtcNow += TimeSpan.FromHours(2);
        var second = await cache.GetPathsAsync(["Игра 2"]);
        _time.UtcNow += TimeSpan.FromHours(2);
        var third = await cache.GetPathsAsync(["Игра 3"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(first["Игра 1"]), Is.False, "самая давняя обложка уходит первой");
            Assert.That(File.Exists(second["Игра 2"]), Is.True);
            Assert.That(File.Exists(third["Игра 3"]), Is.True);
            Assert.That(Directory.EnumerateFiles(_directory, "*" + GameBoxArtCache.ImageExtension).Count(),
                Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Убранная_очисткой_обложка_скачивается_заново()
    {
        Knows("Minecraft");
        var lifetime = TimeSpan.FromDays(90);
        var first = await Create(imageLifetime: lifetime).GetPathsAsync(["Minecraft"]);

        _time.UtcNow += TimeSpan.FromDays(100);
        var restarted = Create(imageLifetime: lifetime);
        var afterSweep = restarted.GetCachedPaths(["Minecraft"]);
        var again = await restarted.GetPathsAsync(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterSweep, Is.Empty, "убранная обложка отдаётся как отсутствие, а не путём в никуда");
            Assert.That(again["Minecraft"], Is.EqualTo(first["Minecraft"]));
            Assert.That(File.Exists(again["Minecraft"]), Is.True);
            Assert.That(_cdn.Requests, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void Чужие_файлы_в_каталоге_кеша_очистка_не_трогает()
    {
        var note = Path.Combine(_directory, "readme.txt");
        var alien = Path.Combine(_directory, "cover.jpg");
        var longAgo = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(note, "чужое");
        File.WriteAllBytes(alien, Jpeg);
        File.SetLastWriteTimeUtc(note, longAgo);
        File.SetLastWriteTimeUtc(alien, longAgo);

        Create(maxTotalBytes: 1).GetStatus();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(note), Is.True);
            Assert.That(File.Exists(alien), Is.True, "кеш убирает только файлы, которые сам создал");
        }
    }

    [Test]
    public async Task Отказ_Twitch_запоминается_на_выдержку_и_промахом_не_считается()
    {
        _helix
            .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Throws(new TwitchAuthorizationMissingException("нет токена"));

        var cache = Create(failureBackoff: TimeSpan.FromMinutes(5));

        await cache.GetPathsAsync(["Minecraft"]);
        await cache.GetPathsAsync(["Minecraft"]);

        _time.UtcNow += TimeSpan.FromMinutes(4);
        await cache.GetPathsAsync(["Minecraft"]);
        var duringBackoff = cache.GetStatus().MissCount;

        _time.UtcNow += TimeSpan.FromMinutes(1);
        await cache.GetPathsAsync(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(duringBackoff, Is.Zero, "отказ Twitch промахом не записывается");

            await _helix.Received(2).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Отказ_сети_не_мешает_отдавать_обложки_из_кеша()
    {
        Knows("Minecraft");
        var cache = Create();
        var downloaded = await cache.GetPathsAsync(["Minecraft"]);

        _helix
            .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("сеть недоступна"));

        var offline = await cache.GetPathsAsync(["Minecraft", "Dota 2"]);
        var repeated = await cache.GetPathsAsync(["Minecraft", "Dota 2"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(offline["Minecraft"], Is.EqualTo(downloaded["Minecraft"]));
            Assert.That(repeated.Keys, Is.EqualTo(new[] { "Minecraft" }));

            await _helix.Received(2).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Путь_вычеркнутый_уборкой_в_том_же_проходе_наружу_не_отдаётся()
    {
        Knows("Minecraft");
        var cache = Create(maxTotalBytes: 1);

        var paths = await cache.GetPathsAsync(["Minecraft"]);
        var cached = cache.GetCachedPaths(["Minecraft"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Empty, "уборка снесла только что скачанный файл, пути к нему в выдаче быть не должно");
            Assert.That(cached, Is.Empty);
            Assert.That(Directory.EnumerateFiles(_directory, "*" + GameBoxArtCache.ImageExtension), Is.Empty);
        }
    }

    [TestCase(true, 1, TestName = "Отказ_Helix_взводит_выдержку_и_следующий_запрос_в_Helix_не_идёт")]
    [TestCase(false, 2, TestName = "Отказ_CDN_выдержку_не_взводит_и_следующему_запросу_в_Helix_не_мешает")]
    public async Task Выдержку_взводит_только_отказ_Helix(bool helixFails, int expectedHelixCalls)
    {
        Knows("Minecraft", "Dota 2");

        if (helixFails)
        {
            _helix
                .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Throws(new HttpRequestException("сеть недоступна"));
        }
        else
        {
            _cdn.Responder = _ => throw new HttpRequestException("сеть недоступна");
        }

        var cache = Create(failureBackoff: TimeSpan.FromMinutes(5));

        var first = await cache.GetPathsAsync(["Minecraft"]);
        var second = await cache.GetPathsAsync(["Dota 2"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);

            await _helix.Received(expectedHelixCalls)
                .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Отказ_записи_на_диск_выдержку_не_взводит_и_называется_в_журнале_собой()
    {
        Knows("Minecraft", "Dota 2");
        var cache = Create(failureBackoff: TimeSpan.FromMinutes(5));

        var first = await cache.GetPathsAsync(["Minecraft"]);
        cache.Invalidate("Minecraft");
        Directory.CreateDirectory(first["Minecraft"]);

        var blocked = await cache.GetPathsAsync(["Minecraft"]);
        var next = await cache.GetPathsAsync(["Dota 2"]);
        var warnings = _logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(blocked, Is.Empty, "не записанная на диск обложка путём наружу не уходит");
            Assert.That(next, Is.Not.Empty, "отказ диска не мешает следующей игре");
            Assert.That(warnings.Any(entry => entry.Message.Contains("не сохранена", StringComparison.Ordinal)),
                Is.True, "отказ записи называется в журнале записью");
            Assert.That(warnings.Any(entry => entry.Message.Contains("Обложки игр не загружаются", StringComparison.Ordinal)),
                Is.False, "отказ диска сетевым отказом не объявляется");

            await _helix.Received(3).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Игра_которой_нет_на_Twitch_живёт_правилом_промаха_а_не_выдержкой()
    {
        var cache = Create(failureBackoff: TimeSpan.FromMinutes(5));

        await cache.GetPathsAsync(["Выдуманная игра"]);

        _time.UtcNow += TimeSpan.FromMinutes(10);
        await cache.GetPathsAsync(["Выдуманная игра"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cache.GetStatus().MissCount, Is.EqualTo(1));

            await _helix.Received(1).GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }
    }

    private static StubHttpMessageHandler ImageHandler()
    {
        return new()
        {
            Responder = _ =>
            {
                var content = new ByteArrayContent(Jpeg);
                content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                return new(HttpStatusCode.OK) { Content = content };
            },
        };
    }

    private void Knows(params string[] games)
    {
        _helix
            .GetGamesByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var names = call.Arg<IEnumerable<string>>().ToArray();

                return Task.FromResult<IReadOnlyList<GameInfo>>(
                    games
                        .Where(game => names.Contains(game, StringComparer.OrdinalIgnoreCase))
                        .Select(game => new GameInfo("509658", game, BoxArtTemplate, null))
                        .ToArray());
            });
    }

    private GameBoxArtCache Create(
        bool cacheOnly = false,
        int maxImageBytes = 2 * 1024 * 1024,
        TimeSpan? imageLifetime = null,
        long? maxTotalBytes = null,
        TimeSpan? failureBackoff = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(TwitchEndpoints.BoxArtClient).Returns(_ => new HttpClient(_cdn, disposeHandler: false));

        var options = new GameBoxArtCacheOptions
        {
            Directory = _directory,
            CacheOnly = cacheOnly,
            MaxImageBytes = maxImageBytes,
            ImageLifetime = imageLifetime ?? GameBoxArtCacheOptions.DefaultImageLifetime,
            MaxTotalBytes = maxTotalBytes ?? GameBoxArtCacheOptions.DefaultMaxTotalBytes,
            FailureBackoff = failureBackoff ?? GameBoxArtCacheOptions.DefaultFailureBackoff,
        };

        return new(_helix, factory, _time, options, _logger);
    }
}
