using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings.Debugging;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public partial class DebugChannelSettingsControl : UserControl
{
    private DebugChannelOverride _commandLine = DebugChannelOverride.None;

    public DebugChannelSettingsControl()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(DebugChannelSettings settings, DebugChannelOverride commandLine)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(commandLine);

        _commandLine = commandLine;

        _enabledCheckBox.Checked = settings.IsEnabled;
        _channelTextBox.Text = settings.Channel;
        _allowSendingCheckBox.Checked = settings.AllowSending;

        RefreshState();
    }

    public void SaveSettings(DebugChannelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.IsEnabled = _enabledCheckBox.Checked;
        settings.Channel = ChannelLogin.TryNormalize(_channelTextBox.Text, out var login)
            ? login
            : _channelTextBox.Text.Trim();

        settings.AllowSending = _allowSendingCheckBox.Checked;
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        RefreshState();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshState()
    {
        var enabled = _enabledCheckBox.Checked;

        _channelTextBox.Enabled = enabled;
        _channelLabel.Enabled = enabled;
        _allowSendingCheckBox.Enabled = enabled;

        if (_commandLine.Channel is { Length: > 0 } fromCommandLine)
        {
            _hintLabel.ForeColor = SystemColors.GrayText;
            _hintLabel.Text = $"Сейчас действует канал из командной строки: {fromCommandLine}. Настройки этой вкладки применятся при следующем запуске без ключа {DebugChannelOverride.ChannelArgument}.";
            return;
        }

        if (!enabled)
        {
            _hintLabel.ForeColor = SystemColors.GrayText;
            _hintLabel.Text = "Бот работает на своём канале из вкладки «Основные».";
            return;
        }

        if (!ChannelLogin.TryNormalize(_channelTextBox.Text, out var login))
        {
            _hintLabel.ForeColor = Color.Red;
            _hintLabel.Text = "Укажите имя канала Twitch – например, mrbeast или ссылку на канал.";
            return;
        }

        if (_allowSendingCheckBox.Checked)
        {
            _hintLabel.ForeColor = Color.OrangeRed;
            _hintLabel.Text = $"Бот будет писать в чат канала {login} по-настоящему: приветствия, ответы на команды и рассылка.";
            return;
        }

        _hintLabel.ForeColor = Color.Green;
        _hintLabel.Text = $"Бот будет только читать чат канала {login}. Сообщения не отправляются, они видны в журнале.";
    }
}
