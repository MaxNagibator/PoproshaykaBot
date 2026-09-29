using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Update;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Update;

namespace PoproshaykaBot.Core.Update;

public sealed class UpdateCoordinator(
    UpdateChecker checker,
    IUpdateInstaller installer,
    UpdateStore store,
    IEventBus eventBus,
    IUpdateEnvironment environment,
    ILogger<UpdateCoordinator> logger)
    : IUpdateCoordinator, IDisposable
{
    private readonly SemaphoreSlim _prepareGate = new(1, 1);
    private readonly object _syncLock = new();

    private UpdateCandidate? _latestCandidate;

    public UpdateKind Kind => environment.Kind;

    public bool IsUpdatable => environment.Kind switch
    {
        UpdateKind.Portable => true,
        UpdateKind.FrameworkDependent => store.Load().AllowFrameworkDependentUpdate,
        _ => false,
    };

    public string DefaultRepositorySlug => environment.RepositorySlug;

    public Version CurrentVersion => environment.CurrentVersion;

    public UpdateCandidate? LatestCandidate
    {
        get
        {
            lock (_syncLock)
            {
                return _latestCandidate;
            }
        }
    }

    public bool HasPreparedUpdate => installer.ReadPending() is not null;

    public string? PreparedVersion => installer.ReadPending()?.Version;

    public void Dispose()
    {
        _prepareGate.Dispose();
    }

    public async Task<UpdateCandidate?> CheckNowAsync(CancellationToken cancellationToken)
    {
        var skippedVersion = store.Load().SkippedVersion;

        UpdateCandidate? candidate;

        try
        {
            candidate = await checker.CheckAsync(skippedVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Не удалось проверить наличие обновлений");
            return null;
        }

        var settings = store.Load();
        settings.LastCheckUtc = DateTimeOffset.UtcNow;
        store.Save(settings);

        if (candidate is null)
        {
            return null;
        }

        if (string.Equals(settings.SkippedVersion, candidate.Version.ToString(), StringComparison.Ordinal))
        {
            logger.LogInformation("Версия {Version} пропущена, пока шла проверка обновлений", candidate.Version);
            return null;
        }

        lock (_syncLock)
        {
            _latestCandidate = candidate;
        }

        await eventBus
            .PublishAsync(new UpdateAvailable(candidate.Version.ToString(), candidate.NotesUrl), cancellationToken)
            .ConfigureAwait(false);

        return candidate;
    }

    public async Task PrepareAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!await PrepareCoreAsync(candidate, progress, false, cancellationToken).ConfigureAwait(false))
        {
            throw new UpdateException($"Версия {candidate.Version} пропущена, обновление отменено.");
        }
    }

    public async Task<bool> TryPrepareSilentlyAsync(UpdateCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (store.Load().ApplyMode != UpdateApplyMode.SilentOnExit || !IsUpdatable)
        {
            return false;
        }

        var version = candidate.Version.ToString();
        var alreadyPrepared = string.Equals(PreparedVersion, version, StringComparison.Ordinal);
        bool prepared;

        try
        {
            prepared = await PrepareCoreAsync(candidate, null, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Не удалось загрузить обновление {Version} в фоне, оно останется доступным для ручной установки", version);
            return false;
        }

        if (prepared && alreadyPrepared)
        {
            logger.LogDebug("Обновление {Version} уже загружено и ждёт выхода из приложения", version);
        }
        else if (prepared)
        {
            logger.LogInformation("Обновление {Version} загружено в фоне и будет установлено при выходе из приложения", version);
        }

        return prepared;
    }

    public async Task<bool> DiscardPreparedUpdateAsync(CancellationToken cancellationToken)
    {
        var discarded = DiscardIfIdle(null, "автоустановка при выходе выключена");

        if (discarded is not null)
        {
            await eventBus.PublishAsync(new UpdateDiscarded(discarded), cancellationToken).ConfigureAwait(false);
        }

        return discarded is not null;
    }

    public void SkipVersion(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);

        var settings = store.Load();
        settings.SkippedVersion = version;
        store.Save(settings);

        DiscardIfIdle(version, "версия пропущена");

        lock (_syncLock)
        {
            if (_latestCandidate is not null
                && string.Equals(_latestCandidate.Version.ToString(), version, StringComparison.Ordinal))
            {
                _latestCandidate = null;
            }
        }

        logger.LogInformation("Версия {Version} помечена как пропущенная", version);
    }

    private static string? DiscardReason(UpdateSettings settings, string version, bool silent)
    {
        if (string.Equals(settings.SkippedVersion, version, StringComparison.Ordinal))
        {
            return "версия пропущена";
        }

        return silent && settings.ApplyMode != UpdateApplyMode.SilentOnExit
            ? "автоустановка при выходе выключена"
            : null;
    }

    private async Task<bool> PrepareCoreAsync(
        UpdateCandidate candidate,
        IProgress<int>? progress,
        bool silent,
        CancellationToken cancellationToken)
    {
        if (!IsUpdatable)
        {
            throw new UpdateException("Эта сборка не обновляется автоматически.");
        }

        var version = candidate.Version.ToString();

        await _prepareGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        string? discardReason;
        var discarded = false;

        try
        {
            if (string.Equals(PreparedVersion, version, StringComparison.Ordinal))
            {
                progress?.Report(100);
            }
            else
            {
                await installer.PrepareAsync(candidate, progress, cancellationToken).ConfigureAwait(false);
            }

            discardReason = DiscardReason(store.Load(), version, silent);

            if (discardReason is not null)
            {
                discarded = TryDiscard(version, discardReason);
            }
        }
        finally
        {
            _prepareGate.Release();
        }

        if (discardReason is not null)
        {
            if (discarded)
            {
                await eventBus.PublishAsync(new UpdateDiscarded(version), cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        await eventBus.PublishAsync(new UpdatePrepared(version), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private string? DiscardIfIdle(string? version, string reason)
    {
        if (!_prepareGate.Wait(0))
        {
            logger.LogDebug("Идёт загрузка обновления – удаление загруженного ({Reason}) проверит она сама", reason);
            return null;
        }

        try
        {
            var prepared = PreparedVersion;

            if (prepared is null
                || (version is not null && !string.Equals(prepared, version, StringComparison.Ordinal))
                || !TryDiscard(prepared, reason))
            {
                return null;
            }

            return prepared;
        }
        finally
        {
            _prepareGate.Release();
        }
    }

    private bool TryDiscard(string version, string reason)
    {
        if (!installer.DiscardPending(version))
        {
            logger.LogWarning("Не удалось удалить загруженное обновление {Version} ({Reason}) – оно установится при выходе из приложения", version, reason);
            return false;
        }

        logger.LogInformation("Загруженное обновление {Version} удалено: {Reason}", version, reason);
        return true;
    }
}
