namespace PoproshaykaBot.WinForms.Settings;

public partial class HttpServerSettingsControl : UserControl
{
    private static readonly TwitchSettings DefaultSettings = new();
    private readonly KestrelHttpServer _server;

    public HttpServerSettingsControl(KestrelHttpServer server)
    {
        InitializeComponent();
        SetPlaceholders();

        _server = server;
        UpdateServerStatus();
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
        UpdateServerStatus();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnHttpServerEnabledChanged(object? sender, EventArgs e)
    {
        UpdateControlsState();
        UpdateServerStatus();
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

    private async void OnStartServerButtonClicked(object sender, EventArgs e)
    {
        try
        {
            if (_httpServerEnabledCheckBox.Checked == false)
            {
                MessageBox.Show("HTTP сервер отключен настройкой. Включите сервер, чтобы запустить.", "Информация",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_server.IsRunning == false)
            {
                await _server.StartAsync();
                UpdateServerStatus();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска сервера: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnStopServerButtonClicked(object sender, EventArgs e)
    {
        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
                UpdateServerStatus();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка остановки сервера: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnRestartServerButtonClicked(object sender, EventArgs e)
    {
        if (_httpServerEnabledCheckBox.Checked == false)
        {
            MessageBox.Show("HTTP сервер отключен настройкой. Включите сервер, чтобы выполнить перезапуск.", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
            }

            await _server.StartAsync();
            UpdateServerStatus();
            MessageBox.Show("HTTP сервер перезапущен.", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка перезапуска сервера: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
        _startServerButton.Enabled = enabled && _server.IsRunning == false;
        _stopServerButton.Enabled = enabled && _server.IsRunning;
        _restartServerButton.Enabled = enabled;
    }

    private void UpdateServerStatus()
    {
        var enabled = _httpServerEnabledCheckBox.Checked;

        if (enabled == false)
        {
            _serverStatusLabel.Text = "Статус сервера: ○ Отключен";
            _serverStatusLabel.ForeColor = Color.Gray;
            _startServerButton.Enabled = false;
            _stopServerButton.Enabled = false;
            _restartServerButton.Enabled = false;
            return;
        }

        if (_server.IsRunning)
        {
            _serverStatusLabel.Text = "Статус сервера: ● Запущен";
            _serverStatusLabel.ForeColor = Color.Green;
            _startServerButton.Enabled = false;
            _stopServerButton.Enabled = true;
        }
        else
        {
            _serverStatusLabel.Text = "Статус сервера: ● Готов к запуску";
            _serverStatusLabel.ForeColor = Color.Orange;
            _startServerButton.Enabled = true;
            _stopServerButton.Enabled = false;
        }

        _restartServerButton.Enabled = true;
    }

    private void UpdateObsUrl()
    {
        var url = $"http://localhost:{_httpServerPortNumeric.Value}/chat";
        _obsUrlLabel.Text = $"URL для OBS: {url}";
    }
}
