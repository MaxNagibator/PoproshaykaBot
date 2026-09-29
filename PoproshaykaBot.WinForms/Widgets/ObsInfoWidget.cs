using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;
using System.Globalization;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class ObsInfoWidget : UserControl, IDashboardTileHeaderProvider
{
    private const int ConnectedRefreshInterval = 5000;
    private const int DisconnectedRefreshInterval = 15000;
    private const int MinVolumeMeterDelayMs = 30;
    private const int MaxVolumeMeterDelayMs = 1000;
    private const int MaxAutoConnectBackoffSteps = 5;
    private const double MaxAutoConnectIntervalSeconds = 300;
    private static readonly TimeSpan AutoConnectRetryInterval = TimeSpan.FromSeconds(30);

    private readonly List<IDisposable> _subs = [];
    private readonly object _volumeMeterLock = new();

    private readonly Dictionary<string, (double Level, DateTimeOffset UpdatedAt)> _volumeTargets =
        new(StringComparer.OrdinalIgnoreCase);

    private ObsIntegrationSettings _settings = new();
    private CancellationTokenSource? _refreshCts;
    private DateTimeOffset _lastAutoConnectAttempt = DateTimeOffset.MinValue;
    private int _autoConnectFailures;
    private bool _initialized;
    private bool _refreshing;
    private bool _refreshQueued;
    private ToolStripLabel? _connectionStatusLabel;
    private ToolStripButton? _refreshButton;
    private ToolStripButton? _refreshChatSourcesButton;

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

        _refreshChatSourcesButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🔁",
            ToolTipText = "Жёстко обновить чат-источники OBS (refreshnocache)",
        };

        _refreshChatSourcesButton.Click += OnRefreshChatSourcesClick;
        return [_connectionStatusLabel, _refreshButton, _refreshChatSourcesButton];
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

        _streamStatusCard.Configure(ObsOutputCardKind.Stream);
        _recordStatusCard.Configure(ObsOutputCardKind.Record);

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

    private void OnRefreshChatSourcesClick(object? sender, EventArgs e)
    {
        _ = RefreshChatSourcesAsync();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _ = RefreshSnapshotAsync(connectIfNeeded: ShouldTryAutoConnect());
    }

    private void OnVolumeMeterTimerTick(object? sender, EventArgs e)
    {
        if (_sourcesLayoutPanel.Controls.Count == 0)
        {
            return;
        }

        var delayMs = ResolveVolumeMeterDelayMs();

        foreach (var meter in _sourcesLayoutPanel.Controls.OfType<ObsSourceMeter>())
        {
            var target = ReadRowTarget(meter.SourceName, meter.Muted, meter.Found, delayMs);
            if (target <= 0D && meter.CurrentLevel <= 0D)
            {
                continue;
            }

            var alpha = Math.Clamp((double)_volumeMeterTimer.Interval / delayMs, 0.02D, 1D);
            if (target > meter.CurrentLevel)
            {
                alpha = Math.Clamp(alpha * 1.8D, 0.02D, 1D);
            }

            var nextLevel = meter.CurrentLevel + (target - meter.CurrentLevel) * alpha;
            if (Math.Abs(nextLevel - target) < 0.003D)
            {
                nextLevel = target;
            }

            meter.ApplyVolumeMeterLevel(nextLevel);
        }
    }

    private void OnObsEventReceived(object? sender, ObsWebSocketEventArgs evt)
    {
        if (string.Equals(evt.EventType, "InputVolumeMeters", StringComparison.Ordinal))
        {
            UpdateVolumeTargetsFromObsEvent(evt);
            return;
        }

        if (!IsDashboardEvent(evt.EventType))
        {
            return;
        }

        ScheduleRefreshFromObsEvent();
    }

    private static string ToDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "–" : value;
    }

    private static (string Text, ObsCardChipTone Tone)? FormatStreamHealth(
        bool? active,
        double? congestion,
        long? skippedFrames,
        long? totalFrames)
    {
        if (active != true)
        {
            return null;
        }

        if (skippedFrames is > 0 && totalFrames is > 0)
        {
            var ratio = (double)skippedFrames.Value / totalFrames.Value;
            if (ratio >= 0.001D)
            {
                return (string.Create(CultureInfo.InvariantCulture,
                    $"дропы · {skippedFrames.Value} ({ratio * 100D:0.0}%)"), ObsCardChipTone.Bad);
            }
        }

        if (congestion is > 0.05D)
        {
            return (string.Create(CultureInfo.InvariantCulture,
                $"сеть · {congestion.Value * 100D:0}%"), ObsCardChipTone.Warn);
        }

        return ("стабильно", ObsCardChipTone.Ok);
    }

    private static string? FormatRecordBytes(bool? active, long? bytes)
    {
        if (active != true || bytes is null or <= 0)
        {
            return null;
        }

        var value = bytes.Value;
        return value switch
        {
            >= 1L << 30 => string.Create(CultureInfo.InvariantCulture,
                $"{value / (double)(1L << 30):0.##} ГБ"),
            >= 1L << 20 => string.Create(CultureInfo.InvariantCulture,
                $"{value / (double)(1L << 20):0.#} МБ"),
            _ => string.Create(CultureInfo.InvariantCulture,
                $"{value / 1024D:0} КБ"),
        };
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

    private static bool TryGetInputLevel(JsonElement input, out double level)
    {
        level = 0;

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

    private static double NormalizeDecibelsToMeter(double decibels)
    {
        return Math.Clamp((decibels + 60D) / 60D, 0D, 1D);
    }

    private async Task RefreshChatSourcesAsync()
    {
        if (_refreshChatSourcesButton is null || IsDisposed)
        {
            return;
        }

        _refreshChatSourcesButton.Enabled = false;

        var configuredCount = _settings.GetChatRefreshSourceNames().Count;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var refreshed = await ObsIntegration.RefreshConfiguredChatSourcesAsync(_settings, cts.Token);

            if (IsDisposed)
            {
                return;
            }

            if (configuredCount == 0)
            {
                ShowChatRefreshToast("🔁 источники не настроены", Color.DarkOrange);
                Logger.LogInformation("Чат-источники для refresh не настроены");
            }
            else if (refreshed == 0)
            {
                ShowChatRefreshToast($"🔁 ни один из {configuredCount} не обновлён", Color.DarkRed);
                Logger.LogWarning("Ручной refresh: OBS отклонил все {Count} запросов – проверьте имена источников", configuredCount);
            }
            else if (refreshed < configuredCount)
            {
                ShowChatRefreshToast($"🔁 обновлено {refreshed}/{configuredCount}", Color.DarkOrange);
            }
            else
            {
                ShowChatRefreshToast($"🔁 обновлено: {refreshed}", Color.DarkGreen);
            }
        }
        catch (OperationCanceledException exception)
        {
            if (IsDisposed)
            {
                return;
            }

            ShowChatRefreshToast("🔁 таймаут", Color.DarkRed);
            Logger.LogWarning(exception, "Ручной refresh чат-источников OBS: превышено время ожидания");
        }
        catch (Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            ShowChatRefreshToast("🔁 ошибка", Color.DarkRed);
            Logger.LogWarning(exception, "Не удалось обновить чат-источники OBS вручную");
        }
        finally
        {
            if (!IsDisposed && _refreshChatSourcesButton is not null)
            {
                _refreshChatSourcesButton.Enabled = true;
            }
        }
    }

    private void ShowChatRefreshToast(string text, Color color)
    {
        if (_connectionStatusLabel is null || IsDisposed)
        {
            return;
        }

        var previousText = _connectionStatusLabel.Text;
        var previousTooltip = _connectionStatusLabel.ToolTipText;

        _connectionStatusLabel.Text = text;
        _connectionStatusLabel.ForeColor = color;
        _connectionStatusLabel.ToolTipText = text;

        _ = RestoreConnectionLabelAfterDelayAsync(previousText, previousTooltip);
    }

    private async Task RestoreConnectionLabelAfterDelayAsync(string previousText, string previousTooltip)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));

        if (IsDisposed || _connectionStatusLabel is null)
        {
            return;
        }

        ApplyCurrentConnectionState();

        if (_connectionStatusLabel.Text == previousText)
        {
            return;
        }

        _connectionStatusLabel.ToolTipText = previousTooltip;
    }

    private void OnObsIntegrationSettingsChanged(ObsIntegrationSettingsChangedEvent evt)
    {
        _settings = evt.Settings;
        _lastAutoConnectAttempt = DateTimeOffset.MinValue;
        ClearRows();
        ApplyCurrentConnectionState();
        _ = RefreshSnapshotAsync(connectIfNeeded: ShouldTryAutoConnect());
    }

    private void UpdateVolumeTargetsFromObsEvent(ObsWebSocketEventArgs evt)
    {
        if (evt.EventData is not { } eventData
            || !eventData.TryGetProperty("inputs", out var inputs)
            || inputs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var now = DateTimeOffset.Now;

        lock (_volumeMeterLock)
        {
            using var inputEnumerator = inputs.EnumerateArray();
            while (inputEnumerator.MoveNext())
            {
                var input = inputEnumerator.Current;
                var inputName = GetOptionalString(input, "inputName");
                if (string.IsNullOrWhiteSpace(inputName) || !TryGetInputLevel(input, out var level))
                {
                    continue;
                }

                _volumeTargets[inputName] = (level, now);
            }
        }
    }

    private double ReadRowTarget(string name, bool muted, bool found, int delayMs)
    {
        if (!found || muted || string.IsNullOrWhiteSpace(name))
        {
            return 0D;
        }

        lock (_volumeMeterLock)
        {
            if (!_volumeTargets.TryGetValue(name, out var target))
            {
                return 0D;
            }

            var staleAfter = TimeSpan.FromMilliseconds(delayMs * 3D);
            return DateTimeOffset.Now - target.UpdatedAt > staleAfter ? 0D : target.Level;
        }
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

            if (connectIfNeeded)
            {
                _autoConnectFailures = snapshot.IsConnected
                    ? 0
                    : Math.Min(_autoConnectFailures + 1, MaxAutoConnectBackoffSteps);
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

        var streamHealth = FormatStreamHealth(snapshot.IsStreaming,
            snapshot.StreamCongestion,
            snapshot.StreamSkippedFrames,
            snapshot.StreamTotalFrames);

        _streamStatusCard.ApplySnapshot(snapshot.IsStreaming,
            null,
            snapshot.StreamTimecode,
            null,
            streamHealth?.Text,
            streamHealth?.Tone ?? ObsCardChipTone.Neutral);

        _recordStatusCard.ApplySnapshot(snapshot.IsRecording,
            snapshot.IsRecordingPaused,
            snapshot.RecordTimecode,
            FormatRecordBytes(snapshot.IsRecording, snapshot.RecordBytes));

        ApplyAudioSources(snapshot.AudioSources);
    }

    private void ApplyAudioSources(IReadOnlyList<ObsAudioSourceSnapshot> audioSources)
    {
        var configured = _settings.GetDashboardSourceNames();
        var orderedNames = configured.Count > 0
            ? configured.ToList()
            : audioSources.Select(source => source.Name).ToList();

        EnsureRows(orderedNames);

        foreach (var meter in _sourcesLayoutPanel.Controls.OfType<ObsSourceMeter>())
        {
            var snapshot = audioSources.FirstOrDefault(source =>
                string.Equals(source.Name, meter.SourceName, StringComparison.OrdinalIgnoreCase));

            if (snapshot is null)
            {
                meter.Found = false;
                meter.Muted = false;
                meter.ShowMissing(meter.SourceName);
                continue;
            }

            meter.Found = true;
            meter.Muted = snapshot.IsMuted;

            if (snapshot.IsMuted)
            {
                meter.ShowMuted(snapshot.Name, snapshot.VolumeDecibels);
            }
            else
            {
                meter.ShowActive(snapshot.Name, snapshot.VolumeDecibels);
            }
        }
    }

    private void EnsureRows(IReadOnlyList<string> orderedNames)
    {
        var currentNames = _sourcesLayoutPanel.Controls
            .OfType<ObsSourceMeter>()
            .Select(meter => meter.SourceName);

        if (currentNames.SequenceEqual(orderedNames, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _sourcesLayoutPanel.SuspendLayout();

        try
        {
            foreach (var child in _sourcesLayoutPanel.Controls.Cast<Control>().ToList())
            {
                child.Dispose();
            }

            _sourcesLayoutPanel.Controls.Clear();
            _sourcesLayoutPanel.RowStyles.Clear();

            if (orderedNames.Count == 0)
            {
                _sourcesLayoutPanel.RowCount = 1;
                _sourcesLayoutPanel.RowStyles.Add(new(SizeType.Absolute, 0F));
                return;
            }

            _sourcesLayoutPanel.RowCount = orderedNames.Count;

            for (var index = 0; index < orderedNames.Count; index++)
            {
                var meter = new ObsSourceMeter
                {
                    SourceName = orderedNames[index],
                    Dock = DockStyle.Top,
                };

                _sourcesLayoutPanel.RowStyles.Add(new(SizeType.AutoSize));
                _sourcesLayoutPanel.Controls.Add(meter, 0, index);
            }
        }
        finally
        {
            _sourcesLayoutPanel.ResumeLayout(true);
        }

        lock (_volumeMeterLock)
        {
            _volumeTargets.Clear();
        }
    }

    private void ClearRows()
    {
        _sourcesLayoutPanel.SuspendLayout();

        try
        {
            foreach (var child in _sourcesLayoutPanel.Controls.Cast<Control>().ToList())
            {
                child.Dispose();
            }

            _sourcesLayoutPanel.Controls.Clear();
            _sourcesLayoutPanel.RowStyles.Clear();
            _sourcesLayoutPanel.RowCount = 1;
            _sourcesLayoutPanel.RowStyles.Add(new(SizeType.Absolute, 0F));
        }
        finally
        {
            _sourcesLayoutPanel.ResumeLayout(true);
        }

        lock (_volumeMeterLock)
        {
            _volumeTargets.Clear();
        }
    }

    private void ApplyDisabledState()
    {
        UpdateConnectionHeader("○ OBS выкл.", Color.Gray, "OBS интеграция отключена в настройках");
        _sceneLabel.Text = "Сцена: –";
        _streamStatusCard.ApplyUnknown();
        _recordStatusCard.ApplyUnknown();
        ClearRows();
    }

    private void ApplyUnavailableState(string? message)
    {
        UpdateConnectionHeader("● OBS нет",
            Color.Red,
            string.IsNullOrWhiteSpace(message) ? "OBS WebSocket не подключён" : $"OBS WebSocket не подключён: {message}");

        _sceneLabel.Text = "Сцена: –";
        _streamStatusCard.ApplyUnavailable(message);
        _recordStatusCard.ApplyUnavailable(message);
        ClearRows();
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
        if (now - _lastAutoConnectAttempt < CurrentAutoConnectInterval())
        {
            return false;
        }

        _lastAutoConnectAttempt = now;
        return true;
    }

    private TimeSpan CurrentAutoConnectInterval()
    {
        var seconds = Math.Min(AutoConnectRetryInterval.TotalSeconds * Math.Pow(2, _autoConnectFailures),
            MaxAutoConnectIntervalSeconds);

        return TimeSpan.FromSeconds(seconds);
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
