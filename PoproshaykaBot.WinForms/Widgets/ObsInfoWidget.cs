using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class ObsInfoWidget : UserControl, IDashboardTileHeaderProvider
{
    private const int ConnectedRefreshInterval = 5000;
    private const int DisconnectedRefreshInterval = 15000;
    private const int MinVolumeMeterDelayMs = 30;
    private const int MaxVolumeMeterDelayMs = 1000;
    private static readonly TimeSpan AutoConnectRetryInterval = TimeSpan.FromSeconds(30);

    private readonly List<IDisposable> _subs = [];
    private readonly object _volumeMeterLock = new();
    private ObsIntegrationSettings _settings = new();
    private CancellationTokenSource? _refreshCts;
    private DateTimeOffset _lastAutoConnectAttempt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastVolumeMeterEventAt = DateTimeOffset.MinValue;
    private bool _initialized;
    private bool _refreshing;
    private bool _refreshQueued;
    private string? _activeMicrophoneName;
    private bool? _currentMicrophoneMuted;
    private double _targetVolumeMeterLevel;
    private double _currentVolumeMeterLevel;
    private ToolStripLabel? _connectionStatusLabel;
    private ToolStripButton? _refreshButton;

    public ObsInfoWidget()
    {
        InitializeComponent();
    }

    [Inject]
    public ObsIntegrationStore Store { get; internal init; } = null!;

    [Inject]
    public ObsIntegrationService ObsIntegration { get; internal init; } = null!;

    [Inject]
    public IObsWebSocketClient ObsClient { get; internal init; } = null!;

    [Inject]
    public IEventBus Bus { get; internal init; } = null!;

    [Inject]
    public ILogger<ObsInfoWidget> Logger { get; internal init; } = null!;

    public IReadOnlyList<ToolStripItem> CreateHeaderItems()
    {
        _connectionStatusLabel = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "○ OBS выкл.",
            ToolTipText = "Статус подключения OBS",
            ForeColor = Color.Gray,
        };

        _refreshButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🔄",
            ToolTipText = "Обновить данные OBS",
        };

        _refreshButton.Click += OnRefreshClick;
        return [_connectionStatusLabel, _refreshButton];
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized)
        {
            return;
        }

        if (this.IsInDesignMode())
        {
            return;
        }

        _initialized = true;
        _settings = Store.Load();

        _subs.Add(Bus.SubscribeOnUi<ObsIntegrationSettingsChangedEvent>(this, OnObsIntegrationSettingsChanged));
        ObsClient.EventReceived += OnObsEventReceived;
        _subs.Add(new Subscription(() => ObsClient.EventReceived -= OnObsEventReceived));
        _subs.DisposeOnClose(this);

        ApplyCurrentConnectionState();
        _volumeMeterTimer.Start();
        _ = RefreshSnapshotAsync(connectIfNeeded: ShouldTryAutoConnect());
    }

    private void OnRefreshClick(object? sender, EventArgs e)
    {
        _ = RefreshSnapshotAsync(connectIfNeeded: true);
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _ = RefreshSnapshotAsync(connectIfNeeded: ShouldTryAutoConnect());
    }

    private void OnVolumeMeterPanelResize(object? sender, EventArgs e)
    {
        ApplyVolumeMeterLevel(_currentVolumeMeterLevel);
    }

    private void OnVolumeMeterTimerTick(object? sender, EventArgs e)
    {
        var target = ReadVolumeMeterTarget();
        if (target <= 0D && _currentVolumeMeterLevel <= 0D)
        {
            return;
        }

        var delayMs = ResolveVolumeMeterDelayMs();
        var alpha = Math.Clamp((double)_volumeMeterTimer.Interval / delayMs, 0.02D, 1D);

        if (target > _currentVolumeMeterLevel)
        {
            alpha = Math.Clamp(alpha * 1.8D, 0.02D, 1D);
        }

        var nextLevel = _currentVolumeMeterLevel + (target - _currentVolumeMeterLevel) * alpha;
        if (Math.Abs(nextLevel - target) < 0.003D)
        {
            nextLevel = target;
        }

        ApplyVolumeMeterLevel(nextLevel);
    }

    private void OnObsEventReceived(object? sender, ObsWebSocketEventArgs evt)
    {
        if (string.Equals(evt.EventType, "InputVolumeMeters", StringComparison.Ordinal))
        {
            TryUpdateVolumeMeterFromObsEvent(evt);
            return;
        }

        if (!IsDashboardEvent(evt.EventType))
        {
            return;
        }

        ScheduleRefreshFromObsEvent();
    }

    private static bool TryGetVolumeMeterLevel(JsonElement eventData, string inputName, out double level)
    {
        level = 0;

        if (!eventData.TryGetProperty("inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        using var inputEnumerator = inputs.EnumerateArray();
        while (inputEnumerator.MoveNext())
        {
            var input = inputEnumerator.Current;
            if (!string.Equals(GetOptionalString(input, "inputName"), inputName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (input.TryGetProperty("inputLevelsMul", out var levelsMul)
                && TryGetMaxNumber(levelsMul, out var maxMultiplier))
            {
                level = Math.Clamp(maxMultiplier, 0D, 1D);
                return true;
            }

            if (input.TryGetProperty("inputLevelsDb", out var levelsDb)
                && TryGetMaxNumber(levelsDb, out var maxDecibels))
            {
                level = NormalizeDecibelsToMeter(maxDecibels);
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryGetMaxNumber(JsonElement element, out double max)
    {
        max = double.NegativeInfinity;

        if (element.ValueKind == JsonValueKind.Number)
        {
            max = element.GetDouble();
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var found = false;
        using var childEnumerator = element.EnumerateArray();
        while (childEnumerator.MoveNext())
        {
            var child = childEnumerator.Current;
            if (!TryGetMaxNumber(child, out var childMax))
            {
                continue;
            }

            max = Math.Max(max, childMax);
            found = true;
        }

        return found;
    }

    private static double NormalizeDecibelsToMeter(double decibels)
    {
        return Math.Clamp((decibels + 60D) / 60D, 0D, 1D);
    }

    private static Color ResolveVolumeMeterColor(double normalized)
    {
        if (normalized >= 0.9D)
        {
            return Color.Firebrick;
        }

        if (normalized >= 0.68D)
        {
            return Color.DarkOrange;
        }

        return Color.SeaGreen;
    }

    private static string ToDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool IsDashboardEvent(string eventType)
    {
        return eventType is "CurrentProgramSceneChanged"
            or "StreamStateChanged"
            or "RecordStateChanged"
            or "InputMuteStateChanged"
            or "InputVolumeChanged"
            or "InputNameChanged"
            or "InputCreated"
            or "InputRemoved";
    }

    private static string FormatActiveState(bool? active)
    {
        return active switch
        {
            true => "идёт",
            false => "нет",
            _ => "—",
        };
    }

    private static Color ResolveActiveColor(bool? active)
    {
        return active switch
        {
            true => Color.Green,
            false => Color.DimGray,
            _ => Color.Orange,
        };
    }

    private void OnObsIntegrationSettingsChanged(ObsIntegrationSettingsChangedEvent evt)
    {
        _settings = evt.Settings;
        _lastAutoConnectAttempt = DateTimeOffset.MinValue;
        ResetVolumeMeterLevel();
        ApplyCurrentConnectionState();
        _ = RefreshSnapshotAsync(connectIfNeeded: ShouldTryAutoConnect());
    }

    private async Task RefreshSnapshotAsync(bool connectIfNeeded)
    {
        if (!TryBeginRefresh())
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        Interlocked.Exchange(ref _refreshCts, cts)?.Dispose();

        try
        {
            var snapshot = await ObsIntegration.GetDashboardSnapshotAsync(_settings, connectIfNeeded, cts.Token);
            if (IsDisposed)
            {
                return;
            }

            ApplySnapshot(snapshot);
            UpdateRefreshTimer(true, snapshot.IsConnected);
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed)
            {
                return;
            }

            ApplyUnavailableState("превышено время ожидания");
            UpdateRefreshTimer(true, false);
        }
        catch (Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            Logger.LogWarning(exception, "Не удалось обновить виджет OBS");
            ApplyUnavailableState("не удалось получить данные");
            UpdateRefreshTimer(true, false);
        }
        finally
        {
            CompleteRefresh(cts);
        }
    }

    private bool TryBeginRefresh()
    {
        if (_refreshing || IsDisposed)
        {
            if (!IsDisposed)
            {
                _refreshQueued = true;
            }

            return false;
        }

        if (!_settings.Enabled)
        {
            ApplyDisabledState();
            UpdateRefreshTimer(false, false);
            return false;
        }

        _refreshing = true;
        SetRefreshButtonEnabled(false);
        return true;
    }

    private void CompleteRefresh(CancellationTokenSource cts)
    {
        Interlocked.CompareExchange(ref _refreshCts, null, cts);

        _refreshing = false;
        SetRefreshButtonEnabled(true);

        if (_refreshQueued && !IsDisposed)
        {
            _refreshQueued = false;
            _ = RefreshSnapshotAsync(connectIfNeeded: false);
        }
    }

    private void ApplyCurrentConnectionState()
    {
        if (!_settings.Enabled)
        {
            ApplyDisabledState();
            UpdateRefreshTimer(false, false);
            return;
        }

        ApplySnapshot(ObsDashboardSnapshot.Unavailable(ObsIntegration.CurrentStatus));
        UpdateRefreshTimer(true, ObsIntegration.CurrentStatus.IsConnected);
    }

    private void ApplySnapshot(ObsDashboardSnapshot snapshot)
    {
        if (!snapshot.IsConnected)
        {
            ApplyUnavailableState(snapshot.Connection.ErrorMessage);
            return;
        }

        UpdateConnectionHeader("● OBS подключён", Color.Green, "OBS WebSocket подключён");

        _sceneLabel.Text = $"Сцена: {ToDisplayValue(snapshot.CurrentSceneName)}";
        _streamingLabel.Text = $"Эфир: {FormatActiveState(snapshot.IsStreaming)}";
        _streamingLabel.ForeColor = ResolveActiveColor(snapshot.IsStreaming);
        _recordingLabel.Text = $"Запись: {FormatActiveState(snapshot.IsRecording)}";
        _recordingLabel.ForeColor = ResolveActiveColor(snapshot.IsRecording);

        ApplyMicrophone(snapshot.Microphone, _settings.DashboardMicrophoneName);
    }

    private void ApplyDisabledState()
    {
        UpdateConnectionHeader("○ OBS выкл.", Color.Gray, "OBS интеграция отключена в настройках");
        ResetDetails("—");
    }

    private void ApplyUnavailableState(string? message)
    {
        UpdateConnectionHeader("● OBS нет",
            Color.Red,
            string.IsNullOrWhiteSpace(message) ? "OBS WebSocket не подключён" : $"OBS WebSocket не подключён: {message}");

        ResetDetails("—");
    }

    private void ApplyMicrophone(ObsMicrophoneSnapshot? microphone, string configuredMicrophoneName)
    {
        if (microphone is null)
        {
            _activeMicrophoneName = null;
            _currentMicrophoneMuted = null;
            SetMicrophoneState("МИКРОФОН НЕ НАЙДЕН",
                Color.DarkOrange,
                Color.Black);

            ResetVolumeMeterLevel();
            _microphoneLabel.Text = string.IsNullOrWhiteSpace(configuredMicrophoneName)
                ? "Микрофон: не найден"
                : $"Микрофон: {configuredMicrophoneName.Trim()} не найден";

            _microphoneLabel.ForeColor = Color.Orange;
            return;
        }

        _activeMicrophoneName = microphone.Name;
        _currentMicrophoneMuted = microphone.IsMuted;
        SetMicrophoneState(microphone.IsMuted ? "МИКРОФОН ВЫКЛЮЧЕН" : "МИКРОФОН ВКЛЮЧЕН",
            microphone.IsMuted ? Color.Firebrick : Color.SeaGreen,
            Color.White);

        if (microphone.IsMuted)
        {
            ResetVolumeMeterLevel();
        }

        var volume = microphone.VolumeDecibels.HasValue
            ? $" · {microphone.VolumeDecibels.Value:0.#} дБ"
            : string.Empty;

        _microphoneLabel.Text = microphone.IsMuted
            ? $"Микрофон: выключен · {microphone.Name}{volume}"
            : $"Микрофон: включен · {microphone.Name}{volume}";

        _microphoneLabel.ForeColor = microphone.IsMuted ? Color.Red : Color.Green;
    }

    private void ResetDetails(string value)
    {
        _sceneLabel.Text = $"Сцена: {value}";
        _streamingLabel.Text = $"Эфир: {value}";
        _streamingLabel.ForeColor = Color.DimGray;
        _recordingLabel.Text = $"Запись: {value}";
        _recordingLabel.ForeColor = Color.DimGray;
        _activeMicrophoneName = null;
        _currentMicrophoneMuted = null;
        SetMicrophoneState("МИКРОФОН —", Color.Gray, Color.White);
        ResetVolumeMeterLevel();
        _microphoneLabel.Text = $"Микрофон: {value}";
        _microphoneLabel.ForeColor = Color.DimGray;
    }

    private void UpdateConnectionHeader(string text, Color color, string toolTipText)
    {
        if (_connectionStatusLabel is null || IsDisposed)
        {
            return;
        }

        _connectionStatusLabel.Text = text;
        _connectionStatusLabel.ForeColor = color;
        _connectionStatusLabel.ToolTipText = toolTipText;
    }

    private void SetMicrophoneState(string text, Color backColor, Color foreColor)
    {
        _microphoneStateLabel.Text = text;
        _microphoneStateLabel.BackColor = backColor;
        _microphoneStateLabel.ForeColor = foreColor;
    }

    private void TryUpdateVolumeMeterFromObsEvent(ObsWebSocketEventArgs evt)
    {
        if (_currentMicrophoneMuted == true)
        {
            SetVolumeMeterTarget(0);
            return;
        }

        if (evt.EventData is not { } eventData)
        {
            return;
        }

        var microphoneName = ResolveVolumeMeterInputName();
        if (string.IsNullOrWhiteSpace(microphoneName))
        {
            return;
        }

        if (!TryGetVolumeMeterLevel(eventData, microphoneName, out var level))
        {
            return;
        }

        SetVolumeMeterTarget(level);
    }

    private string? ResolveVolumeMeterInputName()
    {
        return string.IsNullOrWhiteSpace(_settings.DashboardMicrophoneName)
            ? _activeMicrophoneName
            : _settings.DashboardMicrophoneName.Trim();
    }

    private double ReadVolumeMeterTarget()
    {
        lock (_volumeMeterLock)
        {
            var delayMs = ResolveVolumeMeterDelayMs();
            var staleAfter = TimeSpan.FromMilliseconds(delayMs * 3D);
            return _lastVolumeMeterEventAt == DateTimeOffset.MinValue
                   || DateTimeOffset.Now - _lastVolumeMeterEventAt > staleAfter
                ? 0D
                : _targetVolumeMeterLevel;
        }
    }

    private void SetVolumeMeterTarget(double level)
    {
        lock (_volumeMeterLock)
        {
            _targetVolumeMeterLevel = Math.Clamp(level, 0D, 1D);
            _lastVolumeMeterEventAt = DateTimeOffset.Now;
        }
    }

    private void ResetVolumeMeterLevel()
    {
        lock (_volumeMeterLock)
        {
            _targetVolumeMeterLevel = 0D;
            _lastVolumeMeterEventAt = DateTimeOffset.MinValue;
        }

        ApplyVolumeMeterLevel(0);
    }

    private void ApplyVolumeMeterLevel(double level)
    {
        var normalized = Math.Clamp(level, 0D, 1D);
        _currentVolumeMeterLevel = normalized;
        var width = (int)Math.Round(_volumeMeterPanel.ClientSize.Width * normalized);

        _volumeMeterFillPanel.Width = Math.Max(0, width);
        _volumeMeterFillPanel.BackColor = ResolveVolumeMeterColor(normalized);
    }

    private int ResolveVolumeMeterDelayMs()
    {
        return Math.Clamp(_settings.DashboardVolumeMeterDelayMs, MinVolumeMeterDelayMs, MaxVolumeMeterDelayMs);
    }

    private void UpdateRefreshTimer(bool enabled, bool connected)
    {
        _refreshTimer.Interval = connected ? ConnectedRefreshInterval : DisconnectedRefreshInterval;

        if (enabled)
        {
            if (!_refreshTimer.Enabled)
            {
                _refreshTimer.Start();
            }

            return;
        }

        if (_refreshTimer.Enabled)
        {
            _refreshTimer.Stop();
        }
    }

    private bool ShouldTryAutoConnect()
    {
        if (!_settings.Enabled || !_settings.AutoConnect || ObsIntegration.CurrentStatus.IsConnected)
        {
            return false;
        }

        var now = DateTimeOffset.Now;
        if (now - _lastAutoConnectAttempt < AutoConnectRetryInterval)
        {
            return false;
        }

        _lastAutoConnectAttempt = now;
        return true;
    }

    private void SetRefreshButtonEnabled(bool enabled)
    {
        if (_refreshButton != null && !IsDisposed)
        {
            _refreshButton.Enabled = enabled;
        }
    }

    private void CancelActiveRefresh()
    {
        _refreshTimer.Stop();
        _volumeMeterTimer.Stop();
        Interlocked.Exchange(ref _refreshCts, null)?.Cancel();
    }

    private void ScheduleRefreshFromObsEvent()
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(() => _ = RefreshSnapshotAsync(connectIfNeeded: false));
        }
        catch (ObjectDisposedException)
        {
            // The widget is closing; a late OBS event can be ignored.
        }
        catch (InvalidOperationException)
        {
            // The handle is being destroyed; the next timer tick will refresh if needed.
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
