using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.ViewModels;
using MahApps.Metro.IconPacks;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat.Display;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Settings;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Diagnostics;
using System.Windows.Input;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public sealed partial class ChatDisplayTileViewModel : DashboardTileViewModel, IDisposable
{
    private const string WebView2RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private readonly SettingsManager _settings;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly ChatDisplayStore _store;
    private readonly IShellLauncher _shellLauncher;
    private readonly IDialogService _dialogService;
    private readonly IDisposable _chatDisplaySubscription;

    private readonly ToolbarItemViewModel _reloadAction;
    private readonly ToolbarItemViewModel _resetZoomAction;
    private readonly ToolbarItemViewModel _resetSessionAction;

    private TwitchOAuthRole _chatDisplayAccount;

    [ObservableProperty]
    private double _zoomFactor;

    [ObservableProperty]
    private bool _accountChangeRequiresRestart;

    [ObservableProperty]
    private bool _hasFallback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FallbackActionCommand))]
    private bool _showRuntimeDownload;

    [ObservableProperty]
    private string _fallbackHeading = string.Empty;

    [ObservableProperty]
    private string _fallbackDescription = string.Empty;

    public ChatDisplayTileViewModel(
        SettingsManager settings,
        ITargetChannelProvider targetChannelProvider,
        ILogger<ChatDisplayTileViewModel> logger,
        IShellLauncher shellLauncher,
        IDialogService dialogService,
        IEventBus bus,
        ChatDisplayStore store)
        : base("twitch-chat", "Чат", minWidth: 280, minHeight: 234)
    {
        _settings = settings;
        _targetChannelProvider = targetChannelProvider;
        _shellLauncher = shellLauncher;
        _dialogService = dialogService;
        _store = store;
        Logger = logger;

        var twitch = settings.Current.Twitch;
        _chatDisplayAccount = twitch.ChatDisplayAccount;
        UserDataFolder = WebView2UserDataFolders.Resolve(twitch.ChatDisplayAccount);
        AccountLabel = ResolveAccountLabel(twitch.ChatDisplayAccount);
        ChannelUri = BuildChannelUri(targetChannelProvider.Current.Login);

        _chatDisplaySubscription = bus.SubscribeOnUi<ChatDisplaySettingsChanged>(OnChatDisplaySettingsChanged);

        _zoomFactor = _store.LoadZoom();

        _reloadAction = new(PackIconLucideKind.RefreshCw, ReloadCommand, "Перезагрузить страницу чата");
        _resetZoomAction = new(PackIconLucideKind.Scaling, ResetZoomCommand, "Сбросить масштаб к значению по умолчанию");
        _resetSessionAction = new(PackIconLucideKind.Trash2, ResetSessionCommand, "Очистить куки и кэш активного аккаунта (бот или стример)");

        HeaderActions.Add(_reloadAction);
        HeaderActions.Add(_resetZoomAction);
        HeaderActions.Add(new(PackIconLucideKind.ExternalLink, OpenInBrowserCommand, "Открыть текущую страницу в системном браузере"));
        HeaderActions.Add(_resetSessionAction);
        HeaderActions.Add(new(PackIconLucideKind.Ban, EditBlockersCommand, "Блокировка баннеров: скрыть лишние элементы чата"));
    }

    public event Action? ReloadRequested;

    public event Action? ResetSessionRequested;

    public event Action? ClutterScriptChanged;

    public event Action? ChannelUriChanged;

    public override PackIconLucideKind Icon => PackIconLucideKind.MessageSquare;

    public override bool FillsAvailableSpace => true;

    public override bool ContentFillsTile => true;

    public ILogger Logger { get; }

    public string UserDataFolder { get; }

    public string AccountLabel { get; private set; }

    public Uri? ChannelUri { get; private set; }

    public ICommand? FallbackActionCommand => ShowRuntimeDownload ? OpenRuntimeDownloadCommand : null;

    public void Dispose()
    {
        _chatDisplaySubscription.Dispose();
    }

    public string BuildHideClutterScript()
    {
        return _store.BuildHideClutterScript();
    }

    public void ShowNoChannelFallback()
    {
        HasFallback = true;
        ShowRuntimeDownload = false;
        FallbackHeading = "Канал не указан";
        FallbackDescription = "Укажите канал в настройках Twitch, чтобы открыть чат.";
    }

    public void ShowRuntimeMissingFallback()
    {
        HasFallback = true;
        ShowRuntimeDownload = true;
        FallbackHeading = "Требуется Microsoft Edge WebView2 Runtime";
        FallbackDescription = "Установите его, чтобы увидеть чат Twitch.";
        DisableWebViewActions();
    }

    public void ShowGenericFallback()
    {
        HasFallback = true;
        ShowRuntimeDownload = false;
        FallbackHeading = "Не удалось открыть чат Twitch";
        FallbackDescription = "Подробности – в логах.";
        DisableWebViewActions();
    }

    partial void OnZoomFactorChanged(double value)
    {
        _store.SaveZoom(value);
    }

    private static Uri? BuildChannelUri(string? channel)
    {
        var trimmed = channel?.Trim();

        return string.IsNullOrEmpty(trimmed)
            ? null
            : new Uri($"https://www.twitch.tv/popout/{Uri.EscapeDataString(trimmed)}/chat?popout=");
    }

    private static string ResolveAccountLabel(TwitchOAuthRole role)
    {
        return role == TwitchOAuthRole.Broadcaster ? "стримера" : "бота";
    }

    private void OnChatDisplaySettingsChanged(ChatDisplaySettingsChanged @event)
    {
        if (@event.ChatDisplayAccount != _chatDisplayAccount)
        {
            _chatDisplayAccount = @event.ChatDisplayAccount;
            AccountLabel = ResolveAccountLabel(@event.ChatDisplayAccount);
            AccountChangeRequiresRestart = true;
        }

        var uri = BuildChannelUri(_targetChannelProvider.Current.Login);

        if (uri == ChannelUri)
        {
            return;
        }

        ChannelUri = uri;

        if (uri is null)
        {
            ShowNoChannelFallback();
            return;
        }

        HasFallback = false;
        ChannelUriChanged?.Invoke();
    }

    private void DisableWebViewActions()
    {
        _reloadAction.IsEnabled = false;
        _resetZoomAction.IsEnabled = false;
        _resetSessionAction.IsEnabled = false;
    }

    [RelayCommand]
    private void Reload()
    {
        ReloadRequested?.Invoke();
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomFactor = ChatDisplayStore.DefaultZoom;
    }

    [RelayCommand]
    private void ResetSession()
    {
        ResetSessionRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (ChannelUri is null)
        {
            return;
        }

        _shellLauncher.Open(ChannelUri.ToString());
    }

    [RelayCommand]
    private void OpenRuntimeDownload()
    {
        _shellLauncher.Open(WebView2RuntimeDownloadUrl);
    }

    [RelayCommand]
    private async Task EditBlockersAsync()
    {
        if (!_store.TryLoadBlockersText(out var selectors))
        {
            _dialogService.Error("Не удалось открыть правила",
                "Файл с правилами скрытия сейчас не читается: его держит другая программа или к нему нет доступа. Сохранённые правила не тронуты – попробуйте открыть их ещё раз чуть позже. Подробности – в логах.");

            return;
        }

        var dialog = new ChatBlockersDialogViewModel(selectors);

        if (!await _dialogService.ShowAsync(dialog))
        {
            return;
        }

        if (!_store.TrySaveBlockersText(dialog.Selectors))
        {
            _dialogService.Error("Ошибка", "Не удалось сохранить список блокираторов. Подробности – в логах.");
            return;
        }

        ClutterScriptChanged?.Invoke();
    }
}
