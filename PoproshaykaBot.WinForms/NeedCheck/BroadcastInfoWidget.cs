using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

public sealed partial class BroadcastInfoWidget : UserControl
{
    private IChannelProvider? _channelProvider;
    private SettingsManager? _settingsManager;
    private StreamStatusManager? _streamStatusManager;
    private BroadcastScheduler? _broadcastScheduler;

    public BroadcastInfoWidget()
    {
        InitializeComponent();
    }

    public void Setup(
        SettingsManager settingsManager,
        StreamStatusManager streamStatusManager,
        BroadcastScheduler broadcastScheduler,
        IChannelProvider channelProvider)
    {
        _settingsManager = settingsManager;
        _streamStatusManager = streamStatusManager;
        _broadcastScheduler = broadcastScheduler;
        _channelProvider = channelProvider;
        UpdateState();
    }

    public void UpdateState()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateState);
            return;
        }

        if (_channelProvider == null || _broadcastScheduler == null || _streamStatusManager == null)
        {
            _statusLabel.Text = "Бот не подключен";
            _statusLabel.ForeColor = Color.Gray;
            _sentCountLabel.Text = "Отправлено: 0";
            _nextTimeLabel.Text = "Следующая: —";
            _toggleButton.Enabled = false;
            _sendNowButton.Enabled = false;

            if (_settingsManager is not null)
            {
                var initialIsAuto = _settingsManager.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled;
                _modeLabel.Text = $"Режим: {(initialIsAuto ? "Авто" : "Ручной")}";
                _modeToggleButton.Text = initialIsAuto ? "В ручной" : "В авто";
                _modeToggleButton.Enabled = true;
            }
            else
            {
                _modeToggleButton.Enabled = false;
            }

            return;
        }

        var isActive = _broadcastScheduler.IsActive;
        var isAuto = _settingsManager?.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled ?? false;
        var streamOnline = _streamStatusManager.CurrentStatus == StreamStatus.Online;

        _statusLabel.Text = isActive ? "✅ Активна" : "❌ Неактивна";
        _statusLabel.ForeColor = isActive ? Color.Green : Color.Red;

        if (isAuto && !isActive)
        {
            _statusLabel.Text = streamOnline ? "❌ Неактивна (Авто)" : "⏳ Ожидание стрима";
            _statusLabel.ForeColor = Color.Orange;
        }

        _modeLabel.Text = $"Режим: {(isAuto ? "Авто" : "Ручной")}";
        _modeToggleButton.Text = isAuto ? "В ручной" : "В авто";

        _sentCountLabel.Text = $"Отправлено: {_broadcastScheduler.SentMessagesCount}";

        var nextTime = _broadcastScheduler.NextBroadcastTime;
        _nextTimeLabel.Text = nextTime.HasValue
            ? $"Следующая: {nextTime.Value:HH:mm:ss}"
            : "Следующая: —";

        _toggleButton.Enabled = true;
        _modeToggleButton.Enabled = true;
        _toggleButton.Text = isActive ? "Стоп" : "Старт";
        _sendNowButton.Enabled = isActive;

        if (isAuto && !isActive)
        {
            _toggleButton.Enabled = false;
            _toolTip.SetToolTip(_toggleButton, """
                                               В автоматическом режиме рассылка запускается сама при начале стрима.
                                               Переключитесь в ручной режим для ручного запуска.
                                               """);
        }
        else
        {
            _toolTip.SetToolTip(_toggleButton, isActive ? "Остановить рассылку" : "Запустить рассылку");
        }

        _toolTip.SetToolTip(_modeToggleButton,
            isAuto
                ? "Перейти в ручное управление рассылкой"
                : "Включить автоматический запуск рассылки при начале стрима");

        _toolTip.SetToolTip(_sendNowButton,
            isActive
                ? "Отправить сообщение из рассылки прямо сейчас"
                : "Для отправки рассылка должна быть активна");
    }

    private void OnModeToggleClick(object sender, EventArgs e)
    {
        if (_settingsManager == null || _broadcastScheduler == null || _streamStatusManager == null)
        {
            return;
        }

        var settings = _settingsManager.Current;
        var willEnableAuto = !settings.Twitch.AutoBroadcast.AutoBroadcastEnabled;

        if (willEnableAuto && _broadcastScheduler.IsActive && _streamStatusManager.CurrentStatus != StreamStatus.Online)
        {
            var result = MessageBox.Show("""
                                         При переключении в автоматический режим активная рассылка будет остановлена, так как стрим сейчас оффлайн.

                                         Продолжить?
                                         """,
                "Переключение режима",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        settings.Twitch.AutoBroadcast.AutoBroadcastEnabled = willEnableAuto;
        _settingsManager.SaveSettings(settings);

        if (willEnableAuto)
        {
            if (_streamStatusManager.CurrentStatus == StreamStatus.Online)
            {
                if (!_broadcastScheduler.IsActive && !string.IsNullOrEmpty(_channelProvider?.Channel))
                {
                    _broadcastScheduler.Start(_channelProvider.Channel);
                }
            }
            else
            {
                _broadcastScheduler.Stop();
            }
        }

        UpdateState();
    }

    private void OnToggleButtonClick(object sender, EventArgs e)
    {
        if (_channelProvider == null || _broadcastScheduler == null || _settingsManager == null)
        {
            return;
        }

        if (_broadcastScheduler.IsActive)
        {
            _broadcastScheduler.Stop();
        }
        else
        {
            if (_settingsManager.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled)
            {
                MessageBox.Show("""
                                В автоматическом режиме рассылка запускается сама при начале стрима.

                                Для ручного запуска переключитесь в ручной режим.
                                """,
                    "Автоматический режим",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            if (!string.IsNullOrEmpty(_channelProvider.Channel))
            {
                _broadcastScheduler.Start(_channelProvider.Channel);
            }
        }

        UpdateState();
    }

    private async void OnSendNowButtonClick(object sender, EventArgs e)
    {
        try
        {
            if (_broadcastScheduler == null)
            {
                return;
            }

            _sendNowButton.Enabled = false;
            await _broadcastScheduler.ManualSendAsync();
            _sendNowButton.Enabled = true;
        }
        catch (Exception ex)
        {
            throw; // TODO handle exception
        }
    }
}
