using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using System.Collections.ObjectModel;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class ObsIntegrationSectionViewModel : ObservableObject, IDisposable
{
    private const int PortMin = 1;
    private const int PortMax = 65535;
    private const int DelayMin = 0;
    private const int DelayMax = 5000;
    private const int WidthMin = 160;
    private const int WidthMax = 7680;
    private const int HeightMin = 90;
    private const int HeightMax = 4320;

    private readonly ObsIntegrationService _obsIntegration;
    private readonly SettingsManager _settingsManager;
    private readonly ILogger<ObsIntegrationSectionViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboard;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProvisionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshChatNowCommand))]
    private bool _enabled;

    [ObservableProperty]
    private bool _autoConnect;

    [ObservableProperty]
    private bool _autoProvisionBrowserSource;

    [ObservableProperty]
    private bool _refreshChatSourcesOnStreamStart;

    [ObservableProperty]
    private bool _applySceneOnProfile;

    [ObservableProperty]
    private bool _applyProfileOnScene;

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = 4455;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _sceneName = string.Empty;

    [ObservableProperty]
    private int _dashboardVolumeMeterDelayMs = 120;

    [ObservableProperty]
    private string _sourceName = "PoproshaykaBot Chat";

    [ObservableProperty]
    private int _width = 1920;

    [ObservableProperty]
    private int _height = 1080;

    [ObservableProperty]
    private string _statusText = "● Не проверено";

    [ObservableProperty]
    private StatusSeverity _statusSeverity = StatusSeverity.None;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadScenesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProvisionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshChatNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyOverlayUrlCommand))]
    private bool _isBusy;

    private bool _autoCheckStarted;

    [ObservableProperty]
    private string _overlayUrl = string.Empty;

    public ObsIntegrationSectionViewModel(
        ObsIntegrationService obsIntegration,
        SettingsManager settingsManager,
        ILogger<ObsIntegrationSectionViewModel> logger,
        IDialogService dialogService,
        IClipboardService clipboard)
    {
        _obsIntegration = obsIntegration;
        _settingsManager = settingsManager;
        _logger = logger;
        _dialogService = dialogService;
        _clipboard = clipboard;
    }

    public bool IsAutoPopulating { get; private set; }

    public ObservableCollection<string> Scenes { get; } = [];

    public ObservableCollection<ObsSourceItemViewModel> DashboardSources { get; } = [];

    public ObservableCollection<ObsSourceItemViewModel> ChatRefreshSources { get; } = [];

    public void Load(ObsIntegrationSettings settings)
    {
        Enabled = settings.Enabled;
        AutoConnect = settings.AutoConnect;
        AutoProvisionBrowserSource = settings.AutoProvisionBrowserSource;
        RefreshChatSourcesOnStreamStart = settings.RefreshChatSourcesOnStreamStart;
        ApplySceneOnProfile = settings.ApplySceneOnProfile;
        ApplyProfileOnScene = settings.ApplyProfileOnScene;
        Host = settings.Host;
        Port = settings.Port;
        Password = settings.Password;
        SceneName = settings.SceneName;
        DashboardVolumeMeterDelayMs = settings.DashboardVolumeMeterDelayMs;
        SourceName = settings.SourceName;
        Width = settings.Width;
        Height = settings.Height;

        PopulateSourceItems(DashboardSources, [], settings.GetDashboardSourceNames());
        PopulateSourceItems(ChatRefreshSources, [], settings.ChatRefreshSources);

        OverlayUrl = BuildOverlayUrl();
        SetStatus("● Не проверено", StatusSeverity.None);
        _autoCheckStarted = false;
    }

    public void RunAutoConnectionCheck()
    {
        if (_autoCheckStarted || App.IsHeadless)
        {
            return;
        }

        _autoCheckStarted = true;
        SetStatus("● Проверка подключения...", StatusSeverity.None);

        _ = RunObsOperationAsync(async ct =>
        {
            var settings = BuildCurrentSettings();
            await ConnectAndLoadScenesAsync(settings, "Подключен", ct);
        });
    }

    public void SaveTo(ObsIntegrationSettings target)
    {
        target.Enabled = Enabled;
        target.AutoConnect = AutoConnect;
        target.AutoProvisionBrowserSource = AutoProvisionBrowserSource;
        target.RefreshChatSourcesOnStreamStart = RefreshChatSourcesOnStreamStart;
        target.ApplySceneOnProfile = ApplySceneOnProfile;
        target.ApplyProfileOnScene = ApplyProfileOnScene;
        target.Host = string.IsNullOrWhiteSpace(Host) ? "127.0.0.1" : Host.Trim();
        target.Port = Port;
        target.Password = Password;
        target.SceneName = SceneName.Trim();
        target.DashboardSourceNames = DashboardSources
            .Where(s => s.IsChecked)
            .Select(s => s.Name)
            .ToList();
        target.DashboardMicrophoneName = string.Empty;
        target.DashboardVolumeMeterDelayMs = DashboardVolumeMeterDelayMs;
        target.SourceName = string.IsNullOrWhiteSpace(SourceName) ? "PoproshaykaBot Chat" : SourceName.Trim();
        target.Width = Width;
        target.Height = Height;
        target.ChatRefreshSources = ChatRefreshSources
            .Where(s => s.IsChecked)
            .Select(s => s.Name)
            .ToList();
    }

    [RelayCommand(CanExecute = nameof(CanOperateAlways))]
    private async Task TestConnectionAsync()
    {
        await RunObsOperationAsync(async ct =>
        {
            var settings = BuildCurrentSettings();
            await ConnectAndLoadScenesAsync(settings, "Подключен", ct);
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperateWhenEnabled))]
    private async Task ReconnectAsync()
    {
        await RunObsOperationAsync(async ct =>
        {
            var settings = BuildCurrentSettings();
            await _obsIntegration.DisconnectAsync(ct);
            await ConnectAndLoadScenesAsync(settings, "Переподключен", ct);
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperateAlways))]
    private async Task LoadScenesAsync()
    {
        await RunObsOperationAsync(async ct =>
        {
            await LoadObsListsAsync(BuildCurrentSettings(), ct);
            SetStatus("● Списки OBS обновлены", StatusSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperateWhenEnabled))]
    private async Task ProvisionAsync()
    {
        await RunObsOperationAsync(async ct =>
        {
            var result = await _obsIntegration.ProvisionBrowserSourceAsync(BuildCurrentSettings(), ct);

            SetStatus(result.Created
                ? $"● Источник создан: {result.SourceName}"
                : $"● Источник обновлён: {result.SourceName}",
                StatusSeverity.Success);

            _dialogService.Info("OBS", $"Browser Source готов.\n\nСцена: {result.SceneName}\nИсточник: {result.SourceName}\nURL: {result.Url}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperateWhenEnabled))]
    private async Task RefreshChatNowAsync()
    {
        await RunObsOperationAsync(async ct =>
        {
            var refreshed = await _obsIntegration.RefreshConfiguredChatSourcesAsync(BuildCurrentSettings(), ct);

            SetStatus(refreshed > 0
                ? $"● Обновлено чат-источников: {refreshed}"
                : "● Нет настроенных чат-источников",
                refreshed > 0 ? StatusSeverity.Success : StatusSeverity.Warning);
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperateAlways))]
    private void CopyOverlayUrl()
    {
        if (_clipboard.TrySetText(OverlayUrl))
        {
            SetStatus("● URL скопирован", StatusSeverity.Success);
            return;
        }

        _dialogService.Warning("OBS", "Не удалось скопировать URL в буфер обмена.");
    }

    [RelayCommand]
    private void IncreasePort()
    {
        if (Port < PortMax)
        {
            Port++;
        }
    }

    [RelayCommand]
    private void DecreasePort()
    {
        if (Port > PortMin)
        {
            Port--;
        }
    }

    [RelayCommand]
    private void IncreaseDelay()
    {
        if (DashboardVolumeMeterDelayMs < DelayMax)
        {
            DashboardVolumeMeterDelayMs += 10;
        }
    }

    [RelayCommand]
    private void DecreaseDelay()
    {
        if (DashboardVolumeMeterDelayMs > DelayMin)
        {
            DashboardVolumeMeterDelayMs = Math.Max(DelayMin, DashboardVolumeMeterDelayMs - 10);
        }
    }

    [RelayCommand]
    private void IncreaseWidth()
    {
        if (Width < WidthMax)
        {
            Width += 160;
        }
    }

    [RelayCommand]
    private void DecreaseWidth()
    {
        if (Width > WidthMin)
        {
            Width = Math.Max(WidthMin, Width - 160);
        }
    }

    [RelayCommand]
    private void IncreaseHeight()
    {
        if (Height < HeightMax)
        {
            Height += 90;
        }
    }

    [RelayCommand]
    private void DecreaseHeight()
    {
        if (Height > HeightMin)
        {
            Height = Math.Max(HeightMin, Height - 90);
        }
    }

    public void Dispose()
    {
    }

    private bool CanOperateAlways() => !IsBusy;

    private bool CanOperateWhenEnabled() => !IsBusy && Enabled;

    private async Task RunObsOperationAsync(Func<CancellationToken, Task> operation)
    {
        IsBusy = true;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await operation(cts.Token);
        }
        catch (Exception exception)
        {
            _logger.ObsOperationFailed(exception);
            SetStatus($"● Ошибка: {ToSafeMessage(exception)}", StatusSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectAndLoadScenesAsync(ObsIntegrationSettings settings, string successAction, CancellationToken ct)
    {
        var snapshot = await _obsIntegration.ConnectAsync(settings, ct);

        if (!snapshot.IsConnected)
        {
            SetStatus($"● Ошибка: {snapshot.ErrorMessage}", StatusSeverity.Error);
            return;
        }

        var version = string.IsNullOrWhiteSpace(snapshot.ObsVersion) ? "версия не определена" : snapshot.ObsVersion;
        SetStatus($"● {successAction}: OBS {version}", StatusSeverity.Success);
        await LoadObsListsAsync(settings, ct);
    }

    private async Task LoadObsListsAsync(ObsIntegrationSettings settings, CancellationToken ct)
    {
        var scenes = await _obsIntegration.ListScenesAsync(settings, ct);
        var inputNames = await _obsIntegration.ListInputNamesAsync(settings, ct);
        var browserSourceNames = await _obsIntegration.ListBrowserSourceNamesAsync(settings, ct);

        IsAutoPopulating = true;

        try
        {
            var currentScene = SceneName;
            Scenes.Clear();

            foreach (var scene in scenes)
            {
                Scenes.Add(scene);
            }

            if (string.IsNullOrWhiteSpace(currentScene) && scenes.Count > 0)
            {
                SceneName = scenes[0];
            }

            PopulateSourceItems(DashboardSources, inputNames, ReadCheckedSources(DashboardSources));
            PopulateSourceItems(ChatRefreshSources, browserSourceNames, ReadCheckedSources(ChatRefreshSources));
        }
        finally
        {
            IsAutoPopulating = false;
        }
    }

    private static void PopulateSourceItems(
        ObservableCollection<ObsSourceItemViewModel> collection,
        IReadOnlyList<string> allNames,
        IReadOnlyList<string> selectedNames)
    {
        var selected = selectedNames
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var merged = allNames
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .Concat(selected)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        collection.Clear();

        foreach (var name in merged)
        {
            collection.Add(new ObsSourceItemViewModel(name, selected.Contains(name)));
        }
    }

    private static IReadOnlyList<string> ReadCheckedSources(IEnumerable<ObsSourceItemViewModel> items)
    {
        return items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
    }

    private ObsIntegrationSettings BuildCurrentSettings()
    {
        var settings = new ObsIntegrationSettings();
        SaveTo(settings);
        return settings;
    }

    private string BuildOverlayUrl()
    {
        var port = _settingsManager.Current.Twitch.HttpServerPort;
        return FormattableString.Invariant($"http://localhost:{port}/chat");
    }

    private void SetStatus(string text, StatusSeverity severity)
    {
        StatusText = text;
        StatusSeverity = severity;
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
}
