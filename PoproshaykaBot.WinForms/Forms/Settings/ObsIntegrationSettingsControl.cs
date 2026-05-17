using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Globalization;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public sealed partial class ObsIntegrationSettingsControl : UserControl
{
    private const string AutoMicrophoneSelectionText = "Авто";

    private bool _initialized;
    private bool _loading;
    private int _httpServerPort = 8080;

    public ObsIntegrationSettingsControl()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingChanged;

    [Inject]
    public ObsIntegrationService ObsIntegration { get; internal init; } = null!;

    [Inject]
    public ILogger<ObsIntegrationSettingsControl> Logger { get; internal init; } = null!;

    public void LoadSettings(ObsIntegrationSettings settings, int httpServerPort)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _loading = true;
        try
        {
            _httpServerPort = httpServerPort;
            _enabledCheckBox.Checked = settings.Enabled;
            _autoConnectCheckBox.Checked = settings.AutoConnect;
            _autoProvisionCheckBox.Checked = settings.AutoProvisionBrowserSource;
            _syncSceneOnProfileCheckBox.Checked = settings.ApplySceneOnProfile;
            _syncProfileOnSceneCheckBox.Checked = settings.ApplyProfileOnScene;
            _hostTextBox.Text = settings.Host;
            _portNumeric.Value = ClampToNumeric(settings.Port, _portNumeric);
            _passwordTextBox.Text = settings.Password;
            _sceneComboBox.Text = settings.SceneName;
            PopulateMicrophoneComboBox(string.IsNullOrWhiteSpace(settings.DashboardMicrophoneName)
                    ? []
                    : [settings.DashboardMicrophoneName.Trim()],
                settings.DashboardMicrophoneName);

            _volumeMeterDelayNumeric.Value = ClampToNumeric(settings.DashboardVolumeMeterDelayMs, _volumeMeterDelayNumeric);
            _sourceNameTextBox.Text = settings.SourceName;
            _widthNumeric.Value = ClampToNumeric(settings.Width, _widthNumeric);
            _heightNumeric.Value = ClampToNumeric(settings.Height, _heightNumeric);

            UpdateOverlayUrl();
            UpdateEnabledState();
            SetStatus("● Не проверено", Color.Gray);
        }
        finally
        {
            _loading = false;
        }
    }

    public void SaveSettings(ObsIntegrationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.Enabled = _enabledCheckBox.Checked;
        settings.AutoConnect = _autoConnectCheckBox.Checked;
        settings.AutoProvisionBrowserSource = _autoProvisionCheckBox.Checked;
        settings.ApplySceneOnProfile = _syncSceneOnProfileCheckBox.Checked;
        settings.ApplyProfileOnScene = _syncProfileOnSceneCheckBox.Checked;
        settings.Host = string.IsNullOrWhiteSpace(_hostTextBox.Text) ? "127.0.0.1" : _hostTextBox.Text.Trim();
        settings.Port = (int)_portNumeric.Value;
        settings.Password = _passwordTextBox.Text;
        settings.SceneName = _sceneComboBox.Text.Trim();
        settings.DashboardMicrophoneName = ReadMicrophoneInputNameFromControl();
        settings.DashboardVolumeMeterDelayMs = (int)_volumeMeterDelayNumeric.Value;
        settings.SourceName = string.IsNullOrWhiteSpace(_sourceNameTextBox.Text)
            ? "PoproshaykaBot Chat"
            : _sourceNameTextBox.Text.Trim();

        settings.Width = (int)_widthNumeric.Value;
        settings.Height = (int)_heightNumeric.Value;
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
        UpdateEnabledState();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        UpdateEnabledState();

        if (!_loading)
        {
            SettingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnConnectionFieldChanged(object? sender, EventArgs e)
    {
        if (!_loading)
        {
            SetStatus("● Не проверено", Color.Gray);
        }

        OnSettingChanged(sender, e);
    }

    private void OnTestConnectionButtonClicked(object? sender, EventArgs e)
    {
        StartObsOperation(async ct =>
        {
            var settings = ReadSettingsFromControls();
            await ConnectAndLoadScenesAsync(settings, "Подключен", ct);
        });
    }

    private void OnReconnectButtonClicked(object? sender, EventArgs e)
    {
        StartObsOperation(async ct =>
        {
            var settings = ReadSettingsFromControls();
            await ObsIntegration.DisconnectAsync(ct);
            await ConnectAndLoadScenesAsync(settings, "Переподключен", ct);
        });
    }

    private void OnLoadScenesButtonClicked(object? sender, EventArgs e)
    {
        StartObsOperation(async ct =>
        {
            var settings = ReadSettingsFromControls();
            await LoadObsListsFromObsAsync(settings, ct);
            SetStatus("● Списки OBS обновлены", Color.Green);
        });
    }

    private void OnProvisionButtonClicked(object? sender, EventArgs e)
    {
        StartObsOperation(async ct =>
        {
            var result = await ObsIntegration.ProvisionBrowserSourceAsync(ReadSettingsFromControls(), ct);
            SetStatus(result.Created
                    ? $"● Источник создан: {result.SourceName}"
                    : $"● Источник обновлён: {result.SourceName}",
                Color.Green);

            MessageBox.Show(this,
                $"""
                 Browser Source готов.

                 Сцена: {result.SceneName}
                 Источник: {result.SourceName}
                 URL: {result.Url}
                 """,
                "OBS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private void OnCopyUrlButtonClicked(object? sender, EventArgs e)
    {
        try
        {
            Clipboard.SetText(BuildOverlayUrl());
            SetStatus("● URL скопирован", Color.Green);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Не удалось скопировать OBS URL");
            MessageBox.Show(this,
                $"Не удалось скопировать URL: {exception.Message}",
                "OBS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static decimal ClampToNumeric(int value, NumericUpDown numeric)
    {
        return Math.Clamp(value, (int)numeric.Minimum, (int)numeric.Maximum);
    }

    private static string ToSafeMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "превышено время ожидания",
            ObsRequestException requestException => requestException.Message,
            InvalidOperationException invalidOperation => invalidOperation.Message,
            _ => "операция не выполнена",
        };
    }

    private static string ToMicrophoneSelectionText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? AutoMicrophoneSelectionText : value.Trim();
    }

    private async Task ConnectAndLoadScenesAsync(
        ObsIntegrationSettings settings,
        string successAction,
        CancellationToken cancellationToken)
    {
        var snapshot = await ObsIntegration.ConnectAsync(settings, cancellationToken);

        if (!snapshot.IsConnected)
        {
            SetStatus($"● Ошибка: {snapshot.ErrorMessage}", Color.DarkRed);
            return;
        }

        var version = string.IsNullOrWhiteSpace(snapshot.ObsVersion) ? "версия не определена" : snapshot.ObsVersion;
        SetStatus($"● {successAction}: OBS {version}", Color.Green);
        await LoadObsListsFromObsAsync(settings, cancellationToken);
    }

    private void StartObsOperation(Func<CancellationToken, Task> operation)
    {
        _ = RunObsOperationAsync(operation);
    }

    private async Task RunObsOperationAsync(Func<CancellationToken, Task> operation)
    {
        ToggleActionButtons(false);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await operation(cts.Token);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Ошибка операции OBS-интеграции");
            SetStatus($"● Ошибка: {ToSafeMessage(exception)}", Color.DarkRed);
        }
        finally
        {
            ToggleActionButtons(true);
        }
    }

    private async Task LoadObsListsFromObsAsync(ObsIntegrationSettings settings, CancellationToken cancellationToken)
    {
        var scenes = await ObsIntegration.ListScenesAsync(settings, cancellationToken);
        var inputNames = await ObsIntegration.ListInputNamesAsync(settings, cancellationToken);

        _loading = true;
        try
        {
            PopulateSceneComboBox(scenes);
            PopulateMicrophoneComboBox(inputNames);
        }
        finally
        {
            _loading = false;
        }
    }

    private ObsIntegrationSettings ReadSettingsFromControls()
    {
        var settings = new ObsIntegrationSettings();
        SaveSettings(settings);
        return settings;
    }

    private void PopulateSceneComboBox(IReadOnlyList<string> scenes)
    {
        var current = _sceneComboBox.Text;

        _sceneComboBox.Items.Clear();
        _sceneComboBox.Items.AddRange(scenes.Cast<object>().ToArray());
        _sceneComboBox.Text = string.IsNullOrWhiteSpace(current) && scenes.Count > 0 ? scenes[0] : current;
    }

    private void PopulateMicrophoneComboBox(IReadOnlyList<string> inputNames)
    {
        PopulateMicrophoneComboBox(inputNames, ReadMicrophoneInputNameFromControl());
    }

    private void PopulateMicrophoneComboBox(IReadOnlyList<string> inputNames, string selectedInputName)
    {
        _microphoneComboBox.Items.Clear();
        _microphoneComboBox.Items.Add(AutoMicrophoneSelectionText);
        _microphoneComboBox.Items.AddRange(inputNames.Cast<object>().ToArray());
        _microphoneComboBox.Text = ToMicrophoneSelectionText(selectedInputName);
    }

    private string ReadMicrophoneInputNameFromControl()
    {
        var value = _microphoneComboBox.Text.Trim();
        return string.Equals(value, AutoMicrophoneSelectionText, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value;
    }

    private void ToggleActionButtons(bool enabled)
    {
        _testConnectionButton.Enabled = enabled;
        _reconnectButton.Enabled = enabled && _enabledCheckBox.Checked;
        _loadScenesButton.Enabled = enabled;
        _provisionButton.Enabled = enabled && _enabledCheckBox.Checked;
        _copyUrlButton.Enabled = enabled;
    }

    private void UpdateEnabledState()
    {
        _autoConnectCheckBox.Enabled = _enabledCheckBox.Checked;
        _autoProvisionCheckBox.Enabled = _enabledCheckBox.Checked;
        _syncSceneOnProfileCheckBox.Enabled = _enabledCheckBox.Checked;
        _syncProfileOnSceneCheckBox.Enabled = _enabledCheckBox.Checked;
        _reconnectButton.Enabled = _enabledCheckBox.Checked;
        _provisionButton.Enabled = _enabledCheckBox.Checked;
    }

    private void UpdateOverlayUrl()
    {
        _overlayUrlTextBox.Text = BuildOverlayUrl();
    }

    private string BuildOverlayUrl()
    {
        return string.Create(CultureInfo.InvariantCulture, $"http://localhost:{_httpServerPort}/chat");
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }
}
