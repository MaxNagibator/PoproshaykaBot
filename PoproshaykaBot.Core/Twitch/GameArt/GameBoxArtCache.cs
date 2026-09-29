using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PoproshaykaBot.Core.Twitch.GameArt;

public sealed class GameBoxArtCache : IGameBoxArtCache
{
    public const int ResolveBatchSize = TwitchHelixClient.MaxGamesPerRequest;
    public const string IndexFileName = "index.json";
    public const string ImageExtension = ".jpg";

    private readonly ITwitchHelixClient _helix;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameBoxArtCache> _logger;
    private readonly GameBoxArtCacheOptions _options;

    private const int FileNameHashLength = 16;
    private const int MaxConsecutiveDownloadFailures = 3;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static readonly string FileNameSuffix =
        $"-{GameBoxArtCacheOptions.ImageWidth}x{GameBoxArtCacheOptions.ImageHeight}{ImageExtension}";

    private static readonly TimeSpan UseWriteInterval = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _network = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _misses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _indexLock = new();

    private bool _indexLoaded;
    private bool _indexDirty;
    private bool _sweepRequested;
    private DateTimeOffset? _unavailableUntil;
    private bool _offlineReported;

    public GameBoxArtCache(
        [FromKeyedServices(TwitchEndpoints.HelixBotClient)]
        ITwitchHelixClient helix,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        GameBoxArtCacheOptions options,
        ILogger<GameBoxArtCache> logger)
    {
        _helix = helix;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, string> GetCachedPaths(IReadOnlyCollection<string> gameNames)
    {
        ArgumentNullException.ThrowIfNull(gameNames);

        EnsureIndexLoaded();
        SweepIfRequested();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in Normalize(gameNames))
        {
            if (TryResolveOnDisk(name, out var path))
            {
                result[name] = path;
            }
        }

        Touch(result.Keys);
        DropVanished(result);
        SaveIndexIfDirty();

        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetPathsAsync(
        IReadOnlyCollection<string> gameNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNames);

        EnsureIndexLoaded();
        SweepIfRequested();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var pending = new List<string>();

            foreach (var name in Normalize(gameNames))
            {
                if (TryResolveOnDisk(name, out var path))
                {
                    result[name] = path;
                }
                else if (ShouldAsk(name))
                {
                    pending.Add(name);
                }
            }

            if (pending.Count == 0 || _options.CacheOnly || !ShouldTryNetwork())
            {
                return result;
            }

            await _network.WaitAsync(cancellationToken);

            try
            {
                await ResolveAsync(pending, result, cancellationToken);
            }
            finally
            {
                _network.Release();
            }

            return result;
        }
        finally
        {
            Touch(result.Keys);
            SweepIfRequested();
            DropVanished(result);
            SaveIndexIfDirty();
        }
    }

    public void Invalidate(string gameName)
    {
        if (gameName is not { Length: > 0 })
        {
            return;
        }

        var name = gameName.Trim();

        if (name.Length == 0)
        {
            return;
        }

        EnsureIndexLoaded();
        _paths.TryRemove(name, out _);

        var fileName = BuildFileName(name);
        var path = Path.Combine(_options.Directory, fileName);

        try
        {
            File.Delete(path);
            Forget(fileName);
            SaveIndexIfDirty();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Не удалось убрать испорченную обложку игры {Game} из кеша", name);
        }
    }

    public GameBoxArtCacheStatus GetStatus()
    {
        EnsureIndexLoaded();
        SweepIfRequested();

        int missCount;

        lock (_indexLock)
        {
            missCount = _misses.Count;
        }

        return new(_options.Directory, CountImages(), missCount, _options.MissLifetime, _options.CacheOnly);
    }

    private static IEnumerable<string> Normalize(IReadOnlyCollection<string> gameNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in gameNames)
        {
            if (raw is not { Length: > 0 })
            {
                continue;
            }

            var name = raw.Trim();

            if (name.Length > 0 && seen.Add(name))
            {
                yield return name;
            }
        }
    }

    private static string BuildFileName(string gameName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(gameName.ToLowerInvariant()));
        var hash = Convert.ToHexStringLower(bytes)[..FileNameHashLength];

        return hash + FileNameSuffix;
    }

    private static bool IsOwnFileName(string fileName)
    {
        if (fileName.Length != FileNameHashLength + FileNameSuffix.Length
            || !fileName.EndsWith(FileNameSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var symbol in fileName.AsSpan(0, FileNameHashLength))
        {
            if (!char.IsAsciiDigit(symbol) && symbol is < 'a' or > 'f')
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildImageUrl(string template)
    {
        return template
            .Replace("{width}", GameBoxArtCacheOptions.ImageWidth.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{height}", GameBoxArtCacheOptions.ImageHeight.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task ResolveAsync(
        List<string> pending,
        Dictionary<string, string> result,
        CancellationToken cancellationToken)
    {
        var todo = new List<string>(pending.Count);

        foreach (var name in pending)
        {
            if (TryResolveOnDisk(name, out var path))
            {
                result[name] = path;
            }
            else if (ShouldAsk(name))
            {
                todo.Add(name);
            }
        }

        if (todo.Count == 0 || !ShouldTryNetwork())
        {
            return;
        }

        var consecutiveFailures = 0;

        foreach (var batch in todo.Chunk(ResolveBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<GameInfo> games;

            try
            {
                games = await _helix.GetGamesByNamesAsync(batch, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReportUnavailable(exception);

                return;
            }

            NoteAvailable();

            var found = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in games)
            {
                if (game.Name is { Length: > 0 })
                {
                    found[game.Name.Trim()] = game;
                }
            }

            foreach (var name in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!found.TryGetValue(name, out var game) || game.BoxArtUrl is not { Length: > 0 } template)
                {
                    RememberMiss(name);

                    continue;
                }

                var download = await TryDownloadAsync(name, template, cancellationToken);

                if (download.Path is { } path)
                {
                    result[name] = path;
                    consecutiveFailures = 0;
                }
                else if (download.NetworkFailed && ++consecutiveFailures >= MaxConsecutiveDownloadFailures)
                {
                    _logger.LogDebug(
                        "Загрузка обложек прервана: подряд {Count} отказа CDN Twitch на загрузке картинки",
                        consecutiveFailures);

                    return;
                }
            }
        }
    }

    private async Task<BoxArtDownload> TryDownloadAsync(string gameName, string template, CancellationToken cancellationToken)
    {
        var url = BuildImageUrl(template);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            _logger.LogDebug("Обложка игры {Game} пропущена: Twitch прислал ссылку, которую нельзя загрузить", gameName);

            return BoxArtDownload.Skipped;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(TwitchEndpoints.BoxArtClient);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Обложка игры {Game} не загружена: ответ {Status}", gameName, (int)response.StatusCode);

                return BoxArtDownload.Skipped;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (mediaType is null || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Обложка игры {Game} не загружена: вместо картинки пришло {MediaType}", gameName, mediaType ?? "–");

                return BoxArtDownload.Skipped;
            }

            if (response.Content.Headers.ContentLength > _options.MaxImageBytes)
            {
                _logger.LogDebug("Обложка игры {Game} не загружена: файл больше допустимого", gameName);

                return BoxArtDownload.Skipped;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var bytes = await ReadBoundedAsync(stream, _options.MaxImageBytes, cancellationToken);

            if (bytes is not { Length: > 0 })
            {
                _logger.LogDebug("Обложка игры {Game} не загружена: пустой или слишком большой ответ", gameName);

                return BoxArtDownload.Skipped;
            }

            if (!LooksLikeImage(bytes))
            {
                _logger.LogDebug("Обложка игры {Game} не загружена: тело ответа не похоже на картинку", gameName);

                return BoxArtDownload.Skipped;
            }

            var path = Path.Combine(_options.Directory, BuildFileName(gameName));

            if (!TrySave(gameName, path, bytes))
            {
                return BoxArtDownload.Skipped;
            }

            _paths[gameName] = path;
            RequestSweep();

            return BoxArtDownload.Saved(path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            ReportDownloadFailed(gameName, exception);

            return BoxArtDownload.Failed;
        }
    }

    private bool TrySave(string gameName, string path, byte[] bytes)
    {
        try
        {
            AtomicFile.Save(path, bytes, _logger);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Обложка игры {Game} не сохранена: не удалось записать файл в {Directory}",
                gameName,
                _options.Directory);

            return false;
        }
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        ReadOnlySpan<byte> head = bytes;

        return head.StartsWith(JpegSignature)
               || head.StartsWith(PngSignature)
               || head.StartsWith("GIF8"u8)
               || (head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8));
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);

            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private bool TryResolveOnDisk(string gameName, out string path)
    {
        if (_paths.TryGetValue(gameName, out var known) && File.Exists(known))
        {
            path = known;

            return true;
        }

        var candidate = Path.Combine(_options.Directory, BuildFileName(gameName));

        if (File.Exists(candidate))
        {
            _paths[gameName] = candidate;
            path = candidate;

            return true;
        }

        _paths.TryRemove(gameName, out _);
        path = string.Empty;

        return false;
    }

    private void DropVanished(Dictionary<string, string> result)
    {
        List<string>? vanished = null;

        foreach (var (gameName, path) in result)
        {
            if (!File.Exists(path))
            {
                (vanished ??= []).Add(gameName);
            }
        }

        if (vanished is null)
        {
            return;
        }

        foreach (var gameName in vanished)
        {
            result.Remove(gameName);
            _paths.TryRemove(gameName, out _);
            Forget(BuildFileName(gameName));
        }

        _logger.LogDebug("Из выдачи кеша обложек вычеркнуто {Count}: файлов на диске больше нет", vanished.Count);
    }

    private bool ShouldAsk(string gameName)
    {
        lock (_indexLock)
        {
            if (!_misses.TryGetValue(gameName, out var missedAt))
            {
                return true;
            }

            var now = _timeProvider.GetUtcNow();

            return missedAt > now || now - missedAt >= _options.MissLifetime;
        }
    }

    private void RememberMiss(string gameName)
    {
        lock (_indexLock)
        {
            _misses[gameName] = _timeProvider.GetUtcNow();
            _indexDirty = true;
        }
    }

    private void Touch(IEnumerable<string> gameNames)
    {
        var now = _timeProvider.GetUtcNow();

        lock (_indexLock)
        {
            foreach (var gameName in gameNames)
            {
                var fileName = BuildFileName(gameName);

                if (_images.TryGetValue(fileName, out var usedAt) && usedAt <= now && now - usedAt < UseWriteInterval)
                {
                    continue;
                }

                _images[fileName] = now;
                _indexDirty = true;
            }
        }
    }

    private void Forget(string fileName)
    {
        lock (_indexLock)
        {
            if (_images.Remove(fileName))
            {
                _indexDirty = true;
            }
        }
    }

    private void RequestSweep()
    {
        lock (_indexLock)
        {
            _sweepRequested = true;
        }
    }

    private bool ShouldTryNetwork()
    {
        DateTimeOffset until;

        lock (_indexLock)
        {
            if (_unavailableUntil is not { } pause || _timeProvider.GetUtcNow() >= pause)
            {
                return true;
            }

            until = pause;
        }

        _logger.LogDebug("Обложки игр не запрашиваются до {Until:HH:mm:ss} UTC: выдержка после отказа Twitch", until);

        return false;
    }

    private void NoteAvailable()
    {
        lock (_indexLock)
        {
            _unavailableUntil = null;
        }
    }

    private int CountImages()
    {
        try
        {
            return Directory.Exists(_options.Directory)
                ? Directory.EnumerateFiles(_options.Directory, "*" + ImageExtension).Count()
                : 0;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Не удалось пересчитать обложки в {Directory}", _options.Directory);

            return 0;
        }
    }

    private void EnsureIndexLoaded()
    {
        lock (_indexLock)
        {
            if (_indexLoaded)
            {
                return;
            }

            _indexLoaded = true;
            _sweepRequested = true;

            var file = Path.Combine(_options.Directory, IndexFileName);

            if (!File.Exists(file))
            {
                return;
            }

            try
            {
                var model = JsonSerializer.Deserialize<GameBoxArtIndex>(File.ReadAllText(file), JsonStoreOptions.Default);

                if (model?.Misses is null)
                {
                    _logger.LogWarning("Список неизвестных категорий в кеше обложек пуст или повреждён, он будет собран заново");

                    return;
                }

                foreach (var (name, missedAt) in model.Misses)
                {
                    if (name is { Length: > 0 })
                    {
                        _misses[name.Trim()] = missedAt;
                    }
                }

                foreach (var (fileName, usedAt) in model.Images ?? [])
                {
                    if (fileName is { Length: > 0 } && IsOwnFileName(fileName))
                    {
                        _images[fileName] = usedAt;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Список неизвестных категорий в кеше обложек не прочитан, он будет собран заново: {File}",
                    file);
            }
        }
    }

    // TODO: уборка каталога идёт под _indexLock на потоке вызывающего, в том числе UI-потоке через GetCachedPaths;
    // TODO: увести в фон, когда каталог дорастёт до тысяч файлов либо список обложек начнёт подвисать
    private void SweepIfRequested()
    {
        lock (_indexLock)
        {
            if (!_sweepRequested)
            {
                return;
            }

            _sweepRequested = false;

            try
            {
                Sweep();
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Не удалось прибраться в кеше обложек {Directory}", _options.Directory);
            }
        }

        SaveIndexIfDirty();
    }

    private void Sweep()
    {
        if (!Directory.Exists(_options.Directory))
        {
            return;
        }

        var kept = new List<CachedImage>();
        var expired = DiscardExpired(kept);
        var crowded = DiscardCrowded(kept, out var keptBytes);

        DropMissingImages();

        if (expired > 0 || crowded > 0)
        {
            _logger.LogInformation(
                "Кеш обложек прибран: убрано по возрасту {Expired}, по потолку размера {Crowded}, осталось {Bytes} Б",
                expired,
                crowded,
                keptBytes);
        }
    }

    private int DiscardExpired(List<CachedImage> kept)
    {
        var now = _timeProvider.GetUtcNow();
        var expired = 0;

        foreach (var path in Directory.EnumerateFiles(_options.Directory, "*" + ImageExtension))
        {
            var file = new FileInfo(path);

            if (!IsOwnFileName(file.Name))
            {
                continue;
            }

            var usedAt = _images.TryGetValue(file.Name, out var known)
                ? known
                : new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);

            if (_options.ImageLifetime > TimeSpan.Zero
                && usedAt <= now
                && now - usedAt >= _options.ImageLifetime
                && TryDiscard(file))
            {
                expired++;

                continue;
            }

            kept.Add(new(file, usedAt));
        }

        return expired;
    }

    // TODO: потолок меньше одной обложки вырождает кеш – скачанное сносится тем же проходом, и наружу
    // не уходит ничего; когда MaxTotalBytes станет пользовательской настройкой, задать ему нижнюю
    // границу в несколько обложек и сообщать о заниженном значении
    private int DiscardCrowded(List<CachedImage> kept, out long keptBytes)
    {
        keptBytes = kept.Sum(image => image.File.Length);

        if (_options.MaxTotalBytes <= 0 || keptBytes <= _options.MaxTotalBytes)
        {
            return 0;
        }

        var crowded = 0;

        foreach (var image in kept.OrderBy(item => item.UsedAt))
        {
            if (keptBytes <= _options.MaxTotalBytes)
            {
                break;
            }

            var length = image.File.Length;

            if (TryDiscard(image.File))
            {
                keptBytes -= length;
                crowded++;
            }
        }

        return crowded;
    }

    private bool TryDiscard(FileInfo file)
    {
        try
        {
            file.Delete();

            if (_images.Remove(file.Name))
            {
                _indexDirty = true;
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Не удалось убрать обложку {File} из кеша", file.Name);

            return false;
        }
    }

    private void DropMissingImages()
    {
        foreach (var fileName in _images.Keys.ToArray())
        {
            if (File.Exists(Path.Combine(_options.Directory, fileName)))
            {
                continue;
            }

            _images.Remove(fileName);
            _indexDirty = true;
        }
    }

    private void SaveIndexIfDirty()
    {
        lock (_indexLock)
        {
            if (!_indexDirty)
            {
                return;
            }

            _indexDirty = false;

            var model = new GameBoxArtIndex
            {
                Misses = new(_misses, StringComparer.OrdinalIgnoreCase),
                Images = new(_images, StringComparer.OrdinalIgnoreCase),
            };

            try
            {
                var file = Path.Combine(_options.Directory, IndexFileName);
                AtomicFile.Save(file, JsonSerializer.Serialize(model, JsonStoreOptions.Default), _logger);
            }
            catch (Exception exception)
            {
                _indexDirty = true;

                _logger.LogWarning(exception, "Список неизвестных категорий в кеше обложек не сохранён");
            }
        }
    }

    private void ReportUnavailable(Exception exception)
    {
        if (_options.FailureBackoff > TimeSpan.Zero)
        {
            lock (_indexLock)
            {
                _unavailableUntil = _timeProvider.GetUtcNow() + _options.FailureBackoff;
            }
        }

        ReportOffline(DescribeFailure(exception));
    }

    private void ReportDownloadFailed(string gameName, Exception exception)
    {
        _logger.LogDebug(exception, "Обложка игры {Game} не загружена: отказ CDN Twitch", gameName);

        ReportOffline(DescribeFailure(exception));
    }

    private void ReportOffline(string reason)
    {
        if (_offlineReported)
        {
            _logger.LogDebug("Обложки игр по-прежнему не загружаются: {Reason}", reason);

            return;
        }

        _offlineReported = true;

        _logger.LogWarning("Обложки игр не загружаются: {Reason}. Названия категорий показываются без картинок", reason);
    }

    private static string DescribeFailure(Exception exception)
    {
        return exception switch
        {
            TwitchAuthorizationMissingException => "нет авторизации Twitch",
            HelixRequestException helix => $"Twitch ответил кодом {(int)helix.StatusCode}",
            _ => "Twitch недоступен",
        };
    }

    private sealed class GameBoxArtIndex
    {
        public Dictionary<string, DateTimeOffset> Misses { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, DateTimeOffset> Images { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly record struct CachedImage(FileInfo File, DateTimeOffset UsedAt);

    private readonly record struct BoxArtDownload(string? Path, bool NetworkFailed)
    {
        public static BoxArtDownload Skipped { get; } = new(null, false);

        public static BoxArtDownload Failed { get; } = new(null, true);

        public static BoxArtDownload Saved(string path)
        {
            return new(path, false);
        }
    }
}
