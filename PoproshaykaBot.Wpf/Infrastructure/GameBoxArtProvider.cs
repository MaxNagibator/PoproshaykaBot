using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Twitch.GameArt;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class GameBoxArtProvider(IGameBoxArtCache cache, ILogger<GameBoxArtProvider> logger)
{
    private readonly Dictionary<string, GameBoxArtViewModel> _handles = new(StringComparer.OrdinalIgnoreCase);

    public GameBoxArtViewModel For(string? game)
    {
        if (game is not { Length: > 0 })
        {
            return GameBoxArtViewModel.None;
        }

        var name = game.Trim();

        if (name.Length == 0)
        {
            return GameBoxArtViewModel.None;
        }

        if (!_handles.TryGetValue(name, out var handle))
        {
            handle = new(name);
            _handles[name] = handle;
        }

        return handle;
    }

    public async Task LoadAsync(IReadOnlyCollection<string> games, CancellationToken cancellationToken)
    {
        var missing = Missing(games);

        if (missing.Count == 0)
        {
            return;
        }

        try
        {
            var cached = await Task.Run(() => Decode(cache.GetCachedPaths(missing)), cancellationToken);
            Apply(cached, cancellationToken);

            missing = Missing(missing);

            if (missing.Count == 0)
            {
                return;
            }

            var loaded = await Task.Run(
                async () => Decode(await cache.GetPathsAsync(missing, cancellationToken).ConfigureAwait(false)),
                cancellationToken);

            Apply(loaded, cancellationToken);
        }
        catch (Exception exception)
        {
            if (exception is not OperationCanceledException)
            {
                logger.BoxArtLoadFailed(exception);
            }
        }
    }

    private List<string> Missing(IEnumerable<string> games)
    {
        return games.Where(game => For(game) is { HasGame: true, HasImage: false }).ToList();
    }

    private List<(string Game, ImageSource Image)> Decode(IReadOnlyDictionary<string, string> paths)
    {
        var images = new List<(string, ImageSource)>(paths.Count);

        foreach (var (game, path) in paths)
        {
            if (TryDecode(path) is { } image)
            {
                images.Add((game, image));
            }
            else
            {
                cache.Invalidate(game);
            }
        }

        return images;
    }

    private void Apply(List<(string Game, ImageSource Image)> images, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        foreach (var (game, image) in images)
        {
            if (For(game) is { HasGame: true, HasImage: false } handle)
            {
                handle.Image = image;
            }
        }
    }

    private ImageSource? TryDecode(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = GameBoxArtCacheOptions.ImageWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception exception)
        {
            logger.BoxArtDecodeFailed(exception, path);

            return null;
        }
    }
}
