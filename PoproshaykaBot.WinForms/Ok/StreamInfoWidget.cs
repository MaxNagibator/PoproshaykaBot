using PoproshaykaBot.WinForms.Models;
using System.Diagnostics;

namespace PoproshaykaBot.WinForms;

public sealed partial class StreamInfoWidget : UserControl
{
    private string? _lastThumbnailUrl;

    public StreamInfoWidget()
    {
        InitializeComponent();

        _openChannelButton.Click += OnOpenChannelClick;
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

        _lastUpdateLabel.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";

        switch (status)
        {
            case StreamStatus.Online:
                _statusIconLabel.Text = "🔴";
                _statusTextLabel.Text = "В ЭФИРЕ";
                _statusTextLabel.ForeColor = Color.Red;

                if (info != null)
                {
                    _titleLabel.Text = info.Title;
                    _gameLabel.Text = info.GameName;
                    _viewersLabel.Text = $"👥 {info.ViewerCount:N0}";

                    var duration = DateTime.UtcNow - info.StartedAt;
                    if (duration < TimeSpan.Zero)
                    {
                        duration = TimeSpan.Zero;
                    }

                    _uptimeLabel.Text = $"⏱️ {(int)duration.TotalHours}ч {duration.Minutes:00}м";

                    LoadThumbnail(info.ThumbnailUrl);

                    _openChannelButton.Visible = true;
                    _openChannelButton.Tag = info.UserLogin;
                }
                else
                {
                    ClearInfoLabels("Загрузка данных...");
                }

                break;

            case StreamStatus.Offline:
                _statusIconLabel.Text = "⚫";
                _statusTextLabel.Text = "ОФЛАЙН";
                _statusTextLabel.ForeColor = Color.Gray;
                ClearInfoLabels("Стрим завершен");
                break;

            case StreamStatus.Unknown:
            default:
                _statusIconLabel.Text = "⚪";
                _statusTextLabel.Text = "НЕИЗВЕСТНО";
                _statusTextLabel.ForeColor = Color.DarkGray;
                ClearInfoLabels("Статус не определен");
                break;
        }
    }

    private void OnOpenChannelClick(object? sender, EventArgs e)
    {
        if (sender is not Button { Tag: string login } || string.IsNullOrWhiteSpace(login))
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
            MessageBox.Show($"Не удалось открыть браузер.\nСсылка: {url}\n\nОшибка: {ex.Message}",
                "Ошибка навигации", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearInfoLabels(string titleMessage)
    {
        _titleLabel.Text = titleMessage;
        _gameLabel.Text = "—";
        _viewersLabel.Text = "👥 —";
        _uptimeLabel.Text = "⏱️ —";

        ClearThumbnail();

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
            var resolvedUrl = url
                .Replace("{width}", _thumbnailPictureBox.Width.ToString())
                .Replace("{height}", _thumbnailPictureBox.Height.ToString());

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
