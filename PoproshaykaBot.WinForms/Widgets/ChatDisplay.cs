using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using PoproshaykaBot.Core.Chat.Display;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.WinForms.Infrastructure;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;
using System.Diagnostics;

namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class ChatDisplay : UserControl, IDashboardTileHeaderProvider
{
    private const string WebView2RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private bool _initialized;
    private bool _reloadAttempted;
    private ToolStripButton? _reloadButton;
    private ToolStripButton? _resetZoomButton;
    private ToolStripButton? _openInBrowserButton;
    private ToolStripButton? _resetSessionButton;
    private ToolStripButton? _editBlockersButton;
    private string? _clutterScriptId;

    public ChatDisplay()
    {
        InitializeComponent();
    }

    [Inject]
    public SettingsManager Settings { get; internal init; } = null!;

    [Inject]
    public ITargetChannelProvider TargetChannel { get; internal init; } = null!;

    [Inject]
    public ILogger<ChatDisplay> Logger { get; internal init; } = null!;

    [Inject]
    public ChatDisplayStore Store { get; internal init; } = null!;

    public IReadOnlyList<ToolStripItem> CreateHeaderItems()
    {
        _reloadButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "⟳",
            ToolTipText = "Перезагрузить страницу чата",
        };

        _reloadButton.Click += OnReloadClicked;

        _resetZoomButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🔍",
            ToolTipText = "Сбросить масштаб к значению по умолчанию",
        };

        _resetZoomButton.Click += OnResetZoomClicked;

        _openInBrowserButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🌐",
            ToolTipText = "Открыть текущую страницу в системном браузере",
        };

        _openInBrowserButton.Click += OnOpenInBrowserClicked;

        _resetSessionButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🗑",
            ToolTipText = "Очистить куки и кэш активного аккаунта (бот или стример – настраивается в первичной настройке)",
        };

        _resetSessionButton.Click += OnResetSessionClicked;

        _editBlockersButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "🚫",
            ToolTipText = "Блокировка баннеров – скрыть лишние элементы чата",
        };

        _editBlockersButton.Click += OnEditBlockersClicked;

        return [_reloadButton, _resetZoomButton, _openInBrowserButton, _resetSessionButton, _editBlockersButton];
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

        _ = InitializeWebViewAsync();
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Logger.LogError("WebView2 процесс упал: {Reason}", e.ProcessFailedKind);

        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                if (_reloadAttempted)
                {
                    return;
                }

                _reloadAttempted = true;
                _webView.Reload();
            });
        }
        catch (ObjectDisposedException)
        {
            // control disposed before reload could be scheduled
        }
        catch (InvalidOperationException) when (IsDisposed)
        {
            // handle destroyed
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _reloadAttempted = false;
            return;
        }

        Logger.LogWarning("Twitch-чат не загрузился: status={HttpStatusCode}, error={WebErrorStatus}", e.HttpStatusCode, e.WebErrorStatus);
    }

    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
        var zoom = _webView.ZoomFactor;

        Store.SaveZoom(zoom);
    }

    private void OnReloadClicked(object? sender, EventArgs e)
    {
        if (_webView.CoreWebView2 == null)
        {
            return;
        }

        _webView.Reload();
    }

    private void OnResetZoomClicked(object? sender, EventArgs e)
    {
        _webView.ZoomFactor = ChatDisplayStore.DefaultZoom;
    }

    private void OnOpenInBrowserClicked(object? sender, EventArgs e)
    {
        var url = _webView.Source?.ToString();

        if (string.IsNullOrEmpty(url))
        {
            var channel = TargetChannel.Current.Login.Trim();

            if (string.IsNullOrEmpty(channel))
            {
                return;
            }

            url = $"https://www.twitch.tv/popout/{Uri.EscapeDataString(channel)}/chat?popout=";
        }

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
            Logger.LogError(ex, "Не удалось открыть ссылку в браузере");
        }
    }

    private async void OnResetSessionClicked(object? sender, EventArgs e)
    {
        if (_webView.CoreWebView2?.Profile == null)
        {
            return;
        }

        var account = Settings.Current.Twitch.ChatDisplayAccount == TwitchOAuthRole.Broadcaster ? "стримера" : "бота";
        var result = MessageBox.Show(this,
            $"Это разлогинит {account} из Twitch-чата и очистит куки профиля. Продолжить?",
            "Сброс сессии Twitch",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite);
            _webView.Reload();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Не удалось очистить данные сессии WebView2");
        }
    }

    private void OnFallbackLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WebView2RuntimeDownloadUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Не удалось открыть ссылку на WebView2 Runtime");
        }
    }

    private async void OnEditBlockersClicked(object? sender, EventArgs e)
    {
        if (!TryEditBlockers(Store.LoadBlockersText(), out var updated))
        {
            return;
        }

        if (!Store.TrySaveBlockersText(updated))
        {
            MessageBox.Show(this, "Не удалось сохранить список блокираторов. Подробности – в логах.",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            return;
        }

        await RegisterClutterScriptAsync();
        _webView.CoreWebView2?.Reload();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = WebView2UserDataFolders.Resolve(Settings.Current.Twitch.ChatDisplayAccount);

            var env = await WebView2EnvironmentFactory.CreateAsync(userDataFolder, Logger);
            await _webView.EnsureCoreWebView2Async(env);

            if (IsDisposed || Disposing)
            {
                return;
            }

            _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
            _webView.NavigationCompleted += OnNavigationCompleted;
            _webView.ZoomFactorChanged += OnZoomFactorChanged;

            await RegisterClutterScriptAsync();

            _webView.ZoomFactor = Store.LoadZoom();

            NavigateToChannel();
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            Logger.LogError(ex, "WebView2 Runtime не установлен");
            ShowRuntimeMissingFallback();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Не удалось инициализировать WebView2 для Twitch-чата");
            ShowGenericFallback();
        }
    }

    private void NavigateToChannel()
    {
        if (_webView.CoreWebView2 == null)
        {
            return;
        }

        var channel = TargetChannel.Current.Login.Trim();

        if (string.IsNullOrEmpty(channel))
        {
            _webView.Visible = false;
            _fallbackPanel.Visible = true;
            _fallbackLabel.Text = "Укажите канал в настройках Twitch, чтобы открыть чат.";
            _fallbackLink.Visible = false;
            return;
        }

        _webView.Visible = true;
        _fallbackPanel.Visible = false;

        var url = $"https://www.twitch.tv/popout/{Uri.EscapeDataString(channel)}/chat?popout=";
        _webView.CoreWebView2.Navigate(url);
    }

    private void ShowRuntimeMissingFallback()
    {
        _webView.Visible = false;
        SetHeaderItemsEnabled(false);
        _fallbackPanel.Visible = true;
        _fallbackLabel.Text = "Требуется Microsoft Edge WebView2 Runtime.\nУстановите его, чтобы увидеть чат Twitch.";
        _fallbackLink.Text = "Скачать WebView2 Runtime";
        _fallbackLink.Visible = true;
    }

    private void ShowGenericFallback()
    {
        _webView.Visible = false;
        SetHeaderItemsEnabled(false);
        _fallbackPanel.Visible = true;
        _fallbackLabel.Text = "Не удалось открыть чат Twitch. Подробности – в логах.";
        _fallbackLink.Visible = false;
    }

    private void SetHeaderItemsEnabled(bool enabled)
    {
        _reloadButton?.Enabled = enabled;
        _resetZoomButton?.Enabled = enabled;
        _openInBrowserButton?.Enabled = enabled;
        _resetSessionButton?.Enabled = enabled;
        _editBlockersButton?.Enabled = enabled;
    }

    private async Task RegisterClutterScriptAsync()
    {
        var core = _webView.CoreWebView2;

        if (core == null)
        {
            return;
        }

        try
        {
            if (_clutterScriptId != null)
            {
                core.RemoveScriptToExecuteOnDocumentCreated(_clutterScriptId);
            }

            _clutterScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(Store.BuildHideClutterScript());
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Не удалось применить блокираторы баннеров чата");
        }
    }

    private bool TryEditBlockers(string current, out string result)
    {
        result = current;

        using var dialog = new Form
        {
            Text = "Блокировка баннеров чата",
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = LogicalToDeviceUnits(new Size(460, 320)),
            MinimumSize = LogicalToDeviceUnits(new Size(360, 240)),
        };

        OffScreenForm.PrepareIfHeadless(dialog);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new(LogicalToDeviceUnits(10)),
        };

        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100F));
        layout.RowStyles.Add(new(SizeType.AutoSize));

        var hint = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "По одному CSS-селектору в строке. Строки, начинающиеся с #, – комментарии.\nВстроенные блокираторы работают всегда.",
        };

        var editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Text = current,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };

        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(editor, 0, 1);
        layout.Controls.Add(buttons, 0, 2);

        dialog.Controls.Add(layout);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        result = editor.Text;
        return true;
    }
}
