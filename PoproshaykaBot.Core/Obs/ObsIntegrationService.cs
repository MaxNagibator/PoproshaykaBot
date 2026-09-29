using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;

namespace PoproshaykaBot.Core.Obs;

public sealed class ObsIntegrationService(
    IObsWebSocketClient client,
    SettingsManager settingsManager,
    ILogger<ObsIntegrationService> logger)
    : IDisposable, IObsSceneController
{
    private const string BrowserSourceInputKind = "browser_source";
    private const string ConnectFailedMessage = "Не удалось подключиться к OBS WebSocket: {Message}";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] MicrophoneNameMarkers = ["mic", "microphone", "микрофон", "микро"];

    private static readonly Action<ILogger, string, string, string, string, Exception?> LogBrowserSourceProvisioned =
        LoggerMessage.Define<string, string, string, string>(LogLevel.Information,
            new(1, nameof(LogBrowserSourceProvisioned)),
            "OBS Browser Source {Action}: сцена {SceneName}, источник {SourceName}, URL {Url}");

    private static readonly Action<ILogger, string, Exception?> LogBrowserSourceRefreshFailed =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new(2, nameof(LogBrowserSourceRefreshFailed)),
            "Не удалось обновить Browser Source {SourceName} через refreshnocache");

    private static readonly Action<ILogger, string, Exception?> LogChatSourceRefreshed =
        LoggerMessage.Define<string>(LogLevel.Information,
            new(3, nameof(LogChatSourceRefreshed)),
            "Browser Source {SourceName}: выполнен жёсткий refresh (refreshnocache)");

    private static readonly Action<ILogger, string, Exception?> LogChatSourceRefreshRejected =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new(4, nameof(LogChatSourceRefreshRejected)),
            "OBS отклонил жёсткий refresh для источника {SourceName}: возможно, источник не Browser Source или не существует");

    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private bool _connectFailureLogged;

    private bool _summaryFailureLogged;

    private ObsConnectionSnapshot _currentStatus = ObsConnectionSnapshot.Disconnected();

    private bool _statusObserved;

    public ObsConnectionSnapshot CurrentStatus
    {
        get => _currentStatus;

        private set
        {
            _currentStatus = value;
            _statusObserved = true;
        }
    }

    public bool IsConnected => client.IsConnected;

    public bool IsStatusObserved => _statusObserved;

    // TODO: время последнего события OBS (смена сцены, старт записи) – отдельным полем по IObsWebSocketClient.EventReceived,
    // когда карточке диагностики станет мало времени ответа на запрос
    public DateTimeOffset? LastExchangeAt => client.LastExchangeAt;

    public async Task<ObsConnectionSnapshot> ConnectAsync(ObsIntegrationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            return await ConnectCoreAsync(settings, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            CurrentStatus = ObsConnectionSnapshot.Disconnected();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListScenesAsync(ObsIntegrationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            await EnsureConnectedAsync(settings, cts.Token).ConfigureAwait(false);

            var sceneList = await GetSceneListAsync(cts.Token).ConfigureAwait(false);
            return sceneList.Select(scene => scene.Name).ToArray();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListInputNamesAsync(ObsIntegrationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            await EnsureConnectedAsync(settings, cts.Token).ConfigureAwait(false);

            var inputs = await GetInputListAsync(cts.Token).ConfigureAwait(false);
            return
            [
                .. inputs
                    .Select(input => input.Name)
                    .Order(StringComparer.OrdinalIgnoreCase),
            ];
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListBrowserSourceNamesAsync(ObsIntegrationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            await EnsureConnectedAsync(settings, cts.Token).ConfigureAwait(false);

            var inputs = await GetInputListAsync(cts.Token).ConfigureAwait(false);
            return
            [
                .. inputs
                    .Where(input => string.Equals(input.Kind, BrowserSourceInputKind, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(input.UnversionedKind, BrowserSourceInputKind, StringComparison.OrdinalIgnoreCase))
                    .Select(input => input.Name)
                    .Order(StringComparer.OrdinalIgnoreCase),
            ];
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<int> RefreshConfiguredChatSourcesAsync(
        ObsIntegrationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var sourceNames = settings.GetChatRefreshSourceNames();
        if (sourceNames.Count == 0)
        {
            return 0;
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            await EnsureConnectedAsync(settings, cts.Token).ConfigureAwait(false);

            var refreshed = 0;
            foreach (var sourceName in sourceNames)
            {
                try
                {
                    await client.SendRequestAsync("PressInputPropertiesButton",
                            new { inputName = sourceName, propertyName = "refreshnocache" },
                            cts.Token)
                        .ConfigureAwait(false);

                    LogChatSourceRefreshed(logger, sourceName, null);
                    refreshed++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogChatSourceRefreshRejected(logger, sourceName, exception);
                }
            }

            return refreshed;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<ObsDashboardSnapshot> GetDashboardSnapshotAsync(
        ObsIntegrationSettings settings,
        bool connectIfNeeded,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return ObsDashboardSnapshot.Disabled();
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            var connection = await EnsureDashboardConnectionAsync(settings, connectIfNeeded, cts.Token)
                .ConfigureAwait(false);

            if (!connection.IsConnected)
            {
                return ObsDashboardSnapshot.Unavailable(connection);
            }

            try
            {
                var sceneName = await GetCurrentProgramSceneNameAsync(cts.Token).ConfigureAwait(false);
                var streamStatus = await GetStreamStatusAsync(cts.Token).ConfigureAwait(false);
                var recordStatus = await GetRecordStatusAsync(cts.Token).ConfigureAwait(false);
                var audioSources = await GetAudioSourceSnapshotsAsync(settings, cts.Token)
                    .ConfigureAwait(false);

                _summaryFailureLogged = false;

                return new(connection,
                    sceneName,
                    streamStatus.Active,
                    streamStatus.Timecode,
                    streamStatus.Congestion,
                    streamStatus.SkippedFrames,
                    streamStatus.TotalFrames,
                    recordStatus.Active,
                    recordStatus.Paused,
                    recordStatus.Timecode,
                    recordStatus.Bytes,
                    audioSources,
                    DateTimeOffset.Now);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var safeMessage = ToSafeMessage(exception);
                CurrentStatus = ObsConnectionSnapshot.Disconnected(safeMessage);
                logger.Log(_summaryFailureLogged ? LogLevel.Debug : LogLevel.Warning, exception, "Не удалось обновить сводку OBS: {Message}", safeMessage);
                _summaryFailureLogged = true;
                return ObsDashboardSnapshot.Unavailable(CurrentStatus);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<ObsProvisionResult> ProvisionBrowserSourceAsync(
        ObsIntegrationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var cts = CreateTimeoutToken(cancellationToken);
            await EnsureConnectedAsync(settings, cts.Token).ConfigureAwait(false);
            await EnsureBrowserSourceKindAsync(cts.Token).ConfigureAwait(false);

            var sourceName = NormalizeSourceName(settings.SourceName);
            var sceneName = await ResolveSceneNameAsync(settings.SceneName, cts.Token).ConfigureAwait(false);
            var overlayUrl = BuildOverlayUrl();
            var browserSettings = new
            {
                url = overlayUrl,
                width = ClampDimension(settings.Width),
                height = ClampDimension(settings.Height),
                shutdown = false,
                restart_when_active = true,
            };

            var created = await CreateOrUpdateBrowserSourceAsync(sourceName, sceneName, browserSettings, cts.Token)
                .ConfigureAwait(false);

            await TryRefreshBrowserSourceAsync(sourceName, cts.Token).ConfigureAwait(false);

            LogBrowserSourceProvisioned(logger,
                created ? "создан" : "обновлён",
                sceneName,
                sourceName,
                overlayUrl,
                null);

            return new(created, sceneName, sourceName, overlayUrl);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<string?> GetCurrentSceneAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!client.IsConnected)
            {
                return null;
            }

            using var cts = CreateTimeoutToken(cancellationToken);
            return await GetCurrentProgramSceneNameAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SetCurrentSceneAsync(string sceneName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!client.IsConnected)
            {
                throw new InvalidOperationException("OBS не подключён");
            }

            using var cts = CreateTimeoutToken(cancellationToken);
            await client.SendRequestAsync("SetCurrentProgramScene", new { sceneName }, cts.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task StartStreamAsync(CancellationToken cancellationToken)
    {
        return InvokeOutputRequestAsync("StartStream", cancellationToken);
    }

    public Task StopStreamAsync(CancellationToken cancellationToken)
    {
        return InvokeOutputRequestAsync("StopStream", cancellationToken);
    }

    public Task StartRecordAsync(CancellationToken cancellationToken)
    {
        return InvokeOutputRequestAsync("StartRecord", cancellationToken);
    }

    public Task StopRecordAsync(CancellationToken cancellationToken)
    {
        return InvokeOutputRequestAsync("StopRecord", cancellationToken);
    }

    public Task ToggleRecordPauseAsync(CancellationToken cancellationToken)
    {
        return InvokeOutputRequestAsync("ToggleRecordPause", cancellationToken);
    }

    public void Dispose()
    {
        _operationGate.Dispose();
    }

    private static ObsConnectionOptions CreateOptions(ObsIntegrationSettings settings)
    {
        var host = string.IsNullOrWhiteSpace(settings.Host) ? "127.0.0.1" : settings.Host.Trim();
        var port = settings.Port is >= 1 and <= 65535 ? settings.Port : 4455;
        return new(host, port, settings.Password);
    }

    private static CancellationTokenSource CreateTimeoutToken(CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(OperationTimeout);
        return cts;
    }

    private static string NormalizeSourceName(string sourceName)
    {
        return string.IsNullOrWhiteSpace(sourceName) ? "PoproshaykaBot Chat" : sourceName.Trim();
    }

    private static int ClampDimension(int value)
    {
        return Math.Clamp(value, 1, 10000);
    }

    private static string ToSafeMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "превышено время ожидания",
            ObsRequestException requestException => requestException.Message,
            InvalidOperationException invalidOperation => invalidOperation.Message,
            _ => "OBS недоступен или отклонил подключение",
        };
    }

    private static bool IsExpectedConnectionFailure(Exception exception)
    {
        return exception.GetBaseException() is SocketException
        {
            SocketErrorCode: SocketError.ConnectionRefused
            or SocketError.TimedOut
            or SocketError.HostUnreachable
            or SocketError.HostDown
            or SocketError.ConnectionReset,
        };
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return GetOptionalString(element, propertyName) ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }

    private static bool? GetOptionalBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }

    private static long? GetOptionalLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt64(out var parsed)
            ? parsed
            : null;
    }

    private static bool IsMicrophoneInputKind(string kind)
    {
        return kind.Equals("wasapi_input_capture", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("coreaudio_input_capture", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("alsa_input_capture", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("pulse_input_capture", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetMicrophoneCandidateScore(ObsInputInfo input)
    {
        var score = IsMicrophoneInputKind(input.UnversionedKind) ? 100 : 0;

        if (MicrophoneNameMarkers.Any(marker => input.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            score += 50;
        }

        return score;
    }

    private async Task InvokeOutputRequestAsync(string requestType, CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!client.IsConnected)
            {
                throw new InvalidOperationException("OBS не подключён");
            }

            using var cts = CreateTimeoutToken(cancellationToken);
            await client.SendRequestAsync(requestType, null, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<bool> CreateOrUpdateBrowserSourceAsync(
        string sourceName,
        string sceneName,
        object browserSettings,
        CancellationToken cancellationToken)
    {
        var inputs = await GetInputListAsync(cancellationToken).ConfigureAwait(false);
        var existing = inputs.FirstOrDefault(input =>
            string.Equals(input.Name, sourceName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            await client.SendRequestAsync("CreateInput",
                    new
                    {
                        sceneName,
                        inputName = sourceName,
                        inputKind = BrowserSourceInputKind,
                        inputSettings = browserSettings,
                        sceneItemEnabled = true,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }

        if (!string.Equals(existing.Kind, BrowserSourceInputKind, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Источник OBS \"{sourceName}\" уже существует, но это не Browser Source.");
        }

        await client.SendRequestAsync("SetInputSettings",
                new
                {
                    inputName = sourceName,
                    inputSettings = browserSettings,
                    overlay = true,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return false;
    }

    private async Task EnsureConnectedAsync(ObsIntegrationSettings settings, CancellationToken cancellationToken)
    {
        if (client.IsConnected)
        {
            return;
        }

        var snapshot = await client.ConnectAsync(CreateOptions(settings), cancellationToken).ConfigureAwait(false);
        CurrentStatus = await RefreshVersionSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        _connectFailureLogged = false;
        _summaryFailureLogged = false;
    }

    private async Task<ObsConnectionSnapshot> EnsureDashboardConnectionAsync(
        ObsIntegrationSettings settings,
        bool connectIfNeeded,
        CancellationToken cancellationToken)
    {
        if (client.IsConnected)
        {
            if (!CurrentStatus.IsConnected)
            {
                CurrentStatus = new(true, CurrentStatus.ObsVersion, CurrentStatus.ObsWebSocketVersion, null);
            }

            CurrentStatus = await RefreshVersionSnapshotAsync(CurrentStatus, cancellationToken).ConfigureAwait(false);
            return CurrentStatus;
        }

        if (!connectIfNeeded)
        {
            if (CurrentStatus.IsConnected)
            {
                CurrentStatus = ObsConnectionSnapshot.Disconnected();
            }

            return CurrentStatus;
        }

        return await ConnectCoreAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ObsConnectionSnapshot> ConnectCoreAsync(
        ObsIntegrationSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await client.ConnectAsync(CreateOptions(settings), cancellationToken).ConfigureAwait(false);
            CurrentStatus = await RefreshVersionSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _connectFailureLogged = false;
            _summaryFailureLogged = false;
            return CurrentStatus;
        }
        catch (Exception exception)
        {
            var safeMessage = ToSafeMessage(exception);
            CurrentStatus = ObsConnectionSnapshot.Disconnected(safeMessage);

            var level = _connectFailureLogged ? LogLevel.Debug : LogLevel.Warning;
            _connectFailureLogged = true;

            if (IsExpectedConnectionFailure(exception))
            {
                logger.Log(level, ConnectFailedMessage, safeMessage);
            }
            else
            {
                logger.Log(level, exception, ConnectFailedMessage, safeMessage);
            }

            return CurrentStatus;
        }
    }

    private async Task<string?> GetCurrentProgramSceneNameAsync(CancellationToken cancellationToken)
    {
        var response = await client.SendRequestAsync("GetCurrentProgramScene", null, cancellationToken)
            .ConfigureAwait(false);

        return response is null
            ? null
            : GetOptionalString(response.Value, "currentProgramSceneName");
    }

    private async Task<StreamStatusFields> GetStreamStatusAsync(CancellationToken cancellationToken)
    {
        var response = await client.SendRequestAsync("GetStreamStatus", null, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return new(null, null, null, null, null);
        }

        var data = response.Value;
        return new(GetOptionalBool(data, "outputActive"),
            GetOptionalString(data, "outputTimecode"),
            GetOptionalDouble(data, "outputCongestion"),
            GetOptionalLong(data, "outputSkippedFrames"),
            GetOptionalLong(data, "outputTotalFrames"));
    }

    private async Task<RecordStatusFields> GetRecordStatusAsync(CancellationToken cancellationToken)
    {
        var response = await client.SendRequestAsync("GetRecordStatus", null, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return new(null, null, null, null);
        }

        var data = response.Value;
        return new(GetOptionalBool(data, "outputActive"),
            GetOptionalBool(data, "outputPaused"),
            GetOptionalString(data, "outputTimecode"),
            GetOptionalLong(data, "outputBytes"));
    }

    private async Task<IReadOnlyList<ObsAudioSourceSnapshot>> GetAudioSourceSnapshotsAsync(
        ObsIntegrationSettings settings,
        CancellationToken cancellationToken)
    {
        var inputs = await GetInputListAsync(cancellationToken).ConfigureAwait(false);
        var configuredNames = settings.GetDashboardSourceNames();

        if (configuredNames.Count == 0)
        {
            var autoMicrophone = inputs
                .Select(input => new { Input = input, Score = GetMicrophoneCandidateScore(input) })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Input.Name, StringComparer.OrdinalIgnoreCase)
                .Select(candidate => candidate.Input)
                .FirstOrDefault();

            if (autoMicrophone is null)
            {
                return [];
            }

            return [await GetAudioSourceSnapshotAsync(autoMicrophone, cancellationToken).ConfigureAwait(false)];
        }

        var result = new List<ObsAudioSourceSnapshot>();
        foreach (var configuredName in configuredNames)
        {
            var input = inputs.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, configuredName, StringComparison.OrdinalIgnoreCase));

            if (input is null)
            {
                continue;
            }

            result.Add(await GetAudioSourceSnapshotAsync(input, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<ObsAudioSourceSnapshot> GetAudioSourceSnapshotAsync(
        ObsInputInfo input,
        CancellationToken cancellationToken)
    {
        var muteResponse = await client.SendRequestAsync("GetInputMute",
                new { inputName = input.Name },
                cancellationToken)
            .ConfigureAwait(false);

        var volumeResponse = await client.SendRequestAsync("GetInputVolume",
                new { inputName = input.Name },
                cancellationToken)
            .ConfigureAwait(false);

        var muted = muteResponse is not null
                    && (GetOptionalBool(muteResponse.Value, "inputMuted") ?? false);

        return new(input.Name,
            input.UnversionedKind,
            muted,
            volumeResponse is null ? null : GetOptionalDouble(volumeResponse.Value, "inputVolumeDb"),
            volumeResponse is null ? null : GetOptionalDouble(volumeResponse.Value, "inputVolumeMul"));
    }

    private async Task<ObsConnectionSnapshot> RefreshVersionSnapshotAsync(
        ObsConnectionSnapshot fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = await client.SendRequestAsync("GetVersion", null, cancellationToken).ConfigureAwait(false);
            if (version is null)
            {
                return fallback;
            }

            var data = version.Value;
            return fallback with
            {
                ObsVersion = GetOptionalString(data, "obsVersion") ?? fallback.ObsVersion,
                ObsWebSocketVersion = GetOptionalString(data, "obsWebSocketVersion") ?? fallback.ObsWebSocketVersion,
            };
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Не удалось получить версию OBS через GetVersion");
            return fallback;
        }
    }

    private async Task<IReadOnlyList<ObsSceneInfo>> GetSceneListAsync(CancellationToken cancellationToken)
    {
        var response = await client.SendRequestAsync("GetSceneList", null, cancellationToken).ConfigureAwait(false);
        if (response is null || !response.Value.TryGetProperty("scenes", out var scenes))
        {
            return [];
        }

        var result = new List<ObsSceneInfo>();
        using var sceneEnumerator = scenes.EnumerateArray();
        while (sceneEnumerator.MoveNext())
        {
            var scene = sceneEnumerator.Current;
            result.Add(new(GetRequiredString(scene, "sceneName")));
        }

        return result;
    }

    private async Task<string> ResolveSceneNameAsync(string configuredSceneName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(configuredSceneName))
        {
            return configuredSceneName.Trim();
        }

        var response = await client.SendRequestAsync("GetSceneList", null, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            throw new InvalidOperationException("OBS не вернул список сцен.");
        }

        var data = response.Value;
        var current = GetOptionalString(data, "currentProgramSceneName");
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        if (data.TryGetProperty("scenes", out var scenes))
        {
            using var sceneEnumerator = scenes.EnumerateArray();
            while (sceneEnumerator.MoveNext())
            {
                var scene = sceneEnumerator.Current;
                if (scene.ValueKind == JsonValueKind.Object)
                {
                    return GetRequiredString(scene, "sceneName");
                }
            }
        }

        throw new InvalidOperationException("В OBS нет сцен для добавления Browser Source.");
    }

    private async Task<IReadOnlyList<ObsInputInfo>> GetInputListAsync(CancellationToken cancellationToken)
    {
        var response = await client.SendRequestAsync("GetInputList", null, cancellationToken).ConfigureAwait(false);
        if (response is null || !response.Value.TryGetProperty("inputs", out var inputs))
        {
            return [];
        }

        var result = new List<ObsInputInfo>();
        using var inputEnumerator = inputs.EnumerateArray();
        while (inputEnumerator.MoveNext())
        {
            var input = inputEnumerator.Current;
            var inputKind = GetRequiredString(input, "inputKind");
            result.Add(new(GetRequiredString(input, "inputName"),
                inputKind,
                GetOptionalString(input, "unversionedInputKind") ?? inputKind));
        }

        return result;
    }

    private async Task EnsureBrowserSourceKindAsync(CancellationToken cancellationToken)
    {
        var response = await client.SendRequestAsync("GetInputKindList", new { unversioned = true }, cancellationToken).ConfigureAwait(false);
        if (response is null || !response.Value.TryGetProperty("inputKinds", out var inputKinds))
        {
            throw new InvalidOperationException("OBS не вернул список типов источников.");
        }

        var hasBrowserSource = false;
        using var kindEnumerator = inputKinds.EnumerateArray();
        while (kindEnumerator.MoveNext())
        {
            var kind = kindEnumerator.Current;
            if (!string.Equals(kind.GetString(), BrowserSourceInputKind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasBrowserSource = true;
            break;
        }

        if (!hasBrowserSource)
        {
            throw new InvalidOperationException("В OBS недоступен тип Browser Source. Проверьте установку OBS Studio.");
        }
    }

    private async Task TryRefreshBrowserSourceAsync(string sourceName, CancellationToken cancellationToken)
    {
        try
        {
            await client.SendRequestAsync("PressInputPropertiesButton",
                    new { inputName = sourceName, propertyName = "refreshnocache" },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogBrowserSourceRefreshFailed(logger, sourceName, exception);
        }
    }

    private string BuildOverlayUrl()
    {
        var port = settingsManager.Current.Twitch.HttpServerPort;
        return string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/chat");
    }

    private sealed record ObsSceneInfo(string Name);

    private sealed record ObsInputInfo(string Name, string Kind, string UnversionedKind);

    private sealed record StreamStatusFields(
        bool? Active,
        string? Timecode,
        double? Congestion,
        long? SkippedFrames,
        long? TotalFrames);

    private sealed record RecordStatusFields(
        bool? Active,
        bool? Paused,
        string? Timecode,
        long? Bytes);
}
