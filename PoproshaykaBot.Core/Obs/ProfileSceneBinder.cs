using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Infrastructure.Events.Obs;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using System.Threading.Channels;

namespace PoproshaykaBot.Core.Obs;

public sealed class ProfileSceneBinder :
    IEventHandler<BroadcastProfileApplying>,
    IEventHandler<ObsCurrentProgramSceneChanged>,
    IEventHandler<ObsIntegrationSettingsChangedEvent>,
    IEventSubscriber,
    IDisposable,
    IAsyncDisposable
{
    public static readonly TimeSpan DefaultSceneApplyDebounce = TimeSpan.FromMilliseconds(2500);
    public static readonly TimeSpan DefaultEchoSuppressionWindow = TimeSpan.FromSeconds(5);

    private readonly BroadcastProfilesStore _profilesStore;
    private readonly BroadcastProfilesManager _manager;
    private readonly IObsSceneController _obs;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProfileSceneBinder> _logger;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _echoWindow;
    private readonly Channel<string> _sceneChannel;
    private readonly List<IDisposable> _subscriptions;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;

    private readonly object _settingsLock = new();

    private readonly object _echoLock = new();
    private bool _disposed;
    private ObsIntegrationSettings _settings;
    private string? _expectedEchoScene;
    private DateTimeOffset _expectedEchoAt;

    public ProfileSceneBinder(
        BroadcastProfilesStore profilesStore,
        BroadcastProfilesManager manager,
        IObsSceneController obs,
        ObsIntegrationStore obsStore,
        IEventBus eventBus,
        TimeProvider timeProvider,
        ILogger<ProfileSceneBinder> logger)
        : this(profilesStore,
            manager,
            obs,
            obsStore,
            eventBus,
            timeProvider,
            logger,
            DefaultSceneApplyDebounce,
            DefaultEchoSuppressionWindow)
    {
    }

    internal ProfileSceneBinder(
        BroadcastProfilesStore profilesStore,
        BroadcastProfilesManager manager,
        IObsSceneController obs,
        ObsIntegrationStore obsStore,
        IEventBus eventBus,
        TimeProvider timeProvider,
        ILogger<ProfileSceneBinder> logger,
        TimeSpan sceneApplyDebounce,
        TimeSpan echoSuppressionWindow)
    {
        _profilesStore = profilesStore;
        _manager = manager;
        _obs = obs;
        _timeProvider = timeProvider;
        _logger = logger;
        _debounce = sceneApplyDebounce;
        _echoWindow = echoSuppressionWindow;
        _settings = obsStore.Load();

        _sceneChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscriptions =
        [
            eventBus.Subscribe<BroadcastProfileApplying>(this),
            eventBus.Subscribe<ObsCurrentProgramSceneChanged>(this),
            eventBus.Subscribe<ObsIntegrationSettingsChangedEvent>(this),
        ];

        _workerTask = Task.Run(RunWorkerAsync, _cts.Token);
    }

    private ObsIntegrationSettings CurrentSettings
    {
        get
        {
            lock (_settingsLock)
            {
                return _settings;
            }
        }
    }

    public Task HandleAsync(BroadcastProfileApplying @event, CancellationToken cancellationToken)
    {
        if (!CurrentSettings.ApplySceneOnProfile)
        {
            return Task.CompletedTask;
        }

        var scene = @event.Profile.ObsSceneName?.Trim();
        if (string.IsNullOrEmpty(scene))
        {
            return Task.CompletedTask;
        }

        if (!_obs.IsConnected)
        {
            _logger.LogDebug("ProfileSceneBinder: OBS не подключён — сцена «{Scene}» не переключена", scene);
            return Task.CompletedTask;
        }

        RecordEchoSuppression(scene);
        _ = SwitchSceneSafeAsync(scene, @event.Profile.Name);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ObsCurrentProgramSceneChanged @event, CancellationToken cancellationToken)
    {
        if (!CurrentSettings.ApplyProfileOnScene)
        {
            return Task.CompletedTask;
        }

        if (ConsumeEchoSuppression(@event.SceneName))
        {
            _logger.LogDebug("ProfileSceneBinder: эхо собственного переключения на «{Scene}» подавлено",
                @event.SceneName);

            return Task.CompletedTask;
        }

        _sceneChannel.Writer.TryWrite(@event.SceneName);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ObsIntegrationSettingsChangedEvent @event, CancellationToken cancellationToken)
    {
        lock (_settingsLock)
        {
            _settings = @event.Settings;
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!BeginDispose())
        {
            return;
        }

        try
        {
            await _workerTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "ProfileSceneBinder: воркер завершился с ошибкой при остановке");
        }

        _cts.Dispose();
    }

    public void Dispose()
    {
        if (!BeginDispose())
        {
            return;
        }

        try
        {
            _workerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // штатная отмена воркера наблюдена
        }

        _cts.Dispose();
    }

    private bool BeginDispose()
    {
        if (_disposed)
        {
            return false;
        }

        _disposed = true;

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _sceneChannel.Writer.TryComplete();
        _cts.Cancel();
        return true;
    }

    private async Task SwitchSceneSafeAsync(string scene, string profileName)
    {
        try
        {
            await _obs.SetCurrentSceneAsync(scene, _cts.Token).ConfigureAwait(false);
            _logger.LogInformation("ProfileSceneBinder: профиль «{Profile}» → сцена OBS «{Scene}»",
                profileName,
                scene);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // штатная остановка
        }
        catch (ObjectDisposedException)
        {
            // остановка во время выключения
        }
        catch (Exception exception)
        {
            ClearEchoSuppression(scene);
            _logger.LogWarning(exception, "ProfileSceneBinder: не удалось переключить сцену OBS на «{Scene}»", scene);
        }
    }

    private async Task RunWorkerAsync()
    {
        try
        {
            await foreach (var scene in _sceneChannel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await Task.Delay(_debounce, _timeProvider, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (_sceneChannel.Reader.TryPeek(out _))
                {
                    continue;
                }

                await ApplyProfileForSceneAsync(scene, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // штатная остановка
        }
    }

    private async Task ApplyProfileForSceneAsync(string sceneName, CancellationToken cancellationToken)
    {
        var snapshot = _profilesStore.Load();
        var trimmed = sceneName.Trim();

        var profile = snapshot.Profiles.Find(p =>
            !string.IsNullOrWhiteSpace(p.ObsSceneName)
            && string.Equals(p.ObsSceneName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            _logger.LogDebug("ProfileSceneBinder: сцена «{Scene}» не привязана ни к одному профилю", trimmed);
            return;
        }

        if (snapshot.LastAppliedProfileId == profile.Id)
        {
            _logger.LogDebug("ProfileSceneBinder: профиль «{Profile}» уже активен — повторное применение пропущено",
                profile.Name);

            return;
        }

        try
        {
            _logger.LogInformation("ProfileSceneBinder: сцена «{Scene}» → применяю профиль «{Profile}»",
                trimmed,
                profile.Name);

            await _manager.ApplyAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "ProfileSceneBinder: не удалось применить профиль «{Profile}» по смене сцены",
                profile.Name);
        }
    }

    private void RecordEchoSuppression(string scene)
    {
        lock (_echoLock)
        {
            _expectedEchoScene = scene;
            _expectedEchoAt = _timeProvider.GetUtcNow();
        }
    }

    private void ClearEchoSuppression(string scene)
    {
        lock (_echoLock)
        {
            if (string.Equals(_expectedEchoScene, scene, StringComparison.OrdinalIgnoreCase))
            {
                _expectedEchoScene = null;
            }
        }
    }

    private bool ConsumeEchoSuppression(string scene)
    {
        lock (_echoLock)
        {
            if (_expectedEchoScene is null)
            {
                return false;
            }

            var matched = string.Equals(_expectedEchoScene, scene.Trim(), StringComparison.OrdinalIgnoreCase)
                          && _timeProvider.GetUtcNow() - _expectedEchoAt <= _echoWindow;

            if (matched)
            {
                _expectedEchoScene = null;
            }

            return matched;
        }
    }
}
