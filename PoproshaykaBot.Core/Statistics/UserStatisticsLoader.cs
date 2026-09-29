using Microsoft.Extensions.Logging;

namespace PoproshaykaBot.Core.Statistics;

public sealed class UserStatisticsLoader(
    IUserStatisticsRepository repository,
    StatisticsFileStore fileStore,
    ILogger<UserStatisticsLoader> logger)
    : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile StatisticsLoadState _state = StatisticsLoadState.NotRead;
    private bool _disposed;

    public bool IsLoaded => _state == StatisticsLoadState.Loaded;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_state != StatisticsLoadState.NotRead)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_state != StatisticsLoadState.NotRead)
            {
                return;
            }

            await LoadUnderGateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _state = StatisticsLoadState.Abandoned;

            logger.LogError(exception, "Сбой при загрузке статистики пользователей");
            throw new InvalidOperationException($"Ошибка загрузки статистики пользователей: {exception.Message}", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> InvalidateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var wasLoaded = _state == StatisticsLoadState.Loaded;

            if (wasLoaded)
            {
                _state = StatisticsLoadState.Abandoned;

                logger.LogInformation(
                    "Статистика пользователей больше не считается прочитанной: её файл меняют мимо приложения, накопленное в этом сеансе сохраняться не будет, а перечитывания до перезапуска не будет");
            }

            return wasLoaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreAsync(bool wasLoaded)
    {
        if (!wasLoaded)
        {
            return;
        }

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            _state = StatisticsLoadState.Loaded;

            logger.LogInformation("Статистика пользователей снова считается прочитанной: её файл никто не менял");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
    }

    private async Task LoadUnderGateAsync(CancellationToken cancellationToken)
    {
        var result = await fileStore.LoadUsersAsync(cancellationToken).ConfigureAwait(false);

        if (result.Failed)
        {
            _state = StatisticsLoadState.Abandoned;

            logger.LogError(
                "Файл статистики пользователей не прочитан. Накопленное в этом сеансе сохраняться не будет, чтобы не затереть файл, и перечитывать его до перезапуска приложение не станет; рядом с файлом оставлена копия с суффиксом invalid");

            return;
        }

        var users = result.Value ?? [];
        repository.ReplaceAll(users);
        _state = StatisticsLoadState.Loaded;

        logger.LogInformation("Статистика пользователей загружена. Загружено пользователей: {UserCount}", users.Count);
    }
}
