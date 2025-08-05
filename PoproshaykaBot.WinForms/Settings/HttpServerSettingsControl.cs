namespace PoproshaykaBot.WinForms.Settings;

public partial class HttpServerSettingsControl : UserControl
{
    private static readonly TwitchSettings DefaultSettings = new();

    public HttpServerSettingsControl()
    {
        InitializeComponent();
        SetPlaceholders();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(TwitchSettings settings)
    {
        _httpServerEnabledCheckBox.Checked = settings.HttpServerEnabled;
        _httpServerPortNumeric.Value = settings.HttpServerPort;
        _obsOverlayEnabledCheckBox.Checked = settings.ObsOverlayEnabled;

        UpdateServerStatus();
        UpdateObsUrl();
    }

    public void SaveSettings(TwitchSettings settings)
    {
        settings.HttpServerEnabled = _httpServerEnabledCheckBox.Checked;
        settings.HttpServerPort = (int)_httpServerPortNumeric.Value;
        settings.ObsOverlayEnabled = _obsOverlayEnabledCheckBox.Checked;
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        UpdateObsUrl();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnHttpServerEnabledChanged(object? sender, EventArgs e)
    {
        UpdateControlsState();
        OnSettingChanged(sender, e);
    }

    private void OnPortResetButtonClicked(object sender, EventArgs e)
    {
        _httpServerPortNumeric.Value = DefaultSettings.HttpServerPort;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCopyUrlButtonClicked(object sender, EventArgs e)
    {
        try
        {
            var url = $"http://localhost:{_httpServerPortNumeric.Value}/chat";
            Clipboard.SetText(url);

            MessageBox.Show("URL скопирован в буфер обмена!", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка копирования URL: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnRestartServerButtonClicked(object sender, EventArgs e)
    {
        // TODO: Реализовать перезапуск сервера
        MessageBox.Show("Функция перезапуска сервера будет реализована позже.", "Информация",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetPlaceholders()
    {
        _httpServerPortNumeric.Minimum = 1024;
        _httpServerPortNumeric.Maximum = 65535;
    }

    private void UpdateControlsState()
    {
        var enabled = _httpServerEnabledCheckBox.Checked;

        _httpServerPortNumeric.Enabled = enabled;
        _portResetButton.Enabled = enabled;
        _obsOverlayEnabledCheckBox.Enabled = enabled;
        _copyUrlButton.Enabled = enabled;
        _restartServerButton.Enabled = enabled;
    }

    private void UpdateServerStatus()
    {
        // TODO: Получать реальный статус сервера
        if (_httpServerEnabledCheckBox.Checked)
        {
            _serverStatusLabel.Text = "Статус сервера: ● Готов к запуску";
            _serverStatusLabel.ForeColor = Color.Orange;
        }
        else
        {
            _serverStatusLabel.Text = "Статус сервера: ○ Отключен";
            _serverStatusLabel.ForeColor = Color.Gray;
        }
    }

    private void UpdateObsUrl()
    {
        var url = $"http://localhost:{_httpServerPortNumeric.Value}/chat";
        _obsUrlLabel.Text = $"URL для OBS: {url}";
    }
}
