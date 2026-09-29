using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Polls;

namespace PoproshaykaBot.Core.Statistics;

public sealed class StatisticsAutoSaver(
    IUserStatisticsRepository userRepository,
    IBotStatisticsRepository botRepository,
    CommandUsageRepository commandUsageRepository,
    UserStatisticsLoader userLoader,
    StatisticsFileStore fileStore,
    ILogger<StatisticsAutoSaver> logger,
    StreamSessionHistoryStore? historyStore = null,
    PollHistoryStore? pollHistoryStore = null)
    : IHostedComponent, IAsyncDisposable
{
    private static readonly TimeSpan DefaultAutoSaveInterval = TimeSpan.FromMinutes(1);

    private readonly TimeSpan _autoSaveInterval = DefaultAutoSaveInterval;
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);

    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private Task? _autoSaveTask;
    private bool _disposed;
    private bool _botLoaded;
    private StatisticsLoadState _commandUsageState = StatisticsLoadState.NotRead;
    private bool _statisticsRewrittenExternally;
    private bool _statisticsRewriteReported;
    private long _lastRunAtUtcTicks;
    private long _nextRunAtUtcTicks;
    private string? _lastError;

    internal StatisticsAutoSaver(
        IUserStatisticsRepository userRepository,
        IBotStatisticsRepository botRepository,
        CommandUsageRepository commandUsageRepository,
        UserStatisticsLoader userLoader,
        StatisticsFileStore fileStore,
        ILogger<StatisticsAutoSaver> logger,
        TimeSpan autoSaveInterval,
        StreamSessionHistoryStore? historyStore = null,
        PollHistoryStore? pollHistoryStore = null)
        : this(userRepository, botRepository, commandUsageRepository, userLoader, fileStore, logger, historyStore, pollHistoryStore)
    {
        _autoSaveInterval = autoSaveInterval;
    }

    public string Name => "Инициализация статистики...";

    public int StartOrder => 100;

    public TimeSpan Interval => _autoSaveInterval;

    public DateTimeOffset? LastRunAt => ToTimestamp(Interlocked.Read(ref _lastRunAtUtcTicks));

    public DateTimeOffset? NextRunAt => ToTimestamp(Interlocked.Read(ref _nextRunAtUtcTicks));

    public string? LastError => Volatile.Read(ref _lastError);

    public async Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (_autoSaveTask != null)
        {
            logger.LogWarning("Попытка повторного запуска автосохранения статистики проигнорирована");
            return;
        }

        logger.LogDebug("Запуск автосохранения статистики...");

        try
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            botRepository.ResetStartTime();

            _cts = new();
            _periodicTimer = new(_autoSaveInterval);
            Interlocked.Exchange(ref _nextRunAtUtcTicks, (DateTimeOffset.UtcNow + _autoSaveInterval).UtcTicks);
            _autoSaveTask = RunAutoSaveLoopAsync(_cts.Token);

            logger.LogInformation("Автосохранение статистики запущено (интервал: {Interval})", _autoSaveInterval);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Критическая ошибка при запуске автосохранения статистики");
            throw new InvalidOperationException($"Ошибка запуска автосохранения статистики: {exception.Message}", exception);
        }
    }

    public async Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        logger.LogDebug("Инициирована остановка автосохранения статистики");

        if (_cts != null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_autoSaveTask != null)
        {
            try
            {
                await _autoSaveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Задача автосохранения успешно отменена");
            }
        }

        _periodicTimer?.Dispose();
        _periodicTimer = null;
        _autoSaveTask = null;
        Interlocked.Exchange(ref _nextRunAtUtcTicks, 0);

        try
        {
            if (await StopUnderSaveLockAsync(cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation("Автосохранение статистики корректно остановлено");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Ошибка при финальном сохранении статистики");
            throw new InvalidOperationException($"Ошибка остановки автосохранения статистики: {exception.Message}", exception);
        }
    }

    public async Task<T> RunExternalWriteAsync<T>(
        Func<T> write,
        Func<T, StatisticsExternalWrite> rewritten,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(rewritten);

        await _saveSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var invalidation = await InvalidateUnderSaveLockAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var result = write();
                var rewrite = rewritten(result);

                if (rewrite.Statistics)
                {
                    _statisticsRewrittenExternally = true;
                }

                var restore = new StatisticsInvalidation(
                    invalidation.Users && !rewrite.Statistics,
                    invalidation.Bot && !rewrite.Statistics,
                    invalidation.StreamHistory && !rewrite.StreamHistory,
                    invalidation.PollHistory && !rewrite.PollHistory);

                await RestoreUnderSaveLockAsync(restore).ConfigureAwait(false);

                return result;
            }
            catch (Exception exception)
            {
                _statisticsRewrittenExternally = true;

                logger.LogWarning(
                    exception,
                    "Внешняя запись в файлы статистики оборвалась. Статистика, история стримов и история голосований этого сеанса до перезапуска сохраняться не будут: файлы могли остаться переписанными наполовину, и класть поверх них прочитанное до записи нельзя");

                throw;
            }
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    public Task SaveNowAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Принудительный вызов сохранения статистики");
        return SaveAsync(true, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        logger.LogDebug("Освобождение ресурсов StatisticsAutoSaver");

        await StopAsync(new Progress<string>(), CancellationToken.None).ConfigureAwait(false);
        _cts?.Dispose();
        _saveSemaphore.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    private async Task<StatisticsInvalidation> InvalidateUnderSaveLockAsync(CancellationToken cancellationToken)
    {
        var users = await userLoader.InvalidateAsync(cancellationToken).ConfigureAwait(false);
        var bot = _botLoaded;
        _botLoaded = false;

        if (bot)
        {
            logger.LogInformation(
                "Статистика бота помечена непрочитанной: её файл меняют мимо приложения, счётчики этого сеанса сохраняться не будут");
        }

        var history = historyStore?.Invalidate() ?? false;
        var pollHistory = pollHistoryStore?.Invalidate() ?? false;

        return new(users, bot, history, pollHistory);
    }

    private async Task RestoreUnderSaveLockAsync(StatisticsInvalidation invalidation)
    {
        await userLoader.RestoreAsync(invalidation.Users).ConfigureAwait(false);

        if (invalidation.Bot)
        {
            _botLoaded = true;

            logger.LogInformation("Статистика бота снова считается прочитанной: её файл никто не менял");
        }

        historyStore?.Restore(invalidation.StreamHistory);
        pollHistoryStore?.Restore(invalidation.PollHistory);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Начало загрузки статистики");

        await _saveSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await userLoader.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            await LoadCommandUsageAsync(cancellationToken).ConfigureAwait(false);

            if (_statisticsRewrittenExternally)
            {
                if (!_statisticsRewriteReported)
                {
                    _statisticsRewriteReported = true;

                    logger.LogInformation(
                        "Файлы статистики переписаны мимо приложения. До перезапуска статистика бота не перечитывается и не сохраняется, чтобы не затереть перенесённое");
                }

                return;
            }

            var result = await fileStore.LoadBotAsync(cancellationToken).ConfigureAwait(false);

            if (result.Failed)
            {
                _botLoaded = false;

                logger.LogError(
                    "Файл статистики бота не прочитан. Счётчики бота этого сеанса сохраняться не будут, чтобы не затереть файл; рядом с ним оставлена копия с суффиксом invalid");

                return;
            }

            botRepository.Replace(result.Value ?? BotStatistics.Create());
            _botLoaded = true;

            logger.LogInformation("Статистика бота загружена");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Сбой при комплексной загрузке статистики");
            throw new InvalidOperationException($"Ошибка загрузки статистики: {exception.Message}", exception);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    private async Task LoadCommandUsageAsync(CancellationToken cancellationToken)
    {
        if (_commandUsageState != StatisticsLoadState.NotRead)
        {
            return;
        }

        var result = await fileStore.LoadCommandUsageAsync(cancellationToken).ConfigureAwait(false);

        if (result.Failed)
        {
            _commandUsageState = StatisticsLoadState.Abandoned;

            logger.LogError(
                "Файл статистики команд не прочитан. Счётчики команд этого сеанса сохраняться не будут, чтобы не затереть файл; рядом с ним оставлена копия с суффиксом invalid");

            return;
        }

        commandUsageRepository.Replace(result.Value ?? new CommandUsageData());
        _commandUsageState = StatisticsLoadState.Loaded;

        logger.LogInformation("Статистика команд загружена");
    }

    private async Task RunAutoSaveLoopAsync(CancellationToken ct)
    {
        logger.LogDebug("Цикл автосохранения запущен");

        try
        {
            while (await _periodicTimer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await SaveAsync(false, ct).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Сбой тика автосохранения статистики, цикл продолжает работу");
                }

                Interlocked.Exchange(ref _nextRunAtUtcTicks, (DateTimeOffset.UtcNow + _autoSaveInterval).UtcTicks);
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Цикл автосохранения прерван (OperationCanceledException)");
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _lastError, exception.Message);
            logger.LogError(exception, "Непредвиденная ошибка в фоновом цикле автосохранения статистики");
        }
        finally
        {
            Interlocked.Exchange(ref _nextRunAtUtcTicks, 0);
        }
    }

    private async Task<bool> StopUnderSaveLockAsync(CancellationToken cancellationToken)
    {
        await _saveSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_botLoaded && !userLoader.IsLoaded && _commandUsageState != StatisticsLoadState.Loaded)
            {
                historyStore?.TryFlush();
                pollHistoryStore?.TryFlush();

                logger.LogDebug("Остановка автосохранения без финального сохранения: загрузка статистики не выполнялась");

                return false;
            }

            logger.LogDebug("Финальное сохранение статистики при остановке");

            await SaveUnderSaveLockAsync(true, cancellationToken).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    private async Task SaveAsync(bool force, CancellationToken cancellationToken)
    {
        await _saveSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await SaveUnderSaveLockAsync(force, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    private async Task SaveUnderSaveLockAsync(bool force, CancellationToken cancellationToken)
    {
        try
        {
            historyStore?.TryFlush();
            pollHistoryStore?.TryFlush();

            var saveUsers = (force || userRepository.HasChanges) && userLoader.IsLoaded;
            var saveBot = (force || botRepository.HasChanges) && _botLoaded;

            var saveCommands = (force || commandUsageRepository.HasChanges)
                               && _commandUsageState == StatisticsLoadState.Loaded;

            if (!saveUsers && !saveBot && !saveCommands)
            {
                RecordRun(null);
                return;
            }

            List<UserStatistics>? userSnapshot = null;
            BotStatistics? botSnapshot = null;
            CommandUsageData? commandUsageSnapshot = null;

            if (saveUsers)
            {
                userSnapshot = userRepository.CreateSnapshotAndMarkSaved();
            }

            if (saveBot)
            {
                botSnapshot = botRepository.CreateSnapshotAndMarkSaved();
            }

            if (saveCommands)
            {
                commandUsageSnapshot = commandUsageRepository.CreateSnapshotAndMarkSaved();
            }

            try
            {
                if (userSnapshot != null)
                {
                    await fileStore.SaveUsersAsync(userSnapshot, cancellationToken).ConfigureAwait(false);
                    userSnapshot = null;
                }

                if (botSnapshot != null)
                {
                    await fileStore.SaveBotAsync(botSnapshot, cancellationToken).ConfigureAwait(false);
                    botSnapshot = null;
                }

                if (commandUsageSnapshot != null)
                {
                    await fileStore.SaveCommandUsageAsync(commandUsageSnapshot, cancellationToken).ConfigureAwait(false);
                    commandUsageSnapshot = null;
                }
            }
            catch
            {
                if (userSnapshot != null)
                {
                    userRepository.MarkChanged();
                }

                if (botSnapshot != null)
                {
                    botRepository.MarkChanged();
                }

                if (commandUsageSnapshot != null)
                {
                    commandUsageRepository.MarkChanged();
                }

                throw;
            }

            RecordRun(null);
            logger.LogDebug("Сохранение статистики успешно завершено");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RecordRun(exception.Message);
            logger.LogError(exception, "Сбой при попытке сохранения статистики");
            throw new InvalidOperationException($"Ошибка сохранения статистики: {exception.Message}", exception);
        }
    }

    private static DateTimeOffset? ToTimestamp(long utcTicks)
    {
        return utcTicks == 0 ? null : new DateTimeOffset(utcTicks, TimeSpan.Zero);
    }

    private void RecordRun(string? error)
    {
        Interlocked.Exchange(ref _lastRunAtUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        Volatile.Write(ref _lastError, error);
    }
}
