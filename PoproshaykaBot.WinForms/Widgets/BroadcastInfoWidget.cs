using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;

namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class BroadcastInfoWidget : UserControl, IDashboardTileHeaderProvider
{
    private readonly List<IDisposable> _subs = [];
    private bool _initialized;
    private ToolStripButton? _toggleButton;
    private ToolStripButton? _modeToggleButton;
    private ToolStripButton? _sendNowButton;

    public BroadcastInfoWidget()
    {
        InitializeComponent();
    }

    [Inject]
    public SettingsManager Settings { get; internal init; } = null!;

    [Inject]
    public IStreamStatus Stream { get; internal init; } = null!;

    [Inject]
    public BroadcastScheduler Scheduler { get; internal init; } = null!;

    [Inject]
    public IChannelProvider ChannelProvider { get; internal init; } = null!;

    [Inject]
    public IEventBus Bus { get; internal init; } = null!;

    public IReadOnlyList<ToolStripItem> CreateHeaderItems()
    {
        _toggleButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "Старт",
        };

        _toggleButton.Click += OnToggleButtonClick;

        _modeToggleButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "В авто",
        };

        _modeToggleButton.Click += OnModeToggleClick;

        _sendNowButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "Сейчас",
        };

        _sendNowButton.Click += OnSendNowButtonClick;

        return [_toggleButton, _modeToggleButton, _sendNowButton];
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

        _subs.Add(Bus.SubscribeOnUi<BroadcastSchedulerStateChanged>(this, _ => UpdateState()));
        _subs.Add(Bus.SubscribeOnUi<StreamWentOnline>(this, _ => UpdateState()));
        _subs.Add(Bus.SubscribeOnUi<StreamWentOffline>(this, _ => UpdateState()));
        _subs.Add(Bus.SubscribeOnUi<StreamMetadataResolved>(this, _ => UpdateState()));
        _subs.Add(Bus.SubscribeOnUi<BotLifecyclePhaseChanged>(this, _ => UpdateState()));
        _subs.DisposeOnClose(this);

        UpdateState();
    }

    private void OnModeToggleClick(object? sender, EventArgs e)
    {
        var willEnableAuto = !Settings.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled;

        if (willEnableAuto && Scheduler.IsActive && Stream.CurrentStatus != StreamStatus.Online)
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

        Settings.Mutate(settings => settings.Twitch.AutoBroadcast.AutoBroadcastEnabled = willEnableAuto);

        if (willEnableAuto)
        {
            if (Stream.CurrentStatus == StreamStatus.Online)
            {
                if (!Scheduler.IsActive && !string.IsNullOrEmpty(ChannelProvider.Channel))
                {
                    Scheduler.Start(ChannelProvider.Channel);
                }
            }
            else
            {
                Scheduler.Stop();
            }
        }

        UpdateState();
    }

    private void OnToggleButtonClick(object? sender, EventArgs e)
    {
        if (Scheduler.IsActive)
        {
            Scheduler.Stop();
        }
        else
        {
            if (Settings.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled)
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

            if (!string.IsNullOrEmpty(ChannelProvider.Channel))
            {
                Scheduler.Start(ChannelProvider.Channel);
            }
        }

        UpdateState();
    }

    private async void OnSendNowButtonClick(object? sender, EventArgs e)
    {
        if (_sendNowButton == null)
        {
            return;
        }

        _sendNowButton.Enabled = false;
        await Scheduler.ManualSendAsync();
        _sendNowButton.Enabled = true;
    }

    private static string ResolveToggleButtonTooltip(bool isActive, bool isAuto)
    {
        if (isAuto && !isActive)
        {
            return "В автоматическом режиме рассылка запускается сама при начале стрима. Переключитесь в ручной режим для ручного запуска.";
        }

        return isActive ? "Остановить рассылку" : "Запустить рассылку";
    }

    private void UpdateState()
    {
        var isActive = Scheduler.IsActive;
        var isAuto = Settings.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled;
        var streamOnline = Stream.CurrentStatus == StreamStatus.Online;

        UpdateStatusLabel(isActive, isAuto, streamOnline);

        _modeLabel.Text = $"Режим: {(isAuto ? "Авто" : "Ручной")}";

        UpdateModeToggleButton(isAuto);

        _sentCountLabel.Text = $"Отправлено: {Scheduler.SentMessagesCount}";

        var nextTime = Scheduler.NextBroadcastTime;
        _nextTimeLabel.Text = nextTime.HasValue
            ? $"Следующая: {nextTime.Value:HH:mm:ss}"
            : "Следующая: –";

        UpdateToggleButton(isActive, isAuto);
        UpdateSendNowButton(isActive);
    }

    private void UpdateStatusLabel(bool isActive, bool isAuto, bool streamOnline)
    {
        _statusLabel.Text = isActive ? "✅ Активна" : "❌ Неактивна";
        _statusLabel.ForeColor = isActive ? Color.Green : Color.Red;

        if (isAuto && !isActive)
        {
            _statusLabel.Text = streamOnline ? "❌ Неактивна (Авто)" : "⏳ Ожидание стрима";
            _statusLabel.ForeColor = Color.Orange;
        }
    }

    private void UpdateModeToggleButton(bool isAuto)
    {
        if (_modeToggleButton == null)
        {
            return;
        }

        _modeToggleButton.Text = isAuto ? "В ручной" : "В авто";
        _modeToggleButton.Enabled = true;
        _modeToggleButton.ToolTipText = isAuto
            ? "Перейти в ручное управление рассылкой"
            : "Включить автоматический запуск рассылки при начале стрима";
    }

    private void UpdateToggleButton(bool isActive, bool isAuto)
    {
        if (_toggleButton == null)
        {
            return;
        }

        _toggleButton.Text = isActive ? "Стоп" : "Старт";
        _toggleButton.Enabled = !(isAuto && !isActive);
        _toggleButton.ToolTipText = ResolveToggleButtonTooltip(isActive, isAuto);
    }

    private void UpdateSendNowButton(bool isActive)
    {
        if (_sendNowButton == null)
        {
            return;
        }

        _sendNowButton.Enabled = isActive;
        _sendNowButton.ToolTipText = isActive
            ? "Отправить сообщение из рассылки прямо сейчас"
            : "Для отправки рассылка должна быть активна";
    }
}
