using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;
using System.Diagnostics;

namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class StreamInfoWidget : UserControl, IDashboardTileHeaderProvider
{
    private readonly List<IDisposable> _subs = [];
    private string? _lastThumbnailUrl;
    private bool _initialized;
    private bool _refreshing;
    private ToolStripButton? _openChannelButton;
    private ToolStripButton? _refreshButton;

    public StreamInfoWidget()
    {
        InitializeComponent();
    }

    [Inject]
    public IStreamStatus Stream { get; internal init; } = null!;

    [Inject]
    public IEventBus Bus { get; internal init; } = null!;

    [Inject]
    public ILogger<StreamInfoWidget> Logger { get; internal init; } = null!;

    public IReadOnlyList<ToolStripItem> CreateHeaderItems()
    {
        _refreshButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🔄 Обновить",
            ToolTipText = "Принудительно обновить данные стрима",
        };

        _refreshButton.Click += OnRefreshClick;

        _openChannelButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🔗 Открыть Twitch",
            ToolTipText = "Открыть страницу канала на Twitch",
            Visible = false,
        };

        _openChannelButton.Click += OnOpenChannelClick;

        return [_refreshButton, _openChannelButton];
    }

    public void UpdateStatus(StreamStatus status, StreamInfo? info)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateStatus(status, info));
            return;
        }

        _lastUpdateLabel.Text = $"🕐 {DateTime.Now:HH:mm:ss}";

        switch (status)
        {
            case StreamStatus.Online:
                ApplyOnlineStatus(info);
                break;

            case StreamStatus.Offline:
                _statusIconLabel.Text = "⚫";
                _statusTextLabel.Text = "ОФЛАЙН";
                _statusTextLabel.ForeColor = Color.Gray;
                ClearInfoLabels("Стрим завершен");
                break;

            default:
                _statusIconLabel.Text = "⚪";
                _statusTextLabel.Text = "НЕИЗВЕСТНО";
                _statusTextLabel.ForeColor = Color.DarkGray;
                ClearInfoLabels("Статус не определен");
                break;
        }

        UpdateRefreshTimer(status);
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

        _subs.Add(Bus.SubscribeOnUi<StreamWentOnline>(this, _ => UpdateCurrentStatus()));
        _subs.Add(Bus.SubscribeOnUi<StreamWentOffline>(this, _ => UpdateCurrentStatus()));
        _subs.Add(Bus.SubscribeOnUi<StreamMetadataResolved>(this, _ => UpdateCurrentStatus()));
        _subs.Add(Bus.SubscribeOnUi<ChannelUpdated>(this, _ => UpdateCurrentStatus()));
        _subs.DisposeOnClose(this);

        UpdateCurrentStatus();
    }

    private async void OnStreamInfoTimerTick(object? sender, EventArgs e)
    {
        if (Stream.CurrentStatus != StreamStatus.Online)
        {
            return;
        }

        try
        {
            await Stream.RefreshLiveSnapshotAsync();
            UpdateCurrentStatus();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Ошибка обновления информации о стриме");
        }
    }

    private async void OnRefreshClick(object? sender, EventArgs e)
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        _refreshButton?.Enabled = false;

        try
        {
            await Stream.RefreshLiveSnapshotAsync();
            UpdateCurrentStatus();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Ошибка обновления информации о стриме");
        }
        finally
        {
            _refreshing = false;

            if (_refreshButton != null && !IsDisposed)
            {
                _refreshButton.Enabled = true;
            }
        }
    }

    private void OnOpenChannelClick(object? sender, EventArgs e)
    {
        if (sender is not ToolStripButton { Tag: string login } || string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var url = $"https://twitch.tv/{login}";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"""
                             Не удалось открыть браузер.
                             Ссылка: {url}

                             Ошибка: {ex.Message}
                             """,
                "Ошибка навигации", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyOnlineStatus(StreamInfo? info)
    {
        _statusIconLabel.Text = "🔴";
        _statusTextLabel.Text = "В ЭФИРЕ";
        _statusTextLabel.ForeColor = Color.Red;

        if (info == null || info.IsBare)
        {
            ClearInfoLabels("Загрузка данных...");
            return;
        }

        _titleLabel.Text = info.Title;
        _gameLabel.Text = string.IsNullOrWhiteSpace(info.GameName) ? "–" : $"🎮 {info.GameName}";
        _viewersLabel.Text = $"👥 {info.ViewerCount:N0}";

        var duration = DateTime.UtcNow - info.StartedAt;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        _uptimeLabel.Text = $"⏱️ {(int)duration.TotalHours}ч {duration.Minutes:00}м";

        LoadThumbnail(info.ThumbnailUrl);

        if (_openChannelButton != null)
        {
            _openChannelButton.Visible = true;
            _openChannelButton.Tag = info.UserLogin;
        }
    }

    private void UpdateCurrentStatus()
    {
        UpdateStatus(Stream.CurrentStatus, Stream.CurrentStream);
    }

    private void UpdateRefreshTimer(StreamStatus status)
    {
        if (status == StreamStatus.Online)
        {
            if (!_streamInfoTimer.Enabled)
            {
                _streamInfoTimer.Start();
            }
        }
        else
        {
            if (_streamInfoTimer.Enabled)
            {
                _streamInfoTimer.Stop();
            }
        }
    }

    private void ClearInfoLabels(string titleMessage)
    {
        _titleLabel.Text = titleMessage;
        _gameLabel.Text = "–";
        _viewersLabel.Text = "👥 –";
        _uptimeLabel.Text = "⏱️ –";

        ClearThumbnail();

        if (_openChannelButton == null)
        {
            return;
        }

        _openChannelButton.Visible = false;
        _openChannelButton.Tag = null;
    }

    private void LoadThumbnail(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            ClearThumbnail();
            return;
        }

        if (url == _lastThumbnailUrl)
        {
            return;
        }

        try
        {
            var minWidth = LogicalToDeviceUnits(240);
            var width = Math.Max(_thumbnailPictureBox.Width, minWidth);
            var height = (int)Math.Round(width * 9.0 / 16.0);

            var resolvedUrl = url
                .Replace("{width}", width.ToString())
                .Replace("{height}", height.ToString());

            ClearThumbnail();

            _thumbnailPictureBox.LoadAsync(resolvedUrl);
            _lastThumbnailUrl = url;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка подготовки превью: {ex.Message}");
            ClearThumbnail();
        }
    }

    private void ClearThumbnail()
    {
        if (_thumbnailPictureBox.Image != null)
        {
            _thumbnailPictureBox.Image.Dispose();
            _thumbnailPictureBox.Image = null;
        }

        _lastThumbnailUrl = null;
    }
}
